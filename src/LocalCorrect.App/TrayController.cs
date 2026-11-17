using System.Diagnostics;
using LocalCorrect.App.Win32;
using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.App;

internal sealed class TrayController : ApplicationContext
{
    readonly string _logPath;
    readonly SymSpellCorrector _symspell;
    readonly LanguageToolServer _server;
    readonly LanguageToolEngine _languageTool;
    readonly ToolStripMenuItem _status = new() { Enabled = false };
    readonly SynchronizationContext _ui;
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
        _server = new LanguageToolServer(AppContext.BaseDirectory, process =>
        {
            try
            {
                ChildProcessJob.Add(process);
            }
            catch (Exception ex)
            {
                FileLog.Write(_logPath, true, "error", "languagetool job " + ex.GetType().Name);
            }
        });
        _languageTool = new LanguageToolEngine(_server, symspell);
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
        // Creating the first control installed the WinForms context; server events are posted to it.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _server.StateChanged += state => _ui.Post(_ => OnServerState(state), null);
        menu.Items.Add(_status);
        menu.Items.Add(new ToolStripSeparator());
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
            var timed = new TimedEngine(_engine);
            var workflow = new CorrectionWorkflow(_clipboard, _transport, timed);
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
                + " lang=" + (result.Language ?? "-") + " engine=" + (timed.Last?.Engine ?? _engine.Id)
                + " ms=" + (timed.Milliseconds?.ToString() ?? "-") + " process=" + ProcessFilter.CoreName(target.ProcessName));
            var notice = timed.Last?.Notice;
            if (result.Status == WorkflowStatus.PreviewDeclined || (result.Status == WorkflowStatus.Replaced && _settings.PreviewBeforeReplace))
            {
                if (notice != null) Balloon(notice);
                return;
            }
            Balloon(notice == null ? result.Message : result.Message + " " + notice);
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
        var next = CorrectionEngineFactory.Create(_settings, _symspell, _languageTool, null, out var warning);
        if (!ReferenceEquals(_engine, next) && _engine is IDisposable disposable)
            disposable.Dispose();
        _engine = next;
        // Starting again also leaves Failed: a settings change is the user's retry. Stopping frees about 1 GB.
        if (ReferenceEquals(next, _languageTool)) _server.Start();
        else _server.Stop();
        ShowServerState();
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
        if (_engine is IDisposable disposable) disposable.Dispose();
        _server.Dispose();
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

    void OnServerState(LanguageToolState state)
    {
        if (state == LanguageToolState.Ready)
            FileLog.Write(_logPath, _settings.Logging, "info", "languagetool ready listen_ms=" + ((long)_server.ListenTime.TotalMilliseconds).ToString()
                + " warmup_ms=" + ((long)_server.WarmUpTime.TotalMilliseconds).ToString() + " starts=" + _server.Starts.ToString());
        else if (state is LanguageToolState.Failed or LanguageToolState.NotInstalled)
            FileLog.Write(_logPath, true, "error", "languagetool " + state.ToString().ToLowerInvariant());
        if (state == LanguageToolState.Failed)
            Balloon("LanguageTool stopped working. The built-in word lists are used until you change a setting or restart LocalCorrect.");
        if (state == LanguageToolState.NotInstalled)
            Balloon("LanguageTool is not installed with this copy of LocalCorrect. The built-in word lists are used.");
        ShowServerState();
    }

    void ShowServerState()
    {
        _status.Text = "LanguageTool: " + ServerState()
            + (_server.State is LanguageToolState.Failed or LanguageToolState.NotInstalled ? " (using the word lists)" : "");
        _notify.Text = Tooltip();
    }

    string ServerState() => _server.State switch
    {
        LanguageToolState.Starting => "starting",
        LanguageToolState.Ready => "ready",
        LanguageToolState.Failed => "failed",
        LanguageToolState.NotInstalled => "not installed",
        _ => "off"
    };

    string Tooltip()
    {
        var text = Program.VersionLabel() + " — " + (_paused ? "paused" : _settings.Hotkey.Display)
            + (ReferenceEquals(_engine, _languageTool) ? " — " + ServerState() : "");
        return text.Length <= 63 ? text : text.Substring(0, 63);
    }
}

/// <summary>Records which engine produced the result, its notice and how long the engine took, for the log and the tray.</summary>
internal sealed class TimedEngine : ICorrectionEngine
{
    readonly ICorrectionEngine _inner;

    public TimedEngine(ICorrectionEngine inner) => _inner = inner;

    public string Id => _inner.Id;
    public string DisplayName => _inner.DisplayName;
    public CorrectionResult? Last { get; private set; }
    public long? Milliseconds { get; private set; }

    public CorrectionResult Correct(string text, string language)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            return Last = _inner.Correct(text, language);
        }
        finally
        {
            Milliseconds = clock.ElapsedMilliseconds;
        }
    }
}
