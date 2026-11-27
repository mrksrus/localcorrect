using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalCorrect.Core;

namespace LocalCorrect.Engine;

/// <summary>
/// Default engine: asks the bundled LanguageTool server on 127.0.0.1 and applies only spelling, capitalisation,
/// grammar, punctuation and confused-word fixes (W-06). Anything doubtful is left alone. While the server is not
/// ready, or when a request fails or takes too long, the built-in word lists correct the text instead.
/// </summary>
public sealed class LanguageToolEngine : ICorrectionEngine
{
    public const string EngineId = "languagetool";
    public const string StartingNotice = "LanguageTool is still starting. The built-in word lists were used this time.";
    public const string FailedRequestNotice = "LanguageTool could not check this text in time. The built-in word lists were used this time.";

    static readonly HashSet<string> Applied = new(StringComparer.Ordinal)
    {
        "TYPOS", "CASING", "GRAMMAR", "PUNCTUATION", "CONFUSED_WORDS", "COMPOUNDING", "MULTITOKEN_SPELLING"
    };
    static readonly char[] LineBreaks = { '\n', '\r', '\u2028', '\u2029' };

    readonly LanguageToolServer _server;
    readonly SymSpellCorrector _wordLists;
    int _noticedStart;

    public LanguageToolEngine(LanguageToolServer server, SymSpellCorrector wordLists)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _wordLists = wordLists ?? throw new ArgumentNullException(nameof(wordLists));
    }

    public string Id => EngineId;
    public string DisplayName => "LanguageTool on this computer";
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public CorrectionResult Correct(string text, string language)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        var forced = (language ?? "auto").Trim().ToLowerInvariant();
        var code = forced is "en" or "de" ? forced : _wordLists.Detect(text);
        var address = _server.Address;
        if (_server.State != LanguageToolState.Ready || address == null)
        {
            // Not installed, failed or stopped: the tray has said so once already.
            if (_server.State != LanguageToolState.Starting) return Fallback(text, code, null);
            // One notice per server start; later presses while it starts fall back silently.
            var first = _noticedStart != _server.Starts;
            _noticedStart = _server.Starts;
            return Fallback(text, code, first ? StartingNotice : null);
        }

        (List<CorrectionEdit> Edits, List<CorrectionDoubt> Doubts)? found;
        try
        {
            using var cancel = new CancellationTokenSource(RequestTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "check"))
            {
                Content = LanguageToolServer.CheckContent(code == "de" ? "de-DE" : "en-US", text)
            };
            using var response = _server.Http.Send(request, cancel.Token);
            response.EnsureSuccessStatusCode();
            using var stream = response.Content.ReadAsStream(cancel.Token);
            using var document = JsonDocument.Parse(stream);
            found = Edits(text, code, document.RootElement);
        }
        catch (Exception)
        {
            found = null;
        }
        if (found is not var (edits, doubts)) return Fallback(text, code, FailedRequestNotice);
        return new CorrectionResult(CorrectionEdit.Apply(text, edits), code, edits, EngineId, doubts: doubts);
    }

    CorrectionResult Fallback(string text, string code, string? notice)
    {
        var result = _wordLists.Correct(text, code);
        return new CorrectionResult(result.Text, result.Language, result.Edits, result.Engine, notice);
    }

    /// <summary>
    /// The accepted edits and the doubts (W-11), or null when the response does not fit the text (then nothing of it
    /// is trusted). A doubt is a match the name policy, the distance limit or the fragment-start rule held back.
    /// </summary>
    (List<CorrectionEdit> Edits, List<CorrectionDoubt> Doubts)? Edits(string text, string code, JsonElement root)
    {
        var matches = new List<(int Offset, int Length, string Rule, string Category, List<string> Replacements)>();
        foreach (var match in root.GetProperty("matches").EnumerateArray())
        {
            var offset = match.GetProperty("offset").GetInt32();
            var length = match.GetProperty("length").GetInt32();
            if (offset < 0 || length < 0 || offset > text.Length - length) return null;
            var rule = match.GetProperty("rule");
            var replacements = new List<string>();
            if (match.TryGetProperty("replacements", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray().Take(3))
                    if (item.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
                        replacements.Add(value.GetString()!);
            }
            matches.Add((offset, length, rule.GetProperty("id").GetString() ?? "", rule.GetProperty("category").GetProperty("id").GetString() ?? "", replacements));
        }

        List<(int Start, int End)> protectedRanges;
        try
        {
            protectedRanges = TextPolicy.ProtectedRanges(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
        var edits = new List<CorrectionEdit>();
        var doubts = new List<CorrectionDoubt>();
        var end = 0;
        foreach (var match in matches.OrderBy(match => match.Offset))
        {
            var original = text.Substring(match.Offset, match.Length);
            var candidates = match.Replacements.Where(r => r.Length > 0 && r != original && r.IndexOfAny(LineBreaks) < 0).ToList();
            if (original.Length == 0 || candidates.Count == 0 || original.IndexOfAny(LineBreaks) >= 0) continue;
            if (!Applied.Contains(match.Category)) continue;
            if (match.Offset < end || TextPolicy.Overlaps(protectedRanges, match.Offset, match.Offset + match.Length)) continue;
            end = match.Offset + match.Length;
            string? replacement;
            // The text may be a fragment that starts in the middle of a sentence.
            if (match.Rule == "UPPERCASE_SENTENCE_START" && text.AsSpan(0, match.Offset).IsWhiteSpace()) replacement = null;
            else if (!IsSpeller(match.Rule)) replacement = candidates[0];
            else
            {
                var caseFix = TextPolicy.TwoCapitalsFix(original);
                replacement = caseFix != null && candidates.Contains(caseFix)
                    ? caseFix
                    : candidates.FirstOrDefault(candidate => !LooksLikeName(text, code, match.Offset, original, candidate));
            }
            if (replacement != null) edits.Add(new CorrectionEdit(match.Offset, match.Length, original, replacement));
            else doubts.Add(new CorrectionDoubt(match.Offset, match.Length, original, candidates));
        }
        return (edits, doubts);
    }

    static bool IsSpeller(string rule) => rule == "GERMAN_SPELLER_RULE" || rule.StartsWith("MORFOLOGIK_RULE_", StringComparison.Ordinal);

    /// <summary>The word-list name policy, so a name or a word of the other language is not "corrected".</summary>
    bool LooksLikeName(string text, string code, int start, string word, string suggestion)
    {
        if (word.Length > 1 && word.Substring(1).Any(char.IsUpper)) return true;
        if (_wordLists.IsKnown(word)) return true;
        var distance = Distance(Squash(word), Squash(suggestion));
        // A far-off suggestion is a guess (a name, a tool, a word the speller lacks), not a typo fix.
        // A word of nine letters or more may hold three slips and still be a typo, not a guess.
        var limit = word.Count(char.IsLetter) >= 9 ? 3 : 2;
        if (!char.IsUpper(word[0])) return distance > limit;
        if (TextPolicy.FollowsTitle(text, start)) return true;
        if (TextPolicy.IsSentenceStart(text, start)) return distance > limit;
        // English: a capital inside a sentence is a name. German nouns are capitalised, so only a close fix applies.
        return code == "en" || distance > 1;
    }

    static string Squash(string value) => value.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>Edit distance counting insertions, deletions, substitutions and swaps of neighbours.</summary>
    static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }
        return d[a.Length, b.Length];
    }
}
