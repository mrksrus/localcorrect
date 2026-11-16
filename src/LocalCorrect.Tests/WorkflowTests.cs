using LocalCorrect.Core;

namespace LocalCorrect.Tests;

public class WorkflowTests
{
    static readonly Action<int> NoWait = _ => { };

    sealed class FakeEngine : ICorrectionEngine
    {
        public int Calls;
        public Func<string, CorrectionResult> Handle = text =>
        {
            var next = text.Replace("teh", "the", StringComparison.Ordinal);
            var edits = next == text
                ? Array.Empty<CorrectionEdit>()
                : new[] { new CorrectionEdit(0, text.Length, text, next) };
            return new CorrectionResult(next, "en", edits);
        };

        public string Id => "fake";
        public string DisplayName => "Fake";
        public CorrectionResult Correct(string text, string language)
        {
            Calls++;
            return Handle(text);
        }
    }

    sealed class FakeTransport : ISelectionTransport
    {
        public FakeTransport(MemoryClipboard clipboard) => Clipboard = clipboard;
        public MemoryClipboard Clipboard { get; }
        public string? Selection { get; set; }
        public int Copies { get; private set; }
        public List<string> Pasted { get; } = new();

        public void Copy()
        {
            Copies++;
            if (Selection == null) return;
            ClipboardSession.WriteText(Clipboard, Selection, attempts: 2, wait: NoWait);
        }

        public void Paste()
        {
            Pasted.Add(ClipboardSession.ReadUnicode(Clipboard, attempts: 2, wait: NoWait) ?? "");
        }
    }

    static (MemoryClipboard Clip, FakeTransport Transport, FakeEngine Engine, CorrectionWorkflow Workflow) Harness()
    {
        var clip = new MemoryClipboard();
        var custom = new byte[] { 1, 2, 3, 0, 9 };
        ClipboardSession.WriteText(clip, "secret", attempts: 1, wait: NoWait);
        var seeded = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        ClipboardSession.Restore(clip, new ClipboardArchive(seeded.Entries.Append(new ClipboardEntry(0xC100, custom)).ToList()), attempts: 1, wait: NoWait);
        var transport = new FakeTransport(clip);
        var engine = new FakeEngine();
        var workflow = new CorrectionWorkflow(clip, transport, engine, NoWait);
        return (clip, transport, engine, workflow);
    }

    static CorrectionRequest Request(bool preview = false) => new()
    {
        Preview = preview,
        CopyWaitMilliseconds = 0,
        PasteSettleMilliseconds = 0,
        Language = "en"
    };

    static void AssertClipboardRestored(MemoryClipboard clip)
    {
        Assert.Equal("secret", ClipboardSession.ReadUnicode(clip, attempts: 1, wait: NoWait));
        var saved = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        Assert.Equal(new byte[] { 1, 2, 3, 0, 9 }, Assert.Single(saved.Entries, entry => entry.Format == 0xC100).Data);
    }

    [Fact]
    public void ReplacePastesTheCorrectionAndRestoresCustomFormats()
    {
        var (clip, transport, engine, workflow) = Harness();
        transport.Selection = "teh cat";
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.Replaced, result.Status);
        Assert.Equal(new[] { "the cat" }, transport.Pasted);
        Assert.Equal(1, engine.Calls);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void NoSelectionDoesNotTouchTheClipboardOrTheEngine()
    {
        var (clip, transport, engine, workflow) = Harness();
        transport.Selection = null;
        var before = clip.SequenceNumber;
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.NoSelection, result.Status);
        Assert.Equal(0, engine.Calls);
        Assert.Empty(transport.Pasted);
        Assert.Equal(before, clip.SequenceNumber);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void DeclinedPreviewDoesNotPaste()
    {
        var (clip, transport, _, workflow) = Harness();
        transport.Selection = "teh cat";
        var result = workflow.Run(Request(preview: true), (_, _, _) => false);
        Assert.Equal(WorkflowStatus.PreviewDeclined, result.Status);
        Assert.Empty(transport.Pasted);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void MissingPreviewCallbackDeclines()
    {
        var (_, transport, _, workflow) = Harness();
        transport.Selection = "teh cat";
        var result = workflow.Run(Request(preview: true), preview: null);
        Assert.Equal(WorkflowStatus.PreviewDeclined, result.Status);
        Assert.Empty(transport.Pasted);
    }

    [Fact]
    public void PreviewOffDoesNotAsk()
    {
        var (_, transport, _, workflow) = Harness();
        transport.Selection = "teh cat";
        var asked = false;
        var result = workflow.Run(Request(), (_, _, _) => { asked = true; return false; });
        Assert.Equal(WorkflowStatus.Replaced, result.Status);
        Assert.False(asked);
        Assert.Equal(new[] { "the cat" }, transport.Pasted);
    }

    [Fact]
    public void UnchangedTextIsNotPasted()
    {
        var (clip, transport, _, workflow) = Harness();
        transport.Selection = "the cat";
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.Unchanged, result.Status);
        Assert.Empty(transport.Pasted);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void ExcludedElevatedAndPasswordSkipTheClipboard()
    {
        var (clip, transport, engine, workflow) = Harness();
        var before = clip.SequenceNumber;
        Assert.Equal(WorkflowStatus.Excluded, workflow.Run(new CorrectionRequest { ProcessName = "OUTLOOK.EXE", ExcludedProcesses = new[] { "outlook" } }).Status);
        Assert.Equal(WorkflowStatus.Elevated, workflow.Run(new CorrectionRequest { Elevated = true }).Status);
        Assert.Equal(WorkflowStatus.PasswordField, workflow.Run(new CorrectionRequest { PasswordField = true }).Status);
        Assert.Equal(0, transport.Copies);
        Assert.Equal(0, engine.Calls);
        Assert.Equal(before, clip.SequenceNumber);
    }

    [Fact]
    public void VeryLongTextIsRefusedBeforeTheEngineRuns()
    {
        var (clip, transport, engine, workflow) = Harness();
        transport.Selection = new string('a', 50_001);
        var result = workflow.Run(new CorrectionRequest { MaxCharacters = 50_000, CopyWaitMilliseconds = 0, PasteSettleMilliseconds = 0 });
        Assert.Equal(WorkflowStatus.TooLong, result.Status);
        Assert.Equal(0, engine.Calls);
        Assert.Empty(transport.Pasted);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void BusyClipboardDoesNotCopy()
    {
        var (clip, transport, _, workflow) = Harness();
        clip.FailOpenTimes = 100;
        transport.Selection = "teh";
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.ClipboardBusy, result.Status);
        Assert.Equal(0, transport.Copies);
    }

    [Fact]
    public void EngineFailureRestoresTheClipboardAndDoesNotPaste()
    {
        var (clip, transport, engine, workflow) = Harness();
        transport.Selection = "teh cat";
        engine.Handle = _ => throw new LocalEngineException("The local model could not be reached. Nothing was changed.");
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.Failed, result.Status);
        Assert.Contains("local model", result.Message, StringComparison.Ordinal);
        Assert.Empty(transport.Pasted);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void AcceptedPreviewPastes()
    {
        var (_, transport, _, workflow) = Harness();
        transport.Selection = "teh cat";
        string? seen = null;
        var result = workflow.Run(Request(preview: true), (original, corrected, edits) =>
        {
            seen = original + "=>" + corrected;
            Assert.NotEmpty(edits);
            return true;
        });
        Assert.Equal(WorkflowStatus.Replaced, result.Status);
        Assert.Equal("teh cat=>the cat", seen);
        Assert.Equal(new[] { "the cat" }, transport.Pasted);
    }
}
