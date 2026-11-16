using LocalCorrect.Core;

namespace LocalCorrect.App;

internal sealed class PreviewForm : Form
{
    public PreviewForm(string original, string corrected, IReadOnlyList<CorrectionEdit> edits)
    {
        Text = "LocalCorrect";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Font = SystemFonts.MessageBoxFont;
        ClientSize = new Size(440, 360);

        var title = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(12, 12, 12, 0),
            Text = edits.Count == 1 ? "1 change. Replace the selection?" : edits.Count + " changes. Replace the selection?"
        };
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        var shown = Math.Min(edits.Count, 40);
        for (var i = 0; i < shown; i++)
            list.Items.Add(Clip(edits[i].Original) + "  →  " + Clip(edits[i].Replacement));
        if (edits.Count > shown) list.Items.Add("… and " + (edits.Count - shown) + " more");

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(12, 8, 12, 8)
        };
        var replace = new Button { Text = "Replace", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(replace);
        buttons.Controls.Add(cancel);
        AcceptButton = replace;
        CancelButton = cancel;

        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            Padding = new Padding(12, 0, 12, 0),
            Text = original.Length > 1800 || corrected.Length > 1800
                ? "Long selection. The list above is the part that would change."
                : "Text stays on this computer."
        };

        Controls.Add(title);
        Controls.Add(buttons);
        Controls.Add(note);
        Controls.Add(list);
        Load += (_, _) => PlaceNearCursor();
    }

    public static bool Ask(string original, string corrected, IReadOnlyList<CorrectionEdit> edits)
    {
        using var form = new PreviewForm(original, corrected, edits);
        return form.ShowDialog() == DialogResult.OK;
    }

    void PlaceNearCursor()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var x = Cursor.Position.X + 16;
        var y = Cursor.Position.Y + 16;
        if (x + Width > area.Right) x = area.Right - Width;
        if (y + Height > area.Bottom) y = area.Bottom - Height;
        if (x < area.Left) x = area.Left;
        if (y < area.Top) y = area.Top;
        Location = new Point(x, y);
    }

    static string Clip(string value)
    {
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= 80 ? value : value.Substring(0, 77) + "...";
    }
}
