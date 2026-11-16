using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LocalCorrect.Core;

namespace LocalCorrect.Engine;

/// <summary>
/// English and German spelling correction from word-frequency lists.
/// A word that is already in the list for the text's language is left alone
/// (German compounds may be split into known parts). Otherwise the most
/// frequent word one edit away wins, and longer words may take two edits.
/// Swapped letters get a bonus because that is the common slip. Missing
/// German umlauts and ß, German noun capitals, and English contractions are
/// covered too.
/// This is the same policy as extension/corrector.js in localcorrect-browser
/// and native/Engine/Corrector.cs in localcorrect-outlook, written here for
/// the MIT tray app. The idea follows Peter Norvig's spelling corrector and
/// Wolf Garbe's SymSpell. It does not fix grammar or a real word in the wrong place.
/// </summary>
public sealed class SymSpellCorrector : ICorrectionEngine
{
    public const string EngineId = "symspell";
    const int MaxSuggestLength = 32;
    const int MaxTextLength = 200_000;
    const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    static readonly (string From, string To)[] Umlauts =
    {
        ("ae", "ä"), ("oe", "ö"), ("ue", "ü"), ("ss", "ß"), ("a", "ä"), ("o", "ö"), ("u", "ü")
    };

    static readonly Dictionary<string, string> Contractions = BuildContractions();
    static readonly HashSet<string> GermanColloquial = new(StringComparer.Ordinal)
    {
        "gehts", "gibts", "wars", "klappts", "passts", "stimmts", "hats", "gings", "machts", "ists"
    };
    static readonly HashSet<string> Tech = new(StringComparer.Ordinal)
    {
        "npm", "npx", "dev", "devs", "repo", "repos", "config", "configs", "api", "apis", "cli", "url", "urls",
        "auth", "env", "prod", "admin", "app", "apps", "sql", "sudo", "git", "github", "gitlab", "yaml", "json",
        "http", "https", "async", "todo", "todos", "bugfix", "hotfix", "frontend", "backend", "localhost",
        "webhook", "webhooks", "refactor", "refactoring", "changelog", "codebase", "docker", "kubernetes",
        "linux", "macos", "ios", "wifi", "online", "offline", "login", "logout", "email", "emails", "inbox",
        "spam", "blog", "vlog", "podcast", "smartphone", "laptop", "tablet", "browser", "chatbot"
    };

    static readonly Regex Titles = new(@"(?:Herr|Herrn|Frau|Dr|Prof|Mr|Mrs|Ms|Dear|Liebe|Lieber|Hallo|Hi|Hello)\.?\s+\z", Options, Timeout);
    static readonly Regex Skip = new(@"(?:[a-z][a-z0-9+.-]*://|www\.)\S+|(?<!\S)--?\p{L}[\p{L}0-9-]*|[^\s@]+@\S+|[@#$%][\p{L}0-9_]+|\S*[0-9_\\/]\S*|[\p{L}0-9-]+(?:\.[\p{L}0-9-]+)+", Options | RegexOptions.IgnoreCase, Timeout);
    static readonly Regex Quoted = new(@"^[ \t]*>[^\n\r\u2028\u2029]*", Options | RegexOptions.Multiline, Timeout);
    static readonly Regex Word = new(@"[\p{L}\p{M}]+(?:['’][\p{L}]+)?", Options, Timeout);
    static readonly Regex LoneI = new(@"(?<![\p{L}'’.-])i(?![\p{L}'’.-])", Options, Timeout);
    static readonly Regex SentenceEnd = new(@"(?:[.!?:]\s+|\n\s*)\z", Options, Timeout);
    static readonly Regex Upper = new(@"\p{Lu}", Options, Timeout);

    readonly Dictionary<string, Lang> _langs;

    public SymSpellCorrector(string englishLexicon, string germanLexicon)
    {
        if (englishLexicon == null) throw new ArgumentNullException(nameof(englishLexicon));
        if (germanLexicon == null) throw new ArgumentNullException(nameof(germanLexicon));
        _langs = new Dictionary<string, Lang>(StringComparer.Ordinal)
        {
            ["en"] = Parse(englishLexicon, "abcdefghijklmnopqrstuvwxyz"),
            ["de"] = Parse(germanLexicon, "abcdefghijklmnopqrstuvwxyzäöüß")
        };
    }

    public string Id => EngineId;
    public string DisplayName => "Built-in word lists (offline)";
    public int EnglishWords => _langs["en"].Counts.Count;
    public int GermanWords => _langs["de"].Counts.Count;

    public static SymSpellCorrector FromEmbeddedLexicons()
    {
        return new SymSpellCorrector(ReadResource("LocalCorrect.en.lex"), ReadResource("LocalCorrect.de.lex"));
    }

    public string Detect(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        var en = 0;
        var de = 0;
        foreach (Match match in Word.Matches(text.ToLowerInvariant()))
        {
            if (_langs["en"].Counts.ContainsKey(match.Value)) en++;
            if (_langs["de"].Counts.ContainsKey(match.Value)) de++;
        }
        return de > en ? "de" : "en";
    }

