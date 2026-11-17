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

    const uint HtmlFormat = 0xC0F0;

    /// <summary>
    /// An editor with text, a selection and a clock. Keys are handled in order, each when the previous one is done,
    /// the way a real program drains its input queue; the workflow's sleeps move the clock.
    /// </summary>
    sealed class FakeEditor : ISelectionTransport
    {
        readonly Queue<(long Due, Action Act)> _queue = new();
        long _now, _busyUntil;

        public FakeEditor(MemoryClipboard clipboard) => Clipboard = clipboard;
        public MemoryClipboard Clipboard { get; }
        public string Text { get; set; } = "";
        public int Anchor { get; set; }
        public int Caret { get; set; }
        public string? Html { get; set; }
        /// <summary>How long the first Ctrl+C takes to land; later copies take <see cref="CopyCost"/>.</summary>
        public int FirstCopyDelay { get; set; }
        public int CopyCost { get; set; }
        /// <summary>Like a web password box: Ctrl+C copies nothing even with a selection.</summary>
        public bool RefuseCopy { get; set; }
        public int Copies { get; private set; }
        public List<string> Pasted { get; } = new();
        public List<SelectKeys> Keys { get; } = new();

        public string? Selection
        {
            set
            {
                Text = value ?? "";
                Anchor = 0;
                Caret = Text.Length;
            }
        }

        public string Selected => Text[Math.Min(Anchor, Caret)..Math.Max(Anchor, Caret)];

        public void Sleep(int milliseconds)
        {
            _now += milliseconds;
            Drain();
        }

        public void Copy()
        {
            Copies++;
            Enqueue(Copies == 1 ? FirstCopyDelay : CopyCost, () =>
            {
                if (Selected.Length == 0 || RefuseCopy) return;
                var entries = new List<ClipboardEntry> { new(ClipboardFormatPolicy.UnicodeText, ClipboardText.Encode(Selected)) };
                if (Html != null) entries.Add(new ClipboardEntry(HtmlFormat, System.Text.Encoding.UTF8.GetBytes(Html)));
                Assert.True(Clipboard.TryOpen());
                Clipboard.WriteAll(entries);
                Clipboard.Close();
            });
        }

        public void Paste() => Enqueue(0, () =>
        {
            var text = ClipboardSession.ReadUnicode(Clipboard, attempts: 1, wait: NoWait) ?? "";
            Pasted.Add(text);
            var start = Math.Min(Anchor, Caret);
            Text = Text[..start] + text + Text[Math.Max(Anchor, Caret)..];
            Anchor = Caret = start + text.Length;
        });

        public void Select(SelectKeys keys) => Enqueue(0, () =>
        {
            Keys.Add(keys);
            switch (keys)
            {
                case SelectKeys.Paragraph:
                    Anchor = Caret = NextParagraph(Caret); // Ctrl+Down
                    Anchor = Caret = Caret == 0 ? 0 : ParagraphStart(Text[Caret - 1] == '\n' ? Caret - 1 : Caret); // Ctrl+Up
                    Caret = NextParagraph(Caret); // Ctrl+Shift+Down
                    break;
                case SelectKeys.Line:
                    Anchor = Caret = ParagraphStart(Caret); // Home
                    var end = Text.IndexOfAny(new[] { '\r', '\n' }, Caret); // Shift+End
                    Caret = end < 0 ? Text.Length : end;
                    break;
                case SelectKeys.DropLast:
                    Caret -= Caret >= 2 && Text[(Caret - 2)..Caret] == "\r\n" ? 2 : 1;
                    break;
                default:
                    var lineEnd = Text.IndexOfAny(new[] { '\r', '\n' }, Caret); // End
                    Anchor = Caret = lineEnd < 0 ? Text.Length : lineEnd;
                    break;
            }
        });

        int NextParagraph(int at)
        {
            var i = Text.IndexOf('\n', at);
            return i < 0 ? Text.Length : i + 1;
        }

        int ParagraphStart(int at) => at <= 0 ? 0 : Text.LastIndexOf('\n', at - 1) + 1;

        void Enqueue(int cost, Action act)
        {
            _busyUntil = Math.Max(_now, _busyUntil) + cost;
            _queue.Enqueue((_busyUntil, act));
            Drain();
        }

        void Drain()
        {
            while (_queue.Count > 0 && _queue.Peek().Due <= _now) _queue.Dequeue().Act();
        }
    }

    static (MemoryClipboard Clip, FakeEditor Transport, FakeEngine Engine, CorrectionWorkflow Workflow) Harness()
    {
        var clip = new MemoryClipboard();
        var custom = new byte[] { 1, 2, 3, 0, 9 };
        ClipboardSession.WriteText(clip, "secret", attempts: 1, wait: NoWait);
        var seeded = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        ClipboardSession.Restore(clip, new ClipboardArchive(seeded.Entries.Append(new ClipboardEntry(0xC100, custom)).ToList()), attempts: 1, wait: NoWait);
        var transport = new FakeEditor(clip);
        var engine = new FakeEngine();
        var workflow = new CorrectionWorkflow(clip, transport, engine, transport.Sleep);
        return (clip, transport, engine, workflow);
    }

    static CorrectionRequest Request(bool preview = false, bool paragraph = true, string? windowClass = "_WwG", string? process = null) => new()
    {
        Preview = preview,
        PasteSettleMilliseconds = 0,
        Language = "en",
        CorrectParagraph = paragraph,
        WindowClass = windowClass,
        HtmlFormat = HtmlFormat,
        ProcessName = process
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
        var result = workflow.Run(Request(paragraph: false));
        Assert.Equal(WorkflowStatus.NoSelection, result.Status);
        Assert.Equal(0, engine.Calls);
        Assert.Empty(transport.Pasted);
        Assert.Empty(transport.Keys);
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

    // ---- Nothing selected: the current paragraph (W-05, W-09) ----

    const string Document = "First line.\r\nWe saw teh cat.\r\nLast teh line";

    static FakeEditor Caret(FakeEditor editor, string text, int caret)
    {
        editor.Text = text;
        editor.Anchor = editor.Caret = caret;
        return editor;
    }

    [Fact]
    public void SelectionStillWinsOverTheParagraph()
    {
        var (clip, editor, _, workflow) = Harness();
        editor.Text = Document;
        editor.Anchor = 13;
        editor.Caret = 23; // "We saw teh"
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.Replaced, result.Status);
        Assert.Empty(editor.Keys);
        Assert.Equal("First line.\r\nWe saw the cat.\r\nLast teh line", editor.Text);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void ParagraphIsTakenWithoutItsBreak()
    {
        var (clip, editor, engine, workflow) = Harness();
        Caret(editor, Document, 18);
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.Replaced, result.Status);
        Assert.Contains("paragraph", result.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { SelectKeys.Paragraph, SelectKeys.DropLast }, editor.Keys);
        Assert.Equal(new[] { "We saw the cat." }, editor.Pasted);
        Assert.Equal("First line.\r\nWe saw the cat.\r\nLast teh line", editor.Text);
        Assert.Equal(1, engine.Calls);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void CaretAtTheStartOfTheLastParagraph()
    {
        var (clip, editor, _, workflow) = Harness();
        Caret(editor, Document, 30);
        Assert.Equal(WorkflowStatus.Replaced, workflow.Run(Request()).Status);
        Assert.Equal(new[] { SelectKeys.Paragraph }, editor.Keys);
        Assert.Equal("First line.\r\nWe saw teh cat.\r\nLast the line", editor.Text);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void ClassicEditBoxTakesTheLine()
    {
        var (clip, editor, _, workflow) = Harness();
        Caret(editor, "teh one-line field", 5);
        Assert.Equal(WorkflowStatus.Replaced, workflow.Run(Request(windowClass: "Edit")).Status);
        Assert.Equal(new[] { SelectKeys.Line }, editor.Keys);
        Assert.Equal("the one-line field", editor.Text);
        AssertClipboardRestored(clip);
    }

    [Theory]
    [InlineData("A teh\vline break", null)]
    [InlineData("A teh\tcell", null)]
    [InlineData("A teh\acell mark", null)]
    [InlineData("A teh\fpage", null)]
    [InlineData("A teh link", "<html><body><!--StartFragment-->A teh <a href=\"https://example.com\">link</a><!--EndFragment--></body></html>")]
    [InlineData("A teh bold", "<html><body><!--StartFragment-->A teh <B>bold</B><!--EndFragment--></body></html>")]
    [InlineData("LONG", null)]
    public void ParagraphThatIsNotPlainTextIsLeftAlone(string paragraph, string? html)
    {
        var (clip, editor, engine, workflow) = Harness();
        if (paragraph == "LONG") paragraph = "teh " + new string('a', NoSelectionPolicy.MaxCharacters);
        var text = "Before\r\n" + paragraph + "\r\nAfter";
        Caret(editor, text, 10);
        editor.Html = html;
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.ParagraphSkipped, result.Status);
        Assert.Contains("Select the words", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, engine.Calls);
        Assert.Empty(editor.Pasted);
        Assert.Equal(text, editor.Text);
        Assert.Equal(SelectKeys.Collapse, editor.Keys[^1]);
        Assert.Equal("", editor.Selected);
        Assert.Equal(8 + paragraph.Length, editor.Caret); // end of the paragraph, before its break
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void PlainHtmlWithoutFormattingIsReplaced()
    {
        var (clip, editor, _, workflow) = Harness();
        Caret(editor, Document, 18);
        editor.Html = "Version:0.9\r\n<html><head><style>a:link{color:blue}</style></head><body><p class=MsoNormal><span lang=EN-US>We saw teh cat.<o:p></o:p></span></p></body></html>";
        Assert.Equal(WorkflowStatus.Replaced, workflow.Run(Request()).Status);
        Assert.Equal("First line.\r\nWe saw the cat.\r\nLast teh line", editor.Text);
        AssertClipboardRestored(clip);
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("declined")]
    [InlineData("failed")]
    public void SelectedParagraphIsCollapsedWhenNothingIsPasted(string outcome)
    {
        var (clip, editor, engine, workflow) = Harness();
        var text = outcome == "unchanged" ? "One.\r\nNo typos here.\r\nTwo." : "One.\r\nA teh typo.\r\nTwo.";
        Caret(editor, text, 8);
        if (outcome == "failed") engine.Handle = _ => throw new LocalEngineException("Engine down. Nothing was changed.");
        var result = workflow.Run(Request(preview: outcome == "declined"), (_, _, _) => false);
        var expected = outcome switch { "unchanged" => WorkflowStatus.Unchanged, "declined" => WorkflowStatus.PreviewDeclined, _ => WorkflowStatus.Failed };
        Assert.Equal(expected, result.Status);
        Assert.Empty(editor.Pasted);
        Assert.Equal(text, editor.Text);
        Assert.Equal(new[] { SelectKeys.Paragraph, SelectKeys.DropLast, SelectKeys.Collapse }, editor.Keys);
        Assert.Equal("", editor.Selected);
        Assert.Equal(text.IndexOf("\r\nTwo", StringComparison.Ordinal), editor.Caret);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void SelectionIsCollapsedWhenTheParagraphCopyBringsNothing()
    {
        var (clip, editor, engine, workflow) = Harness();
        Caret(editor, "hunter2 is secret", 3);
        editor.RefuseCopy = true;
        var result = workflow.Run(Request(windowClass: "Chrome_RenderWidgetHostHWND"));
        Assert.Equal(WorkflowStatus.NoSelection, result.Status);
        Assert.Equal(SelectKeys.Collapse, editor.Keys[^1]);
        Assert.Equal("", editor.Selected);
        Assert.Equal(0, engine.Calls);
        AssertClipboardRestored(clip);
    }

    [Theory]
    [InlineData(350)]
    [InlineData(400)]
    [InlineData(800)]
    [InlineData(1100)]
    public void SlowCopyOfASelectionIsNotTakenForTheParagraph(int copyCost)
    {
        var (clip, editor, _, workflow) = Harness();
        // Every Ctrl+C in this program is slow. The user's selection must be what gets corrected and replaced.
        editor.Text = "Alpha teh beta gamma.\r\nNext";
        editor.Anchor = 0;
        editor.Caret = 9;
        editor.FirstCopyDelay = editor.CopyCost = copyCost;
        var result = workflow.Run(Request(preview: true), (original, _, _) => original == "Alpha teh");
        Assert.Equal(WorkflowStatus.Replaced, result.Status);
        Assert.Empty(editor.Keys);
        Assert.Equal("Alpha the beta gamma.\r\nNext", editor.Text);
        AssertClipboardRestored(clip);
    }

    [Fact]
    public void SettingOffLeavesTheParagraphAlone()
    {
        var (clip, editor, engine, workflow) = Harness();
        Caret(editor, Document, 18);
        var before = clip.SequenceNumber;
        var result = workflow.Run(Request(paragraph: false));
        Assert.Equal(WorkflowStatus.NoSelection, result.Status);
        Assert.Empty(editor.Keys);
        Assert.Equal(0, engine.Calls);
        Assert.Equal(Document, editor.Text);
        Assert.Equal(before, clip.SequenceNumber);
    }

    [Fact]
    public void EmptyParagraphSelectsNothingAndDoesNotMoveOn()
    {
        var (clip, editor, engine, workflow) = Harness();
        Caret(editor, "One.\r\n\r\nTwo.", 6);
        var result = workflow.Run(Request());
        Assert.Equal(WorkflowStatus.NoSelection, result.Status);
        Assert.Equal(new[] { SelectKeys.Paragraph, SelectKeys.DropLast }, editor.Keys);
        Assert.Equal(6, editor.Caret);
        Assert.Equal(0, engine.Calls);
        AssertClipboardRestored(clip);
    }

    [Theory]
    [InlineData("WindowsTerminal")]
    [InlineData("cmd.exe")]
    [InlineData("C:\\Program Files\\Microsoft VS Code\\Code.exe")]
    [InlineData("EXCEL")]
    [InlineData("notepad++")]
    public void TerminalsCodeEditorsAndExcelGetNoKeys(string process)
    {
        var (clip, editor, engine, workflow) = Harness();
        Caret(editor, Document, 18);
        var before = clip.SequenceNumber;
        var result = workflow.Run(Request(process: process));
        Assert.Equal(WorkflowStatus.Excluded, result.Status);
        Assert.Equal(0, editor.Copies);
        Assert.Empty(editor.Keys);
        Assert.Equal(0, engine.Calls);
        Assert.Equal(before, clip.SequenceNumber);
    }

    [Fact]
    public void NotepadIsNotOnTheDoNothingList()
    {
        Assert.False(ProcessFilter.IsExcluded("notepad", ProcessFilter.NeverCorrect));
        Assert.False(ProcessFilter.IsExcluded("OUTLOOK", ProcessFilter.NeverCorrect));
    }
}
