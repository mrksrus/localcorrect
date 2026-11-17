using LocalCorrect.App.Win32;
using LocalCorrect.Core;
using Xunit;

// The tests share the foreground window and the clipboard.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LocalCorrect.App.Tests;

/// <summary>
/// Sends the app's real keys (SendInput) to a WinForms TextBox and RichTextBox on this desktop and checks the text.
/// Windows only; needs an interactive desktop, which the CI runner has.
/// </summary>
public class RealKeysTests
{
    sealed class TehEngine : ICorrectionEngine
    {
        public string Id => "teh";
        public string DisplayName => "teh";
        public CorrectionResult Correct(string text, string language)
        {
            var next = text.Replace("teh", "the", StringComparison.Ordinal);
            return new CorrectionResult(next, "en", next == text ? Array.Empty<CorrectionEdit>() : new[] { new CorrectionEdit(0, text.Length, text, next) });
        }
    }

    [Theory]
    [InlineData(false, 18, 18, "First line.\r\nWe saw the cat.\r\nLast teh line.")]
    [InlineData(true, 18, 18, "First line.\nWe saw the cat.\nLast teh line.")]
    [InlineData(false, 13, 23, "First line.\r\nWe saw the cat.\r\nLast teh line.")]
    public void HotkeyCorrectsTheCurrentParagraph(bool rich, int selectionStart, int selectionEnd, string expected)
    {
        var (text, caret, focusClass) = RunInForm(rich, "First line.\r\nWe saw teh cat.\r\nLast teh line.", selectionStart, selectionEnd);
        Assert.Equal(expected, text);
        Assert.Equal(rich ? SelectKeys.Paragraph : SelectKeys.Line, NoSelectionPolicy.KeysFor(focusClass));
        Assert.True(caret > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnchangedParagraphIsCollapsedAtItsEnd(bool rich)
    {
        var original = "First line.\r\nNo typo here.\r\nLast line.";
        var (text, caret, _) = RunInForm(rich, original, 16, 16);
        Assert.Equal(rich ? original.Replace("\r\n", "\n") : original, text);
        Assert.Equal(rich ? 25 : 26, caret); // end of "No typo here.", before its break
    }

    static (string Text, int Caret, string FocusClass) RunInForm(bool rich, string content, int selectionStart, int selectionEnd)
    {
        using var shown = new ManualResetEventSlim();
        Form? form = null;
        TextBoxBase? box = null;
        var thread = new Thread(() =>
        {
            box = rich ? new RichTextBox { Dock = DockStyle.Fill } : new TextBox { Multiline = true, Dock = DockStyle.Fill };
            form = new Form { Width = 500, Height = 300, StartPosition = FormStartPosition.CenterScreen, TopMost = true };
            form.Controls.Add(box);
            form.Shown += (_, _) =>
            {
                box.Text = content;
                box.Select(selectionStart, selectionEnd - selectionStart);
                box.Focus();
                shown.Set();
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(shown.Wait(10_000), "The test window did not open.");
        try
        {
            var hwnd = (nint)form!.Invoke(() => form.Handle);
            WindowFocus.TryRestore(hwnd);
            Thread.Sleep(300);
            var target = ForegroundProbe.Capture();
            Assert.True(target.Handle == hwnd, "The test window is not in the foreground; the runner does not deliver input to it.");
            var clipboard = new Win32Clipboard { Owner = hwnd };
            var transport = new Win32SelectionTransport { Target = hwnd };
            var result = new CorrectionWorkflow(clipboard, transport, new TehEngine()).Run(new CorrectionRequest
            {
                ProcessName = target.ProcessName,
                WindowClass = target.FocusClass,
                HtmlFormat = Win32Clipboard.HtmlFormat,
                Preview = false,
                Language = "en"
            });
            Thread.Sleep(300);
            var state = ((string, int))form.Invoke(() => (box!.Text, box.SelectionStart + box.SelectionLength));
            Assert.True(result.Status is WorkflowStatus.Replaced or WorkflowStatus.Unchanged, result.Status + ": " + result.Message + " class=" + target.FocusClass);
            return (state.Item1, state.Item2, target.FocusClass);
        }
        finally
        {
            form?.Invoke(() => form.Close());
            thread.Join(5000);
        }
    }
}
