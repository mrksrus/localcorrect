namespace LocalCorrect.Core;

public enum WorkflowStatus
{
    Replaced,
    Unchanged,
    NoSelection,
    ParagraphSkipped,
    Excluded,
    Elevated,
    ClipboardBusy,
    TooLong,
    ClipboardTooLarge,
    PreviewDeclined,
    PasswordField,
    Failed
}

public sealed class WorkflowResult
{
    public WorkflowResult(WorkflowStatus status, string message, int characters = 0, int edits = 0, string? language = null)
    {
        Status = status;
        Message = message ?? "";
        Characters = characters;
        Edits = edits;
        Language = language;
    }

    public WorkflowStatus Status { get; }
    public string Message { get; }
    public int Characters { get; }
    public int Edits { get; }
    public string? Language { get; }
}

public delegate bool PreviewDecision(string original, string corrected, IReadOnlyList<CorrectionEdit> edits);

public sealed class CorrectionRequest
{
    public string? ProcessName { get; init; }
    public bool Elevated { get; init; }
    public bool PasswordField { get; init; }
    public bool Preview { get; init; } = true;
    public string Language { get; init; } = "auto";
    public IReadOnlyList<string> ExcludedProcesses { get; init; } = Array.Empty<string>();
    public int MaxCharacters { get; init; } = 50_000;
    /// <summary>With nothing selected, select and correct the current paragraph (W-05).</summary>
    public bool CorrectParagraph { get; init; } = true;
    /// <summary>Class name of the focused window; picks the paragraph keys.</summary>
    public string? WindowClass { get; init; }
    /// <summary>Registered id of the clipboard's "HTML Format"; 0 skips the formatting check.</summary>
    public uint HtmlFormat { get; init; }
    /// <summary>How long the first copy may take before the app decides nothing is selected.</summary>
    /// <remarks>As long as 0.1.0's wait: a shorter one let a slow program's late copy of a selection be read as
    /// the paragraph, and the paste then replaced the whole paragraph.</remarks>
    public int CopyWaitMilliseconds { get; init; } = 1200;
    /// <summary>Wait for a copy that must come: the paragraph copy, and the first copy when the fallback is off.</summary>
    public int SlowCopyWaitMilliseconds { get; init; } = 1200;
    /// <summary>A copy is read only after the clipboard has not changed for this long, so a late earlier copy is not read.</summary>
    public int QuietMilliseconds { get; init; } = 120;
    public int PasteSettleMilliseconds { get; init; } = 300;
}

/// <summary>
/// Copies the selection, corrects it, pastes the result, and puts the previous
/// clipboard back, including formats that are not text. All of the platform
/// details are injected so this can run under tests.
/// </summary>
public sealed class CorrectionWorkflow
{
    readonly IClipboardAccess _clipboard;
    readonly ISelectionTransport _transport;
    readonly ICorrectionEngine _engine;
    readonly Action<int>? _sleep;
    bool _selected;

    public CorrectionWorkflow(IClipboardAccess clipboard, ISelectionTransport transport, ICorrectionEngine engine, Action<int>? sleep = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _sleep = sleep;
    }

    public WorkflowResult Run(CorrectionRequest request, PreviewDecision? preview = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        _selected = false;
        var result = RunCore(request, preview);
        // A paragraph the app selected must not stay selected under the user's next keystroke.
        if (_selected && result.Status != WorkflowStatus.Replaced)
        {
            try
            {
                _transport.Select(SelectKeys.Collapse);
            }
            catch (Exception)
            {
                // The text is unchanged; only the selection stays.
            }
        }
        return result;
    }

