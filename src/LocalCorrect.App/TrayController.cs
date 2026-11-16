using System.Diagnostics;
using LocalCorrect.App.Win32;
using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.App;

internal sealed class TrayController : ApplicationContext
{
    readonly string _logPath;
    readonly SymSpellCorrector _symspell;
    readonly NotifyIcon _notify;
    readonly HotkeyWindow _hotkeys = new();
    readonly Win32Clipboard _clipboard = new();
    readonly Win32SelectionTransport _transport = new();
    readonly Icon? _ownedIcon;
    AppSettings _settings;
    ICorrectionEngine _engine;
    bool _paused;
    bool _busy;

    public TrayController(AppSettings settings, SymSpellCorrector symspell, string logPath)
    {
        _settings = settings;
        _symspell = symspell;
        _logPath = logPath;
        _engine = symspell;
        try
        {
            if (!string.IsNullOrEmpty(Environment.ProcessPath))
                _ownedIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
        }
        catch
        {
            _ownedIcon = null;
        }

        _notify = new NotifyIcon
        {
            Icon = _ownedIcon ?? SystemIcons.Application,
            Visible = true,
            Text = Tooltip()
        };
        _notify.DoubleClick += (_, _) => OpenSettings();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        var pause = new ToolStripMenuItem("Pause") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) =>
        {
            _paused = pause.Checked;
            if (_paused) _hotkeys.Unregister();
            else ApplyHotkey();
            _notify.Text = Tooltip();
        };
        menu.Items.Add(pause);
        menu.Items.Add("Open log", null, (_, _) => OpenLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        _notify.ContextMenuStrip = menu;

        _clipboard.Owner = _hotkeys.Handle;
        _hotkeys.Pressed += OnHotkey;
        if (Environment.Is64BitProcess && KeyboardChord.InputSize != 40)
            FileLog.Write(_logPath, true, "error", "keyboard struct size " + KeyboardChord.InputSize.ToString());
        ApplyEngine();
        ApplyHotkey();
        try
        {
            StartupRegistration.Set(_settings.StartWithWindows, Environment.ProcessPath ?? "");
        }
        catch (Exception ex)
        {
            FileLog.Write(_logPath, true, "error", "startup registration " + ex.GetType().Name);
        }
    }

    void OnHotkey()
    {
        if (_busy || _paused) return;
        _busy = true;
        try
        {
            var target = ForegroundProbe.Capture();
            _transport.Target = target.Handle;
            var workflow = new CorrectionWorkflow(_clipboard, _transport, _engine);
            var result = workflow.Run(new CorrectionRequest
            {
                ProcessName = target.ProcessName,
                Elevated = target.Blocked,
                PasswordField = target.Password,
                Preview = _settings.PreviewBeforeReplace,
                Language = _settings.Language,
                ExcludedProcesses = _settings.ExcludedProcesses
            }, (original, corrected, edits) => PreviewForm.Ask(original, corrected, edits));
            var category = result.Status is WorkflowStatus.Failed or WorkflowStatus.ClipboardBusy or WorkflowStatus.ClipboardTooLarge ? "error" : "info";
            FileLog.Write(_logPath, _settings.Logging, category,
                result.Status + " chars=" + result.Characters.ToString() + " edits=" + result.Edits.ToString()
                + " lang=" + (result.Language ?? "-") + " process=" + ProcessFilter.CoreName(target.ProcessName));
            if (result.Status == WorkflowStatus.PreviewDeclined) return;
            if (result.Status == WorkflowStatus.Replaced && _settings.PreviewBeforeReplace) return;
            Balloon(result.Message);
        }
        catch (Exception ex)
        {
            FileLog.Write(_logPath, true, "error", "hotkey " + ex.GetType().Name);
            Balloon("Something went wrong. Nothing was changed.");
        }
        finally
        {
            _busy = false;
        }
    }

    void OpenSettings()
    {
        using var form = new SettingsForm(_settings.Clone());
        if (form.ShowDialog() != DialogResult.OK || form.Saved == null) return;
        _settings = form.Saved;
        try
        {
            SettingsStore.Save(AppPaths.SettingsFile(), _settings);
        }
        catch (Exception ex)
        {
            FileLog.Write(_logPath, true, "error", "settings " + ex.GetType().Name);
            Balloon("Could not save settings.");
            return;
        }
        ApplyEngine();
        if (!_paused) ApplyHotkey();
        try
        {
            StartupRegistration.Set(_settings.StartWithWindows, Environment.ProcessPath ?? "");
        }
        catch (Exception ex)
        {
            FileLog.Write(_logPath, true, "error", "startup registration " + ex.GetType().Name);
            Balloon("Settings were saved, but Windows startup could not be updated.");
        }
        _notify.Text = Tooltip();
    }

    void ApplyEngine()
    {
        var next = CorrectionEngineFactory.Create(_settings, _symspell, null, out var warning);
        if (!ReferenceEquals(_engine, next) && !ReferenceEquals(_engine, _symspell) && _engine is IDisposable disposable)
            disposable.Dispose();
        _engine = next;
        if (!string.IsNullOrEmpty(warning))
        {
            FileLog.Write(_logPath, true, "error", "engine rejected");
            Balloon(warning);
        }
    }

    void ApplyHotkey()
    {
        if (_hotkeys.Register(_settings.Hotkey)) return;
        FileLog.Write(_logPath, true, "error", "hotkey busy " + _settings.Hotkey.Display);
        Balloon("Could not register " + _settings.Hotkey.Display + ". Another program may be using it. Change it in Settings.");
    }

    void OpenLog()
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (!File.Exists(_logPath)) File.WriteAllText(_logPath, "");
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + _logPath + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            FileLog.Write(_logPath, true, "error", "open log " + ex.GetType().Name);
        }
    }

    void Exit()
    {
        _hotkeys.Pressed -= OnHotkey;
        _hotkeys.Dispose();
        _notify.Visible = false;
        _notify.Dispose();
        _ownedIcon?.Dispose();
        if (!ReferenceEquals(_engine, _symspell) && _engine is IDisposable disposable) disposable.Dispose();
        ExitThread();
    }

    void Balloon(string message)
    {
        try
        {
            _notify.BalloonTipTitle = "LocalCorrect";
            _notify.BalloonTipText = message.Length > 220 ? message.Substring(0, 220) : message;
            _notify.ShowBalloonTip(4000);
        }
        catch
        {
            // A missing balloon must not undo a correction.
        }
    }

    string Tooltip()
    {
        var text = Program.VersionLabel() + " — " + (_paused ? "paused" : _settings.Hotkey.Display);
        return text.Length <= 63 ? text : text.Substring(0, 63);
    }
}
