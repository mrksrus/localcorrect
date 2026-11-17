using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.App;

internal sealed class SettingsForm : Form
{
    readonly CheckBox _ctrl = new() { Text = "Ctrl", AutoSize = true, Checked = true };
    readonly CheckBox _alt = new() { Text = "Alt", AutoSize = true, Checked = true };
    readonly CheckBox _shift = new() { Text = "Shift", AutoSize = true };
    readonly CheckBox _win = new() { Text = "Win", AutoSize = true };
    readonly ComboBox _key = new() { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Hotkey key" };
    readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox _engine = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox _endpoint = new();
    readonly TextBox _model = new();
    readonly CheckBox _preview = new() { Text = "Show the changes and ask before replacing", AutoSize = true };
    readonly CheckBox _paragraph = new() { Text = "When nothing is selected, correct the current paragraph", AutoSize = true };
    readonly TextBox _excluded = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, AccessibleName = "Excluded programs" };
    readonly CheckBox _startup = new() { Text = "Start LocalCorrect when I sign in to Windows", AutoSize = true };
    readonly CheckBox _logging = new() { Text = "Write a diagnostic log", AutoSize = true };
    readonly Label _languageTool = Ui.Label("");

    public AppSettings? Saved { get; private set; }

    public SettingsForm(AppSettings current, string languageToolState)
    {
        var em = Ui.Em;
        Text = "LocalCorrect settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = Ui.Font();
        // The window is as large as its content at the current text size; nothing has a pixel size of its own.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        for (var letter = 'A'; letter <= 'Z'; letter++) _key.Items.Add(letter.ToString());
        for (var function = 1; function <= 12; function++) _key.Items.Add("F" + function.ToString());
        _language.Items.AddRange(new object[] { "Detect automatically", "English", "German (Deutsch)" });
        _engine.Items.AddRange(new object[] { "LanguageTool (recommended)", "Built-in word lists only", "Local model (Ollama)" });

        _ctrl.Checked = current.Hotkey.Ctrl;
        _alt.Checked = current.Hotkey.Alt;
        _shift.Checked = current.Hotkey.Shift;
        _win.Checked = current.Hotkey.Win;
        var keyName = VirtualKeys.Normalize(current.Hotkey.Key);
        var keyIndex = _key.Items.IndexOf(keyName);
        _key.SelectedIndex = keyIndex >= 0 ? keyIndex : 0;
        _language.SelectedIndex = current.Language switch { "en" => 1, "de" => 2, _ => 0 };
        _engine.SelectedIndex = current.Engine switch { "wordlists" => 1, "ollama" => 2, _ => 0 };
        _endpoint.Text = current.LocalModelEndpoint;
        _model.Text = current.LocalModelName;
        _preview.Checked = current.PreviewBeforeReplace;
        _paragraph.Checked = current.CorrectParagraphWhenNothingSelected;
        _excluded.Text = string.Join(Environment.NewLine, current.ExcludedProcesses);
        _startup.Checked = current.StartWithWindows;
        _logging.Checked = current.Logging;
        _engine.SelectedIndexChanged += (_, _) => UpdateEndpointEnabled();
        UpdateEndpointEnabled();
        ShowLanguageToolState(languageToolState);

        // Two columns: what the hotkey does on the left; the optional parts and the credits on the right.
        var wrapAt = em * 25;
        var everyday = Column();
        Section(everyday, "Correction");
        Row(everyday, "Language", _language);
        Row(everyday, "Engine", _engine);
        Wide(everyday, Ui.Label("LanguageTool needs about 1 GB of memory and a minute to start. Until it is ready, and if it fails, the built-in word lists are used.", muted: true, wrapAt: wrapAt));
        Section(everyday, "Hotkey");
        _key.Font = Font;
        _key.Width = em * 4;
        _key.Margin = new Padding(em / 2, 0, 0, 0);
        _key.Anchor = AnchorStyles.Left;
        // The row has a height of its own: a drop-down list is taller on screen than the height it reports for layout.
        var hotkey = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, MinimumSize = new Size(0, em * 2) };
        foreach (var modifier in new[] { _ctrl, _alt, _shift, _win })
        {
            modifier.Anchor = AnchorStyles.Left;
            modifier.Margin = new Padding(0, 0, em / 2, 0);
            modifier.Font = Font;
        }
        hotkey.Controls.AddRange(new Control[] { _ctrl, _alt, _shift, _win, _key });
        Wide(everyday, hotkey);
        Wide(everyday, Ui.Label("Ctrl or Alt, plus a letter or F1 to F12.", muted: true, wrapAt: wrapAt));
        Section(everyday, "Behaviour");
        Wide(everyday, _preview);
        Wide(everyday, _paragraph);
        Wide(everyday, _startup);
        Section(everyday, "Privacy and log");
        Wide(everyday, Ui.Label("Your text is corrected on this computer. It is never sent anywhere and never written to the log.", muted: true, wrapAt: wrapAt));
        Wide(everyday, _logging);

