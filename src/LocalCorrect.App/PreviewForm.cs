using LocalCorrect.App.Win32;
using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.App;

/// <summary>What the preview says about a correction besides the text itself.</summary>
internal sealed record PreviewDetails(string? Language = null, string? Engine = null, string? Notice = null, bool Paragraph = false);

/// <summary>
/// Shows the text as a marked-up proof: removed words struck through, their replacements underlined, in place.
/// Words the engine is unsure about are blue with a wavy underline; a click (or F8) offers the suggestions (W-11).
/// Enter replaces, Esc cancels.
/// </summary>
internal sealed class PreviewForm : Form
{
    readonly string _original, _corrected;
    readonly IReadOnlyList<CorrectionEdit> _edits;
    readonly IReadOnlyList<CorrectionDoubt> _doubts;
    readonly string?[] _choices;
    readonly bool _paragraph;
    readonly Label _heading;
    readonly RichTextBox _view;
    readonly Button _replace = Ui.Button("Replace");
    readonly ContextMenuStrip _menu = new() { ShowImageMargin = false, ShowCheckMargin = true };
    /// <summary>Where each doubt is in the text box, a chosen one with its replacement.</summary>
    readonly List<(int Start, int End, int Doubt)> _marks = new();
    IReadOnlyList<PreviewSegment> _segments = Array.Empty<PreviewSegment>();
    int _menuDoubt = -1;
    int _contentHeight;

