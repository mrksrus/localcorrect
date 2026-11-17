using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.App;

/// <summary>What the preview says about a correction besides the text itself.</summary>
internal sealed record PreviewDetails(string? Language = null, string? Engine = null, string? Notice = null, bool Paragraph = false);

/// <summary>
/// Shows the text as a marked-up proof: removed words struck through, their replacements underlined, in place.
/// Enter replaces, Esc cancels.
/// </summary>
internal sealed class PreviewForm : Form
{
    readonly IReadOnlyList<PreviewSegment> _segments;
    readonly RichTextBox _view;
    readonly Button _replace = Ui.Button("Replace");
    int _contentHeight;

    public PreviewForm(string original, string corrected, IReadOnlyList<CorrectionEdit> edits, PreviewDetails details)
    {
        _segments = PreviewSegments.Build(original, corrected, edits);
        var em = Ui.Em;
        var width = em * 38;

        Text = "LocalCorrect";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Font = Ui.Font();
        ClientSize = new Size(width, em * 16);
        MinimumSize = new Size(em * 26, em * 14);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(em) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var changes = PreviewSegments.CountChanges(_segments);
        var heading = Ui.Label((changes == 1 ? "1 change" : changes + " changes") + (details.Paragraph ? " in this paragraph" : " in the selection"), Ui.Heading);
        var source = Ui.Label(Source(details), muted: true);
        source.Margin = new Padding(0, em / 4, 0, em * 3 / 4);
        AddRow(root, heading);
        AddRow(root, source);

        if (!string.IsNullOrEmpty(details.Notice))
        {
            // The system's tooltip colours: readable in every theme, and the icon says "notice" without them.
            var notice = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Info,
                Padding = new Padding(em / 2),
                Margin = new Padding(0, 0, 0, em * 3 / 4)
            };
            using var icon = SystemIcons.GetStockIcon(StockIconId.Info, em);
            notice.Controls.Add(new PictureBox { Image = icon.ToBitmap(), SizeMode = PictureBoxSizeMode.AutoSize, Margin = new Padding(0, 0, em / 2, 0) }, 0, 0);
            var message = Ui.Label(details.Notice, wrapAt: width - em * 5);
            message.ForeColor = SystemColors.InfoText;
            notice.Controls.Add(message, 1, 0);
            AddRow(root, notice);
        }