    public CorrectionResult Correct(string text, string language)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (text.Length > MaxTextLength)
            throw new LocalEngineException("That text is too long to correct. Nothing was changed.");
        try
        {
            return CorrectCore(text, language ?? "auto");
        }
        catch (RegexMatchTimeoutException)
        {
            throw new LocalEngineException("That text was too complex to correct. Nothing was changed.");
        }
    }

    CorrectionResult CorrectCore(string text, string language)
    {
        var normalized = language.Trim().ToLowerInvariant();
        var code = normalized is "en" or "de" ? normalized : Detect(text);
        var other = code == "en" ? "de" : "en";
        var skip = new List<(int Start, int End)>();
        foreach (Match match in Skip.Matches(text)) skip.Add((match.Index, match.Index + match.Length));
        foreach (Match match in Quoted.Matches(text)) skip.Add((match.Index, match.Index + match.Length));

        var edits = new List<CorrectionEdit>();
        if (code == "en")
        {
            foreach (Match match in LoneI.Matches(text))
            {
                if (!Overlaps(skip, match.Index, match.Index + 1))
                    edits.Add(new CorrectionEdit(match.Index, 1, "i", "I"));
            }
        }

        foreach (Match match in Word.Matches(text))
        {
            var typed = match.Value;
            var start = match.Index;
            var end = start + typed.Length;
            var lowerTyped = Lower(typed);
            var contraction = code == "en" && Contractions.ContainsKey(lowerTyped);
            if ((typed.Length < 3 && !contraction) || Overlaps(skip, start, end)) continue;
            if (typed.Length > 1 && Upper.IsMatch(typed.Substring(1))) continue;
            if (typed.IndexOf('\'') >= 0 || typed.IndexOf('’') >= 0) continue;
            if (Valid(code, typed)) continue;

            var from = Math.Max(0, start - 40);
            var before = text.Substring(from, start - from);
            var sentenceStart = start == 0 || SentenceEnd.IsMatch(before);
            var capitalised = typed[0] != char.ToLowerInvariant(typed[0]);
            if (capitalised && (Titles.IsMatch(before) || (code == "en" && !sentenceStart))) continue;

            var (suggested, distance) = Suggest(code, lowerTyped, !contraction && Valid(other, typed));
            if (suggested == null) continue;
            if (code == "de" && capitalised && !sentenceStart && (distance > 1 || typed.Length < 4)) continue;
            var replacement = Cased(code, typed, suggested);
            if (!string.Equals(replacement, typed, StringComparison.Ordinal))
                edits.Add(new CorrectionEdit(start, typed.Length, typed, replacement));
        }

        edits = edits.OrderBy(edit => edit.Offset).ToList();
        var output = new StringBuilder(text.Length + 16);
        var position = 0;
        foreach (var edit in edits)
        {
            output.Append(text, position, edit.Offset - position);
            output.Append(edit.Replacement);
            position = edit.Offset + edit.Length;
        }
        output.Append(text, position, text.Length - position);
        return new CorrectionResult(output.ToString(), code, edits);
    }

    static bool Overlaps(List<(int Start, int End)> ranges, int start, int end)
    {
        foreach (var range in ranges)
        {
            if (start < range.End && end > range.Start) return true;
        }
        return false;
    }

    (string? Word, int Distance) Suggest(string code, string lower, bool umlautOnly)
    {
        if (lower.Length > MaxSuggestLength) return (null, 0);
        var lang = _langs[code];
        if (code == "de")
        {
            foreach (var pair in Umlauts)
            {
                var candidate = lower.Replace(pair.From, pair.To, StringComparison.Ordinal);
                if (!string.Equals(candidate, lower, StringComparison.Ordinal) && lang.Counts.ContainsKey(candidate))
                    return (candidate, 1);
            }
        }
        if (umlautOnly) return (null, 0);
        if (code == "en" && Contractions.TryGetValue(lower, out var contraction)) return (contraction, 1);
        if (lang.Counts.ContainsKey(lower)) return (lower, 0);

        bool Plain(string word)
        {
            if (code != "en") return true;
            return lang.Forms.TryGetValue(word, out var forms) && forms.Contains(word);
        }

        var one = Edits1(lower, lang.Alphabet);
        var best = MostFrequent(lang, one.Where(word => Plain(word) && (lower.Length > 3 || word.Length > 3 || Swapped(word, lower))), 1, lower);
        if (best != null) return (best, 1);
        if (lower.Length < 5) return (null, 0);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var two = new List<string>();
        foreach (var edit in one)
        {
            foreach (var next in Edits1(edit, lang.Alphabet))
            {
                if (lang.Counts.ContainsKey(next) && Plain(next) && seen.Add(next)) two.Add(next);
            }
        }
        return (MostFrequent(lang, two, 20, ""), 2);
    }

    string Cased(string code, string typed, string lower)
    {
        if (!_langs[code].Forms.TryGetValue(lower, out var list) || list.Count == 0)
            list = new List<string> { lower };
        var plain = list.Contains(lower) ? lower : list[0];
        if (typed.Length > 1 && typed == UpperJs(typed)) return UpperJs(plain);
        if (plain.StartsWith("i'", StringComparison.Ordinal)) return "I" + plain.Substring(1);
        var typedCap = typed.Length > 0 && typed[0] != char.ToLowerInvariant(typed[0]);
        if (typedCap && plain == lower) return char.ToUpperInvariant(plain[0]) + plain.Substring(1);
        return plain;
    }

    bool Valid(string code, string word)
    {
        var lower = Lower(word);
        if (Known(_langs[code], word) || Tech.Contains(lower)) return true;
        return code == "de" && (GermanColloquial.Contains(lower) || Compound(_langs["de"], lower));
    }

    static bool Known(Lang lang, string word)
    {
        if (!lang.Forms.TryGetValue(Lower(word), out var list)) return false;
        if (list.Contains(word) || word == UpperJs(word)) return true;
        if (word.Length == 0) return false;
        var decap = char.ToLowerInvariant(word[0]) + word.Substring(1);
        return word != decap && list.Contains(decap);
    }

    static bool Compound(Lang lang, string lower, int depth = 0)
    {
        if (lower.Length < 8 || depth > 3) return false;
        for (var i = lower.Length - 3; i >= 3; i--)
        {
            var head = lower.Substring(0, i);
            var tail = lower.Substring(i);
            var headOk = lang.Counts.ContainsKey(head)
                || (head.Length > 1 && head[head.Length - 1] == 's' && lang.Counts.ContainsKey(head.Substring(0, head.Length - 1)));
            if (headOk && (lang.Counts.ContainsKey(tail) || Compound(lang, tail, depth + 1))) return true;
        }
        return false;
    }

    static List<string> Edits1(string word, string alphabet)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        void Add(string value)
        {
            if (seen.Add(value)) order.Add(value);
        }
        for (var i = 0; i <= word.Length; i++)
        {
            var a = word.Substring(0, i);
            var b = word.Substring(i);
            if (b.Length > 0) Add(a + b.Substring(1));
            if (b.Length > 1) Add(a + b[1] + b[0] + b.Substring(2));
            foreach (var c in alphabet)
            {
                if (b.Length > 0) Add(a + c + b.Substring(1));
                Add(a + c + b);
            }
        }
        order.Remove(word);
        return order;
    }

    static bool Swapped(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var left = a.ToCharArray();
        var right = b.ToCharArray();
        Array.Sort(left);
        Array.Sort(right);
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    static string? MostFrequent(Lang lang, IEnumerable<string> words, long minimum, string typed)
    {
        string? top = null;
        var best = minimum - 1;
        foreach (var word in words)
        {
            lang.Counts.TryGetValue(word, out var count);
            var score = count * (Swapped(word, typed) ? 50 : 1);
            if (score > best)
            {
                top = word;
                best = score;
            }
        }
        return top;
    }

    static Lang Parse(string source, string alphabet)
    {
        var lang = new Lang(alphabet);
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.Length > 0 && raw[raw.Length - 1] == '\r' ? raw.Substring(0, raw.Length - 1) : raw;
            if (line.Length == 0) continue;
            var space = line.IndexOf(' ');
            if (space <= 0) continue;
            var word = line.Substring(0, space);
            if (!long.TryParse(line.AsSpan(space + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                continue;
            if (count < 0) continue;
            var lower = Lower(word);
            if (!lang.Counts.TryGetValue(lower, out var previous) || count > previous) lang.Counts[lower] = count;
            if (!lang.Forms.TryGetValue(lower, out var forms))
            {
                forms = new List<string>(2);
                lang.Forms[lower] = forms;
            }
            forms.Add(word);
        }
        return lang;
    }

    static string ReadResource(string name)
    {
        var assembly = typeof(SymSpellCorrector).Assembly;
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing word list " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    static Dictionary<string, string> BuildContractions()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var word in "don't can't i'm i've i'll i'd isn't didn't doesn't wouldn't couldn't shouldn't haven't hasn't hadn't wasn't weren't aren't you're they're that's what's there's here's it'll you've we've they've you'll they'll let's who's won't ain't".Split(' '))
            map[word.Replace("'", "", StringComparison.Ordinal)] = word;
        map.Remove("wont");
        map.Remove("lets");
        map.Remove("ill");
        map.Remove("id");
        map.Remove("well");
        return map;
    }

    static string Lower(string value) => value.ToLowerInvariant();

    static string UpperJs(string value) => value.ToUpperInvariant().Replace("ß", "SS", StringComparison.Ordinal);

    sealed class Lang
    {
        public Lang(string alphabet) => Alphabet = alphabet;
        public string Alphabet { get; }
        public Dictionary<string, long> Counts { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<string>> Forms { get; } = new(StringComparer.Ordinal);
    }
}
