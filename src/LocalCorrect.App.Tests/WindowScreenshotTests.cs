using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using LocalCorrect.Core;
using LocalCorrect.Engine;
using Xunit;

namespace LocalCorrect.App.Tests;

/// <summary>
/// Opens the preview and the settings window with synthetic text, checks that no control or label is cut off,
/// and saves a picture of each from the screen. CI uploads the pictures as the artifact "ui-screenshots", so the
/// windows can be looked at without a Windows machine. 150 % is simulated with a 1.5 times larger system font:
/// everything in the windows is measured in that font, but the title bar and check marks stay at the runner's 100 %.
/// </summary>
public class WindowScreenshotTests
{
    const string German = "Hallo zusammen, ich habe heute keine zeit, aber morgen könen wir uns treffen. Aufjedenfall melde ich mich bei Herrn Müller 😀. "
        + "Ich glaube das wir das Meeting bald abschliesen. Ich weis nicht ob das klappt.";
    const string English = "i will recieve the parcel tommorow and we have alot of work. Its a nice day, so lets meet at the office.";

    [Theory]
    [InlineData(1f, false, "100")]
    [InlineData(1.5f, false, "150-simulated")]
    [InlineData(1f, true, "100-high-contrast-marks")]
    public void WindowsShowEverythingAndAreCaptured(float scale, bool plainMarks, string tag) => OnStaThread(() =>
    {
        Application.EnableVisualStyles(); // as the app does at start; without it the controls are drawn in the Windows 95 style
        Ui.Scale = scale;
        Ui.ForcePlainMarks = plainMarks;
        try
        {
            var pairs = new[] { ("zeit", "Zeit"), ("könen", "können"), ("Aufjedenfall", "Auf jeden Fall"), ("das wir", "dass wir"), ("abschliesen", "abschließen"), ("weis", "weiß"), ("nicht", "nicht,") };
            var german = Edits(German, pairs);
            Capture(new PreviewForm(German, CorrectionEdit.Apply(German, german), german,
                new PreviewDetails("de", LanguageToolEngine.EngineId, null, Paragraph: true)), "preview-german-paragraph-" + tag);
            if (plainMarks) return; // the marks are the only difference

            var english = Edits(English, ("i", "I"), ("recieve", "receive"), ("tommorow", "tomorrow"));
            Capture(new PreviewForm(English, CorrectionEdit.Apply(English, english), english,
                new PreviewDetails("en", SymSpellCorrector.EngineId, LanguageToolEngine.StartingNotice)), "preview-english-fallback-notice-" + tag);

            // Three paragraphs: taller than the window starts, shorter than the screen allows, so it must grow and not scroll.
            var medium = string.Join("\r\n\r\n", Enumerable.Repeat(German, 3));
            var mediumEdits = Edits(medium, Enumerable.Repeat(pairs, 3).SelectMany(p => p).ToArray());
            Capture(new PreviewForm(medium, CorrectionEdit.Apply(medium, mediumEdits), mediumEdits,
                new PreviewDetails("de", LanguageToolEngine.EngineId)), "preview-three-paragraphs-" + tag);

            // Forty paragraphs with the changes in the middle, as one whole-text edit the way the local model reports it.
            var clean = CorrectionEdit.Apply(English, Edits(English, ("i", "I"), ("recieve", "receive"), ("tommorow", "tomorrow"), ("alot", "a lot"), ("Its", "It's"), ("lets", "let's")));
            var paragraphs = Enumerable.Repeat(clean, 40).ToArray();
            var corrected = string.Join("\r\n", paragraphs);
            paragraphs[18] = English;
            paragraphs[19] = "Then we could " + English;
            var longText = string.Join("\r\n", paragraphs);
            Capture(new PreviewForm(longText, corrected, new[] { new CorrectionEdit(0, longText.Length, longText, corrected) },
                new PreviewDetails("en", OllamaCorrectionEngine.EngineId)), "preview-long-text-" + tag);

            var settings = new AppSettings();
            settings.ExcludedProcesses.AddRange(new[] { "keepass", "banking" });
            Capture(new SettingsForm(settings, "LanguageTool is ready"), "settings-" + tag);
        }
        finally
        {
            Ui.Scale = 1f;
            Ui.ForcePlainMarks = false;
        }
    });

