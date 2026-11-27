using System.Text.RegularExpressions;

namespace LocalCorrect.Engine;

/// <summary>
/// What no engine may change (links, e-mail addresses, code-like tokens, quoted <c>&gt;</c> lines) and the
/// sentence and title rules of the name policy. Shared by the word lists and LanguageTool.
/// Matching can throw <see cref="RegexMatchTimeoutException"/> on pathological text.
/// </summary>
public static class TextPolicy
{
    const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    static readonly Regex Titles = new(@"(?:Herr|Herrn|Frau|Dr|Prof|Mr|Mrs|Ms|Dear|Liebe|Lieber|Hallo|Hi|Hello)\.?\s+\z", Options, Timeout);
    static readonly Regex Skip = new(@"(?:[a-z][a-z0-9+.-]*://|www\.)\S+|(?<!\S)--?\p{L}[\p{L}0-9-]*|[^\s@]+@\S+|[@#$%][\p{L}0-9_]+|\S*[0-9_\\/]\S*|[\p{L}0-9-]+(?:\.[\p{L}0-9-]+)+", Options | RegexOptions.IgnoreCase, Timeout);
    static readonly Regex Quoted = new(@"^[ \t]*>[^\n\r\u2028\u2029]*", Options | RegexOptions.Multiline, Timeout);
    static readonly Regex SentenceEnd = new(@"(?:[.!?:]\s+|\n\s*)\z", Options, Timeout);

    public static List<(int Start, int End)> ProtectedRanges(string text)
    {
        var ranges = new List<(int Start, int End)>();
        foreach (Match match in Skip.Matches(text)) ranges.Add((match.Index, match.Index + match.Length));
        foreach (Match match in Quoted.Matches(text)) ranges.Add((match.Index, match.Index + match.Length));
        return ranges;
    }

    public static bool Overlaps(List<(int Start, int End)> ranges, int start, int end)
    {
        foreach (var range in ranges)
        {
            if (start < range.End && end > range.Start) return true;
        }
        return false;
    }

    public static bool IsSentenceStart(string text, int start) => start == 0 || SentenceEnd.IsMatch(Before(text, start));

    public static bool FollowsTitle(string text, int start) => Titles.IsMatch(Before(text, start));

    /// <summary>
    /// "THe", "HEllo", "DAnke": two capitals and then only lowercase letters is a slip on the shift key, so the form
    /// with the second letter lowered is returned. Null for anything else, including plurals of acronyms ("IDs", "PCs").
    /// </summary>
    public static string? TwoCapitalsFix(string word)
    {
        if (word.Length < 3 || !char.IsUpper(word[0]) || !char.IsUpper(word[1])) return null;
        for (var i = 2; i < word.Length; i++)
            if (!char.IsLower(word[i])) return null;
        if (word.Length == 3 && word[2] == 's') return null;
        return word[0] + char.ToLowerInvariant(word[1]).ToString() + word.Substring(2);
    }

    static string Before(string text, int start)
    {
        var from = Math.Max(0, start - 40);
        return text.Substring(from, start - from);
    }
}