    public PreviewForm(string original, string corrected, IReadOnlyList<CorrectionEdit> edits, PreviewDetails details,
        IReadOnlyList<CorrectionDoubt>? doubts = null)
    {
        _original = original;
        _corrected = corrected;
        _edits = edits;
        _doubts = doubts ?? Array.Empty<CorrectionDoubt>();
        _choices = new string?[_doubts.Count];
        _paragraph = details.Paragraph;
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

        _heading = Ui.Label("", Ui.Heading);
        var source = Ui.Label(Source(details), muted: true);
        source.Margin = new Padding(0, em / 4, 0, em * 3 / 4);
        AddRow(root, _heading);
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
            TabIndex = 2
        };
        _view.ContentsResized += (_, e) => _contentHeight = e.NewRectangle.Height;
        _view.MouseMove += (_, e) => _view.Cursor = DoubtAt(e.Location) >= 0 ? Cursors.Hand : Cursors.IBeam;
        _view.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && _view.SelectionLength == 0 && DoubtAt(e.Location) is var doubt and >= 0) OpenMenu(doubt);
        };
        _menu.Font = Ui.Font();
        _menu.ItemClicked += (_, e) =>
        {
            if (_menuDoubt < 0 || e.ClickedItem?.Tag is not int pick) return;
            _choices[_menuDoubt] = pick < 0 ? null : _doubts[_menuDoubt].Suggestions[pick];
            // After the menu has closed, so the text box is redrawn without it.
            var doubt = _menuDoubt;
            BeginInvoke(() => Redraw(doubt));
        };
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
        if (_doubts.Count > 0)
        {
            var unsure = Ui.Label("unsure \u2013 click to choose", style: FontStyle.Underline);
            unsure.ForeColor = Ui.UnsureText;
            unsure.BackColor = Ui.UnsureBack;
            inserted.Margin = new Padding(0, 0, em, 0);
            legend.Controls.Add(unsure);
        }

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
        ShowChoices();
    }

    /// <summary>The text to paste: the corrections plus the suggestions picked so far.</summary>
    public string Result => PreviewSegments.Compose(_original, _corrected, _edits, _doubts, _choices);

    /// <summary>The text to paste, or null when the user cancels.</summary>
    public static string? Ask(string original, string corrected, IReadOnlyList<CorrectionEdit> edits, PreviewDetails details,
        IReadOnlyList<CorrectionDoubt>? doubts = null)
    {
        using var form = new PreviewForm(original, corrected, edits, details, doubts);
        return form.ShowDialog() == DialogResult.OK ? form.Result : null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _menu.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Rebuilds the pieces from the choices and updates the heading, the description and Replace.</summary>
    void ShowChoices()
    {
        _segments = PreviewSegments.Build(_original, _corrected, _edits, _doubts, _choices);
        var changes = PreviewSegments.CountChanges(_segments);
        var unsure = PreviewSegments.CountDoubts(_segments);
        var counts = changes > 0 || unsure == 0 ? (changes == 1 ? "1 change" : changes + " changes") : "";
        if (unsure > 0) counts += (counts.Length > 0 ? ", " : "") + unsure + " unsure";
        _heading.Text = counts + (_paragraph ? " in this paragraph" : " in the selection");
        _view.AccessibleDescription = Describe(_segments, _doubts) + (_doubts.Count > 0 ? ". F8 goes to the next unsure word." : "");
        // With no edits and nothing picked there is nothing to paste.
        _replace.Enabled = Result != _original;
    }

    /// <summary>After a choice: draws the text again and puts the caret on that doubt.</summary>
    void Redraw(int doubt)
    {
        ShowChoices();
        _view.Clear();
        Fill();
        var mark = _marks.FirstOrDefault(m => m.Doubt == doubt);
        _view.Select(mark.Start, 0);
        _view.ScrollToCaret();
        _view.Focus();
    }

    /// <summary>The doubt under a point of the text box, or -1.</summary>
    int DoubtAt(Point point)
    {
        if (_marks.Count == 0) return -1;
        var index = _view.GetCharIndexFromPosition(point);
        var at = _view.GetPositionFromCharIndex(index);
        // The nearest character is returned even for a point beside the text.
        if (point.Y < at.Y || point.Y > at.Y + _view.Font.Height || point.X < at.X - 2 || point.X > at.X + _view.Font.Height) return -1;
        foreach (var mark in _marks)
            if (index >= mark.Start && index < mark.End) return mark.Doubt;
        return -1;
    }

    /// <summary>The suggestions and "Keep as typed" in a small menu below the word.</summary>
    void OpenMenu(int doubt)
    {
        var mark = _marks.First(m => m.Doubt == doubt);
        _view.Select(mark.Start, mark.End - mark.Start);
        _view.ScrollToCaret();
        _menuDoubt = doubt;
        _menu.Items.Clear();
        var suggestions = _doubts[doubt].Suggestions;
        for (var i = 0; i < suggestions.Count; i++)
            _menu.Items.Add(new ToolStripMenuItem(suggestions[i].Replace("&", "&&")) { Tag = i, Checked = _choices[doubt] == suggestions[i] });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Keep as typed") { Tag = -1, Checked = _choices[doubt] == null });
        var at = _view.GetPositionFromCharIndex(mark.Start);
        _menu.Show(_view, new Point(at.X, at.Y + _view.Font.Height));
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
        ActiveControl = _replace.Enabled ? _replace : _view;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // A rich text box keeps Enter for itself; in this window Enter means Replace wherever the focus is, except on Cancel.
        if (keyData == Keys.Enter && ActiveControl == _view)
        {
            if (_replace.Enabled) DialogResult = DialogResult.OK;
            return true;
        }
        // F8 goes to the next doubt after the caret and opens its menu.
        if (keyData == Keys.F8 && _marks.Count > 0)
        {
            var caret = _view.SelectionStart;
            OpenMenu(_marks.FirstOrDefault(m => m.Start > caret, _marks[0]).Doubt);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Writes the segments into the text box, notes where the doubts are and returns where the first change starts.</summary>
    int Fill()
    {
        var struck = Ui.Font(Ui.Reading, FontStyle.Strikeout);
        var underlined = Ui.Font(Ui.Reading, Ui.InsertedStyle);
        var firstChange = -1;
        _marks.Clear();
        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            var start = _view.TextLength;
            if (segment.Kind != SegmentKind.Same && firstChange < 0) firstChange = start;
            if (segment.Kind == SegmentKind.Removed) Append(segment.Text, struck, Ui.RemovedText, SystemColors.Window);
            else if (segment.Kind == SegmentKind.Same) Append(segment.Text, _view.Font, SystemColors.WindowText, SystemColors.Window);
            else if (segment.Kind == SegmentKind.Doubt) Append(segment.Text, _view.Font, Ui.UnsureText, Ui.UnsureBack, wave: true);
            else
            {
                // A narrow space, so a struck word and its replacement do not read as one word.
                if (i > 0 && _segments[i - 1].Kind == SegmentKind.Removed) Append(" ", _view.Font, SystemColors.WindowText, SystemColors.Window);
                Append(segment.Text, underlined, Ui.InsertedText, Ui.InsertedBack);
            }
            if (segment.Doubt < 0) continue;
            if (_marks.Count > 0 && _marks[^1].Doubt == segment.Doubt) _marks[^1] = _marks[^1] with { End = _view.TextLength };
            else _marks.Add((start, _view.TextLength, segment.Doubt));
        }
        return Math.Max(firstChange, 0);
    }

    void Append(string text, Font font, Color color, Color back, bool wave = false)
    {
        // Formatted after it is inserted: a rich text box counts a line break as one character, and the format of an empty selection does not always stick.
        var start = _view.TextLength;
        _view.AppendText(text);
        _view.Select(start, _view.TextLength - start);
        _view.SelectionFont = font;
        _view.SelectionColor = color;
        _view.SelectionBackColor = back;
        // The underline type is set on every piece, so a wave does not carry over into the text after a doubt.
        var format = new CHARFORMAT2W
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<CHARFORMAT2W>(),
            dwMask = NativeMethods.CfmUnderline | NativeMethods.CfmUnderlineType,
            dwEffects = wave || font.Underline ? NativeMethods.CfeUnderline : 0,
            bUnderlineType = wave ? NativeMethods.CfuUnderlineWave : NativeMethods.CfuUnderline,
            szFaceName = ""
        };
        NativeMethods.SendMessage(_view.Handle, NativeMethods.EmSetCharFormat, NativeMethods.ScfSelection, ref format);
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

    /// <summary>The changes and the doubts in words, for a screen reader, which cannot see the marks.</summary>
    static string Describe(IReadOnlyList<PreviewSegment> segments, IReadOnlyList<CorrectionDoubt> doubts)
    {
        var changes = new List<string>();
        for (var i = 0; i < segments.Count && changes.Count < 20; i++)
        {
            var (kind, text, doubt) = segments[i];
            if (kind == SegmentKind.Doubt)
                changes.Add("unsure: \"" + text + "\", suggested " + string.Join(" or ", doubts[doubt].Suggestions.Select(s => "\"" + s + "\"")));
            else if (kind == SegmentKind.Removed && i + 1 < segments.Count && segments[i + 1].Kind == SegmentKind.Inserted)
                changes.Add("\"" + text + "\" becomes \"" + segments[++i].Text + "\"");
            else if (kind != SegmentKind.Same)
                changes.Add("\"" + text + "\" " + (kind == SegmentKind.Removed ? "removed" : "added"));
        }
        return string.Join("; ", changes);
    }
}