    [Fact]
    public void SettingsScrollWhenTheyDoNotFitAboveTheTaskbar() => OnStaThread(() =>
    {
        Application.EnableVisualStyles();
        Ui.Scale = 2f; // taller than the 1080-pixel runner screen
        try
        {
            using var form = new SettingsForm(new AppSettings(), "LanguageTool is ready");
            Capture(form, "settings-200-simulated-scrolls", () =>
            {
                Assert.True(form.AutoScroll, "the window should scroll instead of growing past the screen");
                Assert.True(form.Height == Screen.FromControl(form).WorkingArea.Height, "the window " + form.Bounds + " should use the whole height above the taskbar");
                // The client width does not count the scroll bar at the right edge.
                Assert.True(form.ClientSize.Width >= form.Controls[0].Width, "both columns (" + form.Controls[0].Width + " wide) should show without scrolling sideways in " + form.ClientSize);
            });
        }
        finally
        {
            Ui.Scale = 1f;
        }
    });

    /// <summary>One edit per pair, at the first whole-word place after the previous one.</summary>
    static CorrectionEdit[] Edits(string text, params (string Wrong, string Right)[] pairs)
    {
        var edits = new List<CorrectionEdit>();
        var from = 0;
        foreach (var (wrong, right) in pairs)
        {
            var at = text.IndexOf(wrong, from, StringComparison.Ordinal);
            while (at > 0 && char.IsLetter(text[at - 1])) at = text.IndexOf(wrong, at + 1, StringComparison.Ordinal);
            Assert.True(at >= 0, wrong);
            edits.Add(new CorrectionEdit(at, wrong.Length, wrong, right));
            from = at + wrong.Length;
        }
        return edits.ToArray();
    }

    static void Capture(Form form, string name, Action? check = null)
    {
        using (form)
        {
            form.TopMost = true;
            form.Show();
            form.Activate();
            // Let the window paint and its opening animation end.
            for (var until = Environment.TickCount64 + 800; Environment.TickCount64 < until; Thread.Sleep(20)) Application.DoEvents();

            var screen = Screen.FromControl(form).Bounds;
            var area = Screen.FromControl(form).WorkingArea;
            Assert.True(area.Contains(form.Bounds), name + ": the window " + form.Bounds + " is not inside the space above the taskbar " + area);
            AssertNothingIsCut(form, name);
            check?.Invoke();

            var directory = Environment.GetEnvironmentVariable("LOCALCORRECT_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(directory)) directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
            Directory.CreateDirectory(directory);
            var bounds = Rectangle.Intersect(form.Bounds, screen);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
            form.Close();
        }
    }

    /// <summary>Every visible control lies inside its parent, no label, button, check box or list entry is wider than its control, and every field has a name.</summary>
    static void AssertNothingIsCut(Control parent, string window)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            var what = window + ": " + child.GetType().Name + " \"" + child.Text + "\" " + child.Bounds;
            // A scrolling window may hold more than it shows.
            if (parent is not ScrollableControl { AutoScroll: true })
                Assert.True(parent.ClientRectangle.Contains(child.Bounds), what + " does not fit in its " + parent.GetType().Name + " " + parent.ClientSize);
            if (child is Label or ButtonBase)
                Assert.True(child.PreferredSize.Width <= child.Width && child.PreferredSize.Height <= child.Height, what + " needs " + child.PreferredSize);
            if (child is ComboBox or TextBoxBase)
                Assert.False(string.IsNullOrEmpty(child.AccessibleName), what + " has no name for a screen reader");
            if (child is ComboBox combo)
                foreach (var item in combo.Items)
                    Assert.True(TextRenderer.MeasureText(item.ToString(), combo.Font).Width + SystemInformation.VerticalScrollBarWidth <= combo.Width, what + " cuts \"" + item + "\"");
            AssertNothingIsCut(child, window);
        }
    }

    static void OnStaThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
