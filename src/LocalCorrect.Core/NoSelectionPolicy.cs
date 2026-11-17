namespace LocalCorrect.Core;

/// <summary>
/// What the hotkey does when nothing is selected: which keys select the current paragraph in which editor,
/// and when the copied paragraph is not safe to replace with plain text.
/// </summary>
public static class NoSelectionPolicy
{
    public const int MaxCharacters = 5000;

    /// <summary>Picks the key sequence from the class name of the focused window.</summary>
    public static SelectKeys KeysFor(string? windowClass)
    {
        var name = windowClass ?? "";
        if (name == "_WwG" || name == "MozillaWindowClass"
            || name.StartsWith("Chrome_", StringComparison.Ordinal)
            || name.Contains("RichEdit", StringComparison.OrdinalIgnoreCase))
            return SelectKeys.Paragraph;
        return SelectKeys.Line;
    }

    /// <summary>Removes one trailing paragraph mark ("\r\n", "\r" or "\n"); true when there was one.</summary>
    public static bool DropTrailingBreak(ref string text)
    {
        var cut = text.EndsWith("\r\n", StringComparison.Ordinal) ? 2 : text.EndsWith('\r') || text.EndsWith('\n') ? 1 : 0;
        text = text[..^cut];
        return cut > 0;
    }

    /// <summary>Why this paragraph must not be replaced, or null when it is a single run of plain text.</summary>
    public static string? Reject(string text, byte[]? html)
    {
        if (text.Length > MaxCharacters)
            return "This paragraph is too long. Select the words to correct, then press the hotkey again.";
        // Line breaks, vertical tab (Word's line break), form feed, tab and Word's table-cell mark.
        if (text.AsSpan().IndexOfAny("\r\n\v\f\t\a") >= 0)
            return "This text spans several lines or a table. Select the words to correct, then press the hotkey again.";
        if (html != null && ClipboardHtml.HasFormatting(html))
            return "This paragraph has links or formatting. Select the words to correct, then press the hotkey again.";
        return null;
    }
}