    WorkflowResult RunCore(CorrectionRequest request, PreviewDecision? preview)
    {
        if (request.PasswordField)
            return new WorkflowResult(WorkflowStatus.PasswordField, "LocalCorrect skips password fields.");
        if (request.Elevated)
            return new WorkflowResult(WorkflowStatus.Elevated, "That program is running as administrator. LocalCorrect left it alone.");
        if (ProcessFilter.IsExcluded(request.ProcessName, request.ExcludedProcesses))
        {
            var name = ProcessFilter.CoreName(request.ProcessName);
            return new WorkflowResult(WorkflowStatus.Excluded, "LocalCorrect is off for " + name + ".");
        }
        if (ProcessFilter.IsExcluded(request.ProcessName, ProcessFilter.NeverCorrect))
            return new WorkflowResult(WorkflowStatus.Excluded, "LocalCorrect does nothing in terminals, code editors and Excel.");

        ClipboardArchive original;
        try
        {
            original = ClipboardSession.Capture(_clipboard, wait: _sleep);
        }
        catch (ClipboardBusyException)
        {
            return new WorkflowResult(WorkflowStatus.ClipboardBusy, "The clipboard was busy. Try again.");
        }
        catch (ClipboardTooLargeException)
        {
            return new WorkflowResult(WorkflowStatus.ClipboardTooLarge, "The clipboard is holding something very large. LocalCorrect left it alone.");
        }
        catch (Exception)
        {
            return new WorkflowResult(WorkflowStatus.Failed, "Could not read the clipboard. Nothing was changed.");
        }

        var captured = _clipboard.SequenceNumber;
        var paragraph = false;
        string? text;
        byte[]? html = null;
        try
        {
            text = CopyAndRead(request.CorrectParagraph ? request.CopyWaitMilliseconds : request.SlowCopyWaitMilliseconds, request.QuietMilliseconds, 0, out var changed, out _);
            if (!changed && !request.CorrectParagraph)
                return new WorkflowResult(WorkflowStatus.NoSelection, "Select some text, then press the hotkey again.");
            if (request.CorrectParagraph && (!changed || text == ""))
            {
                paragraph = true;
                _selected = true;
                _transport.Select(NoSelectionPolicy.KeysFor(request.WindowClass));
                text = CopyAndRead(request.SlowCopyWaitMilliseconds, request.QuietMilliseconds, request.HtmlFormat, out _, out html) ?? "";
                // Leave the paragraph mark out of the replacement so the next paragraph keeps its formatting.
                var dropped = NoSelectionPolicy.DropTrailingBreak(ref text);
                if (dropped) _transport.Select(SelectKeys.DropLast);
                // Collapse even when nothing was copied (a web password box refuses Ctrl+C but stays selected).
                _selected = text.Length > 0 || !dropped;
            }
        }
        catch (ClipboardBusyException)
        {
            RestoreIfChanged(original, captured);
            return new WorkflowResult(WorkflowStatus.ClipboardBusy, "The clipboard was busy. Try again.");
        }
        catch (ClipboardTooLargeException)
        {
            RestoreIfChanged(original, captured);
            return new WorkflowResult(WorkflowStatus.ClipboardTooLarge, "The copied text is very large. LocalCorrect left it alone.");
        }
        catch (Exception)
        {
            RestoreIfChanged(original, captured);
            return new WorkflowResult(WorkflowStatus.Failed, "Could not copy the text. Nothing was changed.");
        }

        if (!RestoreIfChanged(original, captured))
            return new WorkflowResult(WorkflowStatus.Failed, "Could not restore the clipboard. It may still hold the copied text.");

        if (paragraph)
        {
            if (string.IsNullOrEmpty(text))
                return new WorkflowResult(WorkflowStatus.NoSelection, "Nothing to correct here. Select some text, then press the hotkey again.");
            var reason = NoSelectionPolicy.Reject(text, html);
            if (reason != null)
                return new WorkflowResult(WorkflowStatus.ParagraphSkipped, reason, text.Length);
        }
        else
        {
            if (string.IsNullOrEmpty(text))
                return new WorkflowResult(WorkflowStatus.NoSelection, "The selection is not text. Nothing was changed.");
            if (text.Length > request.MaxCharacters)
                return new WorkflowResult(WorkflowStatus.TooLong, "That selection is too long. LocalCorrect left it alone.", text.Length);
        }

        CorrectionResult correction;
        try
        {
            correction = _engine.Correct(text, request.Language);
        }
        catch (LocalEngineException ex)
        {
            return new WorkflowResult(WorkflowStatus.Failed, ex.Message, text.Length);
        }
        catch (Exception)
        {
            return new WorkflowResult(WorkflowStatus.Failed, "Correction failed. Nothing was changed.", text.Length);
        }

        if (correction.Edits.Count == 0 || correction.Text == text)
            return new WorkflowResult(WorkflowStatus.Unchanged, "No spelling changes.", text.Length, 0, correction.Language);

        if (request.Preview)
        {
            var accepted = false;
            try
            {
                accepted = preview != null && preview(text, correction.Text, correction.Edits);
            }
            catch (Exception)
            {
                return new WorkflowResult(WorkflowStatus.Failed, "The preview could not be shown. Nothing was changed.", text.Length, correction.Edits.Count, correction.Language);
            }
            if (!accepted)
                return new WorkflowResult(WorkflowStatus.PreviewDeclined, "Canceled.", text.Length, correction.Edits.Count, correction.Language);
        }

        ClipboardArchive current;
        try
        {
            current = ClipboardSession.Capture(_clipboard, wait: _sleep);
        }
        catch (ClipboardBusyException)
        {
            return new WorkflowResult(WorkflowStatus.ClipboardBusy, "The clipboard was busy. Nothing was changed.", text.Length, correction.Edits.Count, correction.Language);
        }
        catch (ClipboardTooLargeException)
        {
            return new WorkflowResult(WorkflowStatus.ClipboardTooLarge, "The clipboard is holding something very large. Nothing was changed.", text.Length, correction.Edits.Count, correction.Language);
        }
        catch (Exception)
        {
            return new WorkflowResult(WorkflowStatus.Failed, "Could not borrow the clipboard to paste. Nothing was changed.", text.Length, correction.Edits.Count, correction.Language);
        }

        try
        {
            ClipboardSession.WriteText(_clipboard, correction.Text, wait: _sleep);
            _transport.Paste();
            _selected = false;
            Pause(request.PasteSettleMilliseconds);
        }
        catch (Exception)
        {
            TryRestore(current);
            return new WorkflowResult(WorkflowStatus.Failed, "Could not paste the correction. Nothing was changed.", text.Length, correction.Edits.Count, correction.Language);
        }

        if (!TryRestore(current))
            return new WorkflowResult(WorkflowStatus.Failed, "The selection was replaced, but the previous clipboard could not be restored.", text.Length, correction.Edits.Count, correction.Language);

        var noun = correction.Edits.Count == 1 ? "change" : "changes";
        return new WorkflowResult(WorkflowStatus.Replaced, "Corrected the " + (paragraph ? "paragraph" : "selection") + " (" + correction.Edits.Count.ToString() + " " + noun + ").", text.Length, correction.Edits.Count, correction.Language);
    }