        var extras = Column();
        extras.Margin = new Padding(em * 2, 0, 0, 0);
        extras.TabIndex = 1;
        Section(extras, "Local model");
        Wide(extras, Ui.Label("Optional. Used when the engine is the local model: an Ollama-compatible server on this computer (127.0.0.1, localhost or ::1). Keep the preview on when you try it.", muted: true, wrapAt: wrapAt));
        Row(extras, "Address", _endpoint);
        Row(extras, "Model", _model);
        Section(extras, "Excluded programs");
        Wide(extras, Ui.Label("LocalCorrect does nothing in these programs. One name per line; .exe is optional.", muted: true, wrapAt: wrapAt));
        _excluded.Height = em * 5;
        _excluded.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        Wide(extras, _excluded);
        Section(extras, "About");
        Wide(extras, Ui.Label(Program.VersionLabel()));
        Wide(extras, _languageTool);
        Wide(extras, Ui.Label("Uses LanguageTool (LGPL 2.1 or later), unmodified, for English and German. LocalCorrect is not affiliated with LanguageTool.", muted: true, wrapAt: wrapAt));
        var licences = Ui.Button("Licences and credits");
        licences.Click += (_, _) => Ui.OpenLicences(this);
        Wide(extras, licences);
        licences.Margin = new Padding(0, em / 2, 0, 0);

        var columns = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Margin = Padding.Empty, TabIndex = 0 };
        columns.Controls.Add(everyday, 0, 0);
        columns.Controls.Add(extras, 1, 0);

        var save = Ui.Button("Save");
        var cancel = Ui.Button("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        save.Click += (_, _) => Save();
        AcceptButton = save;
        CancelButton = cancel;
        var buttons = Ui.Buttons(save, cancel);
        buttons.Margin = new Padding(0, em, 0, 0);
        buttons.TabIndex = 1;

        var root = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(em) };
        root.Controls.Add(columns, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
    }

    /// <summary>The LanguageTool line in About; the tray updates it while the window is open.</summary>
    public void ShowLanguageToolState(string state) => _languageTool.Text = state;

    void UpdateEndpointEnabled()
    {
        var local = _engine.SelectedIndex == 2;
        _endpoint.Enabled = local;
        _model.Enabled = local;
    }

    void Save()
    {
        var hotkey = new HotkeySpec
        {
            Ctrl = _ctrl.Checked,
            Alt = _alt.Checked,
            Shift = _shift.Checked,
            Win = _win.Checked,
            Key = _key.SelectedItem?.ToString() ?? "L"
        };
        if (!hotkey.IsValid)
        {
            MessageBox.Show(this, "Choose Ctrl or Alt, plus a letter or a function key. Alt+F4 and the Windows key on its own are not available.", "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var engine = _engine.SelectedIndex switch { 1 => "wordlists", 2 => "ollama", _ => "languagetool" };
        var endpoint = _endpoint.Text.Trim();
        var model = _model.Text.Trim();
        if (engine == "ollama")
        {
            if (!LoopbackEndpoint.TryNormalize(endpoint, out _, out var error))
            {
                MessageBox.Show(this, error, "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!OllamaCorrectionEngine.IsValidModelName(model))
            {
                MessageBox.Show(this, "Enter a model name such as llama3.2.", "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        Saved = new AppSettings
        {
            Hotkey = hotkey,
            Language = _language.SelectedIndex switch { 1 => "en", 2 => "de", _ => "auto" },
            Engine = engine,
            LocalModelEndpoint = endpoint.Length == 0 ? "http://127.0.0.1:11434" : endpoint,
            LocalModelName = model.Length == 0 ? "llama3.2" : model,
            PreviewBeforeReplace = _preview.Checked,
            CorrectParagraphWhenNothingSelected = _paragraph.Checked,
            ExcludedProcesses = AppSettings.NormalizeExclusions(_excluded.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)),
            StartWithWindows = _startup.Checked,
            Logging = _logging.Checked
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    static TableLayoutPanel Column() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 2,
        Anchor = AnchorStyles.Top | AnchorStyles.Left,
        Margin = Padding.Empty
    };

    /// <summary>Adds a control in the next row and in tab order; a wide one spans the label and the field column.</summary>
    static void Add(TableLayoutPanel table, Control control, int column, bool wide, bool newRow = true)
    {
        if (newRow)
        {
            table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        // Set here and not inherited later: a control that was measured in the default font keeps that size for the layout.
        if (control is not Label) control.Font = Ui.Font();
        control.TabIndex = table.Controls.Count;
        table.Controls.Add(control, column, table.RowCount - 1);
        if (wide) table.SetColumnSpan(control, 2);
    }

    static void Section(TableLayoutPanel table, string title)
    {
        var heading = Ui.Label(title, style: FontStyle.Bold);
        heading.Margin = new Padding(0, table.RowCount == 0 ? 0 : Ui.Em, 0, Ui.Em / 3);
        Add(table, heading, 0, wide: true);
    }

    static void Row(TableLayoutPanel table, string label, Control field)
    {
        var name = Ui.Label(label);
        name.Anchor = AnchorStyles.Left;
        name.Margin = new Padding(0, 0, Ui.Em, 0);
        Add(table, name, 0, wide: false);
        field.AccessibleName = label;
        // The font first: a drop-down list takes its height from the font when its width is set, and the row is laid out with that height.
        field.Font = Ui.Font();
        field.Width = Ui.Em * 17;
        field.Margin = new Padding(0, Ui.Em / 5, 0, Ui.Em / 5);
        Add(table, field, 1, wide: false, newRow: false);
    }

    static void Wide(TableLayoutPanel table, Control control)
    {
        control.Margin = new Padding(0, Ui.Em / 5, 0, Ui.Em / 5);
        Add(table, control, 0, wide: true);
    }
}
