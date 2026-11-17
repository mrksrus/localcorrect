using System.Diagnostics;

namespace LocalCorrect.App;

/// <summary>
/// The shared look of the windows. Everything is the system font and system colours, and every distance is
/// measured in that font (<see cref="Em"/>), so the windows follow the display scaling and high contrast.
/// </summary>
internal static class Ui
{
    /// <summary>For the screenshot test only: draws the windows as at a larger display scaling. 1 in the app.</summary>
    internal static float Scale = 1f;
    /// <summary>For the screenshot test only: uses the marks of high-contrast mode.</summary>
    internal static bool ForcePlainMarks;

    static readonly Dictionary<(float, FontStyle), Font> Fonts = new();

    /// <summary>The system font at a multiple of its size. Cached and never disposed: there are five of them.</summary>
    public static Font Font(float factor = 1f, FontStyle style = FontStyle.Regular)
    {
        var key = (factor * Scale, style);
        if (Fonts.TryGetValue(key, out var font)) return font;
        var system = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        return Fonts[key] = new Font(system.FontFamily, system.SizeInPoints * key.Item1, style, GraphicsUnit.Point);
    }

    /// <summary>The height of a line of the system font in pixels: 15 at 100 %, 23 at 150 %.</summary>
    public static int Em => Font().Height;

    public const float Heading = 1.35f;
    /// <summary>The corrected text is what the preview is for, so it is set a little larger than the labels.</summary>
    public const float Reading = 1.2f;

    // Removed text is struck through and inserted text underlined in every mode; the colours only add to that.
    // With high contrast or a dark window colour, only system colours are used and inserted text is bold as well.
    public static bool PlainMarks => ForcePlainMarks || SystemInformation.HighContrast || SystemColors.Window.GetBrightness() < 0.5f;
    public static Color RemovedText => PlainMarks ? SystemColors.GrayText : Color.FromArgb(0xA4, 0x26, 0x2C);
    public static Color InsertedText => PlainMarks ? SystemColors.WindowText : Color.FromArgb(0x0B, 0x5A, 0x0B);
    public static Color InsertedBack => PlainMarks ? SystemColors.Window : Color.FromArgb(0xE2, 0xF3, 0xE0);
    public static FontStyle InsertedStyle => PlainMarks ? FontStyle.Underline | FontStyle.Bold : FontStyle.Underline;

    public static Label Label(string text, float size = 1f, FontStyle style = FontStyle.Regular, bool muted = false, int wrapAt = 0) => new()
    {
        Text = text,
        AutoSize = true,
        UseMnemonic = false,
        Font = Font(size, style),
        ForeColor = muted ? SystemColors.GrayText : SystemColors.ControlText,
        MaximumSize = new Size(wrapAt, 0),
        Margin = Padding.Empty
    };

    public static Button Button(string text) => new()
    {
        Text = text,
        Font = Font(),
        AutoSize = true,
        MinimumSize = new Size(Em * 6, Em * 7 / 4),
        Padding = new Padding(Em / 2, 0, Em / 2, 0),
        Margin = new Padding(Em / 2, 0, 0, 0)
    };

    /// <summary>A row of buttons at the right edge, in the order given.</summary>
    public static FlowLayoutPanel Buttons(params Button[] buttons)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        row.Controls.AddRange(buttons);
        return row;
    }

    /// <summary>Opens THIRD_PARTY.md in Notepad and the folder with the licence texts in Explorer.</summary>
    public static void OpenLicences(IWin32Window? owner = null)
    {
        var notices = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY.md");
        var folder = Path.Combine(AppContext.BaseDirectory, "licenses");
        try
        {
            if (!File.Exists(notices) || !Directory.Exists(folder)) throw new FileNotFoundException();
            Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { notices } })?.Dispose();
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
            MessageBox.Show(owner, "The licence files could not be opened. THIRD_PARTY.md and the licenses folder belong next to LocalCorrect.exe and are part of the release zip.",
                "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
