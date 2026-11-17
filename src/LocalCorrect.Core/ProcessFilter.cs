namespace LocalCorrect.Core;

public static class ProcessFilter
{
    /// <summary>
    /// Terminals (Ctrl+C interrupts a command), code editors (Ctrl+C with nothing selected copies the line)
    /// and Excel (a paste overwrites the cell): the hotkey sends no keys there at all.
    /// </summary>
    public static readonly string[] NeverCorrect =
    {
        "WindowsTerminal", "cmd", "powershell", "pwsh", "conhost", "putty", "mintty",
        "Code", "devenv", "idea64", "sublime_text", "notepad++",
        "excel"
    };

    public static bool IsExcluded(string? processName, IReadOnlyList<string>? excluded)
    {
        if (excluded == null || excluded.Count == 0) return false;
        var name = CoreName(processName);
        if (name.Length == 0) return false;
        foreach (var entry in excluded)
        {
            var item = CoreName(entry);
            if (item.Length == 0) continue;
            if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static string CoreName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var name = value.Trim();
        var slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
        if (slash >= 0 && slash < name.Length - 1) name = name[(slash + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.Trim();
    }
}