    /// <summary>Sends Ctrl+C and reads the copy; changed is false when the clipboard did not change in time.</summary>
    string? CopyAndRead(int wait, int quiet, uint htmlFormat, out bool changed, out byte[]? html)
    {
        html = null;
        var before = _clipboard.SequenceNumber;
        _transport.Copy();
        changed = WaitForChange(before, wait);
        if (!changed) return null;
        WaitForQuiet(quiet);
        if (htmlFormat == 0) return ClipboardSession.ReadUnicode(_clipboard, wait: _sleep);
        string? text = null;
        foreach (var entry in ClipboardSession.Capture(_clipboard, wait: _sleep).Entries)
        {
            if (entry.Format == ClipboardFormatPolicy.UnicodeText) text = ClipboardText.Decode(entry.Data);
            else if (entry.Format == htmlFormat) html = entry.Data;
        }
        return text;
    }

    /// <summary>Keys reach the target in order, so a late copy is followed by the later one; read only once both landed.</summary>
    void WaitForQuiet(int quiet)
    {
        var last = _clipboard.SequenceNumber;
        // Capped so a program that rewrites the clipboard all the time cannot hold the hotkey.
        for (int still = 0, total = 0; still < quiet && total < 1000; total += 10)
        {
            Pause(10);
            var now = _clipboard.SequenceNumber;
            still = now == last ? still + 10 : 0;
            last = now;
        }
    }

    bool RestoreIfChanged(ClipboardArchive original, uint captured) => _clipboard.SequenceNumber == captured || TryRestore(original);

    bool WaitForChange(uint before, int timeoutMilliseconds)
    {
        var waited = 0;
        while (_clipboard.SequenceNumber == before)
        {
            if (waited >= timeoutMilliseconds) return false;
            var slice = Math.Min(25, Math.Max(1, timeoutMilliseconds - waited));
            Pause(slice);
            waited += slice;
        }
        return true;
    }

    bool TryRestore(ClipboardArchive archive)
    {
        try
        {
            ClipboardSession.Restore(_clipboard, archive, wait: _sleep);
            return true;
        }
        catch
        {
            return false;
        }
    }

    void Pause(int milliseconds)
    {
        if (milliseconds <= 0) return;
        if (_sleep != null) _sleep(milliseconds);
        else Thread.Sleep(milliseconds);
    }
}
