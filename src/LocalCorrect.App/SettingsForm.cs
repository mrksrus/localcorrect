using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.App;

internal sealed class SettingsForm : Form
{
    readonly CheckBox _ctrl = new() { Text = "Ctrl", AutoSize = true, Checked = true };
    readonly CheckBox _alt = new() { Text = "Alt", AutoSize = true, Checked = true };
    readonly CheckBox _shift = new() { Text = "Shift", AutoSize = true };
    readonly CheckBox _win = new() { Text = "Win", AutoSize = true };
    readonly ComboBox _key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    readonly ComboBox _engine = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    readonly TextBox _endpoint = new() { Dock = DockStyle.Fill };
    readonly TextBox _model = new() { Dock = DockStyle.Fill };
    readonly CheckBox _preview = new() { Text = "Ask before replacing the selection", AutoSize = true };
    readonly CheckBox _paragraph = new() { Text = "When nothing is selected, correct the current paragraph", AutoSize = true };
    readonly TextBox _excluded = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AcceptsReturn = true };
    readonly CheckBox _startup = new() { Text = "Start LocalCorrect when I sign in to Windows", AutoSize = true };
    readonly CheckBox _logging = new() { Text = "Write a diagnostic log (the text you correct is never written)", AutoSize = true };

    public AppSettings? Saved { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Text = "LocalCorrect settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = SystemFonts.MessageBoxFont;
        ClientSize = new Size(520, 590);

        for (var letter = 'A'; letter <= 'Z'; letter++) _key.Items.Add(letter.ToString());
        for (var function = 1; function <= 12; function++) _key.Items.Add("F" + function.ToString());
        _language.Items.AddRange(new object[] { "Detect automatically", "English", "Deutsch" });
        _engine.Items.AddRange(new object[] { "LanguageTool on this computer (recommended)", "Built-in word lists only", "Local model on this computer (Ollama)" });

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

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var hotkey = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoSize = true };
        hotkey.Controls.AddRange(new Control[] { _ctrl, _alt, _shift, _win, _key });

        AddRow(table, "Hotkey", hotkey);
        AddRow(table, "Language", _language);
        AddRow(table, "Engine", _engine);
        AddRow(table, "Model address", _endpoint);
        AddRow(table, "Model name", _model);
        AddSpan(table, _preview);
        AddSpan(table, _paragraph);
        AddSpan(table, new Label { Text = "Do not correct these programs (one name per line, .exe optional)", AutoSize = true, MaximumSize = new Size(470, 0) });
        AddSpan(table, _excluded, 90);
        AddSpan(table, _startup);
        AddSpan(table, _logging);
        AddSpan(table, new Label
        {
            Text = "LanguageTool runs on this computer and fixes spelling, grammar and punctuation. It needs about 1 GB of memory and a minute to start; until then, and if it fails, the built-in word lists are used. The local model option only talks to an Ollama-compatible server at 127.0.0.1, localhost, or ::1. Leave the preview on when you try it.",
            AutoSize = true,
            MaximumSize = new Size(470, 0)
        });

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(12, 8, 12, 8)
        };
        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;

        Controls.Add(buttons);
        Controls.Add(table);
    }

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

    static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) }, 0, row);
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(control, 1, row);
    }

    static void AddSpan(TableLayoutPanel table, Control control, int height = 0)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, height) : new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 6, 0, 2);
        if (height > 0) control.Height = height - 8;
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
    }
}