        _view = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.WindowText,
            Font = Ui.Font(Ui.Reading),
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            AccessibleName = "Text with the changes marked",
            AccessibleDescription = Describe(_segments),
            TabIndex = 2
        };
        _view.ContentsResized += (_, e) => _contentHeight = e.NewRectangle.Height;
        var paper = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Window, Padding = new Padding(em * 3 / 4, em / 2, em / 4, em / 2) };
        paper.Controls.Add(_view);
        // A one-pixel frame in a system colour around the white "paper".
        var frame = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.ControlDark, Padding = new Padding(1), Margin = Padding.Empty, TabIndex = 2 };
        frame.Controls.Add(paper);
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(frame, 0, root.RowCount++);

        var removed = Ui.Label("removed", style: FontStyle.Strikeout);
        removed.ForeColor = Ui.RemovedText;
        removed.Margin = new Padding(0, 0, em, 0);
        var inserted = Ui.Label("added", style: Ui.InsertedStyle);
        inserted.ForeColor = Ui.InsertedText;
        inserted.BackColor = Ui.InsertedBack;
        var legend = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        legend.Controls.Add(removed);
        legend.Controls.Add(inserted);

        var cancel = Ui.Button("Cancel");
        _replace.DialogResult = DialogResult.OK;
        _replace.TabIndex = 0;
        cancel.DialogResult = DialogResult.Cancel;
        cancel.TabIndex = 1;
        AcceptButton = _replace;
        CancelButton = cancel;
        var footer = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Dock = DockStyle.Fill, Margin = new Padding(0, em * 3 / 4, 0, 0), TabIndex = 0 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(legend, 0, 0);
        footer.Controls.Add(Ui.Buttons(_replace, cancel), 1, 0);
        AddRow(root, footer);

        Controls.Add(root);
    }

    public static bool Ask(string original, string corrected, IReadOnlyList<CorrectionEdit> edits, PreviewDetails details)
    {
        using var form = new PreviewForm(original, corrected, edits, details);
        return form.ShowDialog() == DialogResult.OK;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var em = Ui.Em;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        // As tall as the text needs, up to 60 % of the screen; a longer text scrolls and starts at its first change.
        // The text is measured at the full height: in a lower window a scroll bar would narrow the lines and add some.
        Height = Math.Max(MinimumSize.Height, area.Height * 6 / 10);
        PerformLayout();
        var firstChange = Fill();
        _view.Select(0, 0);
        _view.ScrollToCaret(); // positions are measured from the top of what is shown
        var lastLine = _view.TextLength == 0 ? 0 : _view.GetPositionFromCharIndex(_view.TextLength - 1).Y;
        var text = Math.Max(_contentHeight, lastLine + _view.Font.Height * (_view.Text.EndsWith('\n') ? 2 : 1)) + em / 4;
        Height -= Math.Max(0, _view.Height - Math.Max(text, em * 3));
        _view.Select(firstChange, 0);
        _view.ScrollToCaret();
        PlaceNearCursor(area);
        ActiveControl = _replace;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // A rich text box keeps Enter for itself; in this window Enter means Replace wherever the focus is, except on Cancel.
        if (keyData == Keys.Enter && ActiveControl == _view)
        {
            DialogResult = DialogResult.OK;
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Writes the segments into the text box and returns where the first change starts.</summary>
    int Fill()
    {
        var struck = Ui.Font(Ui.Reading, FontStyle.Strikeout);
        var underlined = Ui.Font(Ui.Reading, Ui.InsertedStyle);
        var firstChange = -1;
        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            if (segment.Kind != SegmentKind.Same && firstChange < 0) firstChange = _view.TextLength;
            if (segment.Kind == SegmentKind.Removed) Append(segment.Text, struck, Ui.RemovedText, SystemColors.Window);
            else if (segment.Kind == SegmentKind.Same) Append(segment.Text, _view.Font, SystemColors.WindowText, SystemColors.Window);
            else
            {
                // A narrow space, so a struck word and its replacement do not read as one word.
                if (i > 0 && _segments[i - 1].Kind == SegmentKind.Removed) Append(" ", _view.Font, SystemColors.WindowText, SystemColors.Window);
                Append(segment.Text, underlined, Ui.InsertedText, Ui.InsertedBack);
            }
        }
        return Math.Max(firstChange, 0);
    }

    void Append(string text, Font font, Color color, Color back)
    {
        // Formatted after it is inserted: a rich text box counts a line break as one character, and the format of an empty selection does not always stick.
        var start = _view.TextLength;
        _view.AppendText(text);
        _view.Select(start, _view.TextLength - start);
        _view.SelectionFont = font;
        _view.SelectionColor = color;
        _view.SelectionBackColor = back;
    }

    void PlaceNearCursor(Rectangle area)
    {
        var x = Cursor.Position.X + 16;
        var y = Cursor.Position.Y + 16;
        if (x + Width > area.Right) x = area.Right - Width;
        if (y + Height > area.Bottom) y = area.Bottom - Height;
        if (x < area.Left) x = area.Left;
        if (y < area.Top) y = area.Top;
        Location = new Point(x, y);
    }

    static void AddRow(TableLayoutPanel table, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, table.RowCount++);
    }

    static string Source(PreviewDetails details)
    {
        var language = details.Language switch { "de" => "German", "en" => "English", _ => null };
        var engine = details.Engine switch
        {
            LanguageToolEngine.EngineId => "LanguageTool",
            SymSpellCorrector.EngineId => "the built-in word lists",
            OllamaCorrectionEngine.EngineId => "the local model",
            _ => null
        };
        if (engine == null) return language ?? "";
        return language == null ? "Checked with " + engine : language + ", checked with " + engine;
    }

    /// <summary>The changes in words, for a screen reader, which cannot see the marks.</summary>
    static string Describe(IReadOnlyList<PreviewSegment> segments)
    {
        var changes = new List<string>();
        for (var i = 0; i < segments.Count && changes.Count < 20; i++)
        {
            var (kind, text) = segments[i];
            if (kind == SegmentKind.Removed && i + 1 < segments.Count && segments[i + 1].Kind == SegmentKind.Inserted)
                changes.Add("\"" + text + "\" becomes \"" + segments[++i].Text + "\"");
            else if (kind != SegmentKind.Same)
                changes.Add("\"" + text + "\" " + (kind == SegmentKind.Removed ? "removed" : "added"));
        }
        return string.Join("; ", changes);
    }
}
