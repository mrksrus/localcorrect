namespace LocalCorrect.Core;

public enum WorkflowStatus
{
    Replaced,
    Unchanged,
    NoSelection,
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
    public int CopyWaitMilliseconds { get; init; } = 1200;
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
        if (request.PasswordField)
            return new WorkflowResult(WorkflowStatus.PasswordField, "LocalCorrect skips password fields.");
        if (request.Elevated)
            return new WorkflowResult(WorkflowStatus.Elevated, "That program is running as administrator. LocalCorrect left it alone.");
        if (ProcessFilter.IsExcluded(request.ProcessName, request.ExcludedProcesses))
        {
            var name = ProcessFilter.CoreName(request.ProcessName);
            return new WorkflowResult(WorkflowStatus.Excluded, "LocalCorrect is off for " + name + ".");
        }

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

        var before = _clipboard.SequenceNumber;
        try
        {
            _transport.Copy();
        }
        catch (Exception)
        {
            return new WorkflowResult(WorkflowStatus.Failed, "Could not copy the selection. Nothing was changed.");
        }

        if (!WaitForChange(before, request.CopyWaitMilliseconds))
            return new WorkflowResult(WorkflowStatus.NoSelection, "Select some text, then press the hotkey again.");

        string? text;
        try
        {
            text = ClipboardSession.ReadUnicode(_clipboard, wait: _sleep);
        }
        catch (ClipboardBusyException)
        {
            TryRestore(original);
            return new WorkflowResult(WorkflowStatus.ClipboardBusy, "The clipboard was busy. Try again.");
        }
        catch (Exception)
        {
            TryRestore(original);
            return new WorkflowResult(WorkflowStatus.Failed, "Could not read the selection. Nothing was changed.");
        }

        if (!TryRestore(original))
            return new WorkflowResult(WorkflowStatus.Failed, "Could not restore the clipboard. It may still hold the copied selection.");

        if (string.IsNullOrEmpty(text))
            return new WorkflowResult(WorkflowStatus.NoSelection, "The selection is not text. Nothing was changed.");
        if (text.Length > request.MaxCharacters)
            return new WorkflowResult(WorkflowStatus.TooLong, "That selection is too long. LocalCorrect left it alone.", text.Length);

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
        return new WorkflowResult(WorkflowStatus.Replaced, "Corrected the selection (" + correction.Edits.Count.ToString() + " " + noun + ").", text.Length, correction.Edits.Count, correction.Language);
    }

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
