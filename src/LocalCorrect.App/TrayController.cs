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
    readonly ToolStripMenuItem _hotkeyLine = new() { Enabled = false };
    SettingsForm? _settingsForm;
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
            Text = "LocalCorrect"
        };
        _notify.DoubleClick += (_, _) => OpenSettings();

        var menu = new ContextMenuStrip();
        // Creating the first control installed the WinForms context; server events are posted to it.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _server.StateChanged += state => _ui.Post(_ => OnServerState(state), null);
        menu.Items.Add(_status);
        menu.Items.Add(_hotkeyLine);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        var pause = new ToolStripMenuItem("Pause") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) =>
        {
            _paused = pause.Checked;
            if (_paused) _hotkeys.Unregister();
            else ApplyHotkey();
            ShowStatus();
        };
        menu.Items.Add(pause);
        menu.Items.Add("Open log", null, (_, _) => OpenLog());
        menu.Items.Add("Licences", null, (_, _) => Ui.OpenLicences());
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
            _transport.SelectedParagraph = false;
            if (!KeyboardChord.WaitForRelease(_settings.Hotkey, 1000))
            {
                FileLog.Write(_logPath, _settings.Logging, "info", "hotkey still held");
                Balloon("Let go of the hotkey keys, then press them again.");
                return;
            }
            var timed = new TimedEngine(_engine);
            var workflow = new CorrectionWorkflow(_clipboard, _transport, timed);
            var result = workflow.Run(new CorrectionRequest
            {
                ProcessName = target.ProcessName,
                Elevated = target.Blocked,
                PasswordField = target.Password,
                CorrectParagraph = _settings.CorrectParagraphWhenNothingSelected,
                WindowClass = target.FocusClass,
                HtmlFormat = Win32Clipboard.HtmlFormat,
                Preview = _settings.PreviewBeforeReplace,
                Language = _settings.Language,
                ExcludedProcesses = _settings.ExcludedProcesses
            }, (original, corrected, edits) => PreviewForm.Ask(original, corrected, edits,
                new PreviewDetails(timed.Last?.Language, timed.Last?.Engine, timed.Last?.Notice, _transport.SelectedParagraph)));
            var category = result.Status is WorkflowStatus.Failed or WorkflowStatus.ClipboardBusy or WorkflowStatus.ClipboardTooLarge ? "error" : "info";
            FileLog.Write(_logPath, _settings.Logging, category,
                result.Status + " chars=" + result.Characters.ToString() + " edits=" + result.Edits.ToString()
                + " lang=" + (result.Language ?? "-") + " engine=" + (timed.Last?.Engine ?? _engine.Id)
                + " ms=" + (timed.Milliseconds?.ToString() ?? "-") + " process=" + ProcessFilter.CoreName(target.ProcessName)
                + " class=" + target.FocusClass);
            // The preview already showed the outcome and any notice.
            if (result.Status == WorkflowStatus.PreviewDeclined || (result.Status == WorkflowStatus.Replaced && _settings.PreviewBeforeReplace))
                return;
            var message = result.Status == WorkflowStatus.Unchanged
                ? "Nothing to correct in " + (_transport.SelectedParagraph ? "this paragraph." : "the selection.")
                : result.Message;
            var notice = timed.Last?.Notice;
            Balloon(notice == null ? message : message + " " + notice, problem: category == "error");
        }
        catch (Exception ex)
        {
            FileLog.Write(_logPath, true, "error", "hotkey " + ex.GetType().Name);
            Balloon("Something went wrong. Nothing was changed.", problem: true);
        }
        finally
        {
            _busy = false;
        }
    }

    void OpenSettings()
    {
        if (_settingsForm != null)
        {
            _settingsForm.Activate();
            return;
        }
        using var form = new SettingsForm(_settings.Clone(), Status());
        _settingsForm = form;
        try
        {
            if (form.ShowDialog() != DialogResult.OK || form.Saved == null) return;
        }
        finally
        {
            _settingsForm = null;
        }
        _settings = form.Saved;
        try
        {
            SettingsStore.Save(AppPaths.SettingsFile(), _settings);
        }
        catch (Exception ex)
        {
            FileLog.Write(_logPath, true, "error", "settings " + ex.GetType().Name);
            Balloon("The settings could not be saved.", problem: true);
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
            Balloon("The settings were saved, but starting with Windows could not be changed.", problem: true);
        }
        ShowStatus();
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
        ShowStatus();
        if (!string.IsNullOrEmpty(warning))
        {
            FileLog.Write(_logPath, true, "error", "engine rejected");
            Balloon(warning, problem: true);
        }
    }

    void ApplyHotkey()
    {
        if (_hotkeys.Register(_settings.Hotkey)) return;
        FileLog.Write(_logPath, true, "error", "hotkey busy " + _settings.Hotkey.Display);
        Balloon(_settings.Hotkey.Display + " is taken by another program, so the hotkey is off. Choose another one in Settings.", problem: true);
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

    void Balloon(string message, bool problem = false)
    {
        try
        {
            _notify.ShowBalloonTip(4000, "LocalCorrect", message.Length > 220 ? message.Substring(0, 220) : message, problem ? ToolTipIcon.Warning : ToolTipIcon.None);
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
            Balloon("LanguageTool stopped working. The built-in word lists are used until you change a setting or restart LocalCorrect.", problem: true);
        if (state == LanguageToolState.NotInstalled)
            Balloon("LanguageTool is missing from this copy of LocalCorrect. The built-in word lists are used.", problem: true);
        ShowStatus();
    }

    /// <summary>The two grey lines at the top of the tray menu, the tooltip and the line in an open settings window.</summary>
    void ShowStatus()
    {
        _status.Text = _paused ? "Paused. The hotkey is off." : Status();
        _hotkeyLine.Text = "Hotkey: " + _settings.Hotkey.Display;
        _settingsForm?.ShowLanguageToolState(Status());
        var state = _paused ? "Paused" : _settings.Hotkey.Display + "\n" + Status();
        // The tooltip holds 63 characters; the version number gives way first.
        var text = Program.VersionLabel() + "\n" + state;
        if (text.Length > 63) text = "LocalCorrect\n" + state;
        _notify.Text = text.Length <= 63 ? text : text.Substring(0, 63);
    }

    /// <summary>What corrects the text right now, in a few words.</summary>
    string Status()
    {
        if (!ReferenceEquals(_engine, _languageTool))
            return _engine is OllamaCorrectionEngine ? "Using the local model" : "Using the built-in word lists";
        return _server.State switch
        {
            LanguageToolState.Starting => "LanguageTool is starting",
            LanguageToolState.Ready => "LanguageTool is ready",
            LanguageToolState.Failed => "LanguageTool failed, using word lists",
            LanguageToolState.NotInstalled => "LanguageTool missing, using word lists",
            _ => "LanguageTool is off"
        };
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
