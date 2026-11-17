using System.Net;
using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.Tests;

/// <summary>
/// The engine on /v2/check answers recorded from LanguageTool 6.6 for synthetic text (Fixtures/lt-*.json),
/// and on hand-written answers for the rules a recording cannot show.
/// </summary>
public class LanguageToolEngineTests
{
    static readonly SymSpellCorrector WordLists = SymSpellCorrector.FromEmbeddedLexicons();

    static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    static (LanguageToolEngine Engine, FakeLanguageTool Fake, LanguageToolServer Server) Ready(Func<string, HttpResponseMessage> check)
    {
        var fake = new FakeLanguageTool();
        var server = fake.Server();
        server.Start();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Ready);
        fake.Check = check;
        return (new LanguageToolEngine(server, WordLists), fake, server);
    }

    static CorrectionResult Recorded(string name, string language)
    {
        var (engine, fake, server) = Ready(_ => FakeLanguageTool.Json(Fixture("lt-" + name + ".json")));
        using (server)
        {
            var result = engine.Correct(Fixture("lt-" + name + ".txt"), language);
            Assert.Contains("language=" + (result.Language == "de" ? "de-DE" : "en-US"), fake.Requests[^1], StringComparison.Ordinal);
            return result;
        }
    }

    static CorrectionResult Answer(string text, string json, string language = "en")
    {
        var (engine, _, server) = Ready(_ => FakeLanguageTool.Json(json));
        using (server) return engine.Correct(text, language);
    }

    static string Match(int offset, int length, string? replacement, string rule = "MORFOLOGIK_RULE_EN_US", string category = "TYPOS") =>
        "{\"offset\":" + offset + ",\"length\":" + length + ",\"replacements\":"
        + (replacement == null ? "[]" : "[{\"value\":" + System.Text.Json.JsonSerializer.Serialize(replacement) + "}]")
        + ",\"rule\":{\"id\":\"" + rule + "\",\"category\":{\"id\":\"" + category + "\"}}}";

    static string Matches(params string[] matches) => "{\"matches\":[" + string.Join(",", matches) + "]}";

    [Fact]
    public void GermanRecordingFixesErrorsAfterAnEmojiAndLeavesNamesAndQuotesAlone()
    {
        var result = Recorded("de", "auto");
        Assert.Equal("de", result.Language);
        Assert.Equal(LanguageToolEngine.EngineId, result.Engine);
        Assert.Null(result.Notice);
        Assert.Equal(
            "Hallo zusammen, ich habe heute keine Zeit, aber morgen können wir uns treffen. Auf jeden Fall melde ich mich bei Herr Müllerr. "
            + "\U0001F600 Ich glaube dass wir das Meeting bald abschließen. Ich weiß nicht, ob das klappt. "
            + "Die Datei liegt unter C:\\daten\\berichtt.txt und auf www.beispiell.de.\n> Das ist eine zitirte Zeile.\nSag mir bitte Bescheid.",
            result.Text);
        Assert.Equal(7, result.Edits.Count);
    }

    [Fact]
    public void EnglishRecordingKeepsTitlesFlagsNamesAndQuotes()
    {
        var result = Recorded("en", "en");
        Assert.Equal(
            "I will receive the parcel tomorrow, and we have a lot of work. It's a nice day. Dear Smithe, please check www.exampel.com "
            + "and run it with --verboose. My friend Jonh said their going to the meeting.\n> This is a quoted lne.",
            result.Text);
    }

    [Fact]
    public void OnlyAllowedCategoriesAndNotTheFirstWordOfAFragment()
    {
        Assert.Equal("the meeting is at noon. It went well, and it was was fine... We will to it in order to finish it.", Recorded("en2", "en").Text);
        var german = Recorded("de2", "de");
        Assert.Equal(Fixture("lt-de2.txt"), german.Text);
        Assert.Empty(german.Edits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("teh")]
    [InlineData("the\nend")]
    public void AMatchWithoutAUsableReplacementIsDropped(string? replacement)
    {
        var result = Answer("I saw teh cat.", Matches(Match(6, 3, replacement)));
        Assert.Equal("I saw teh cat.", result.Text);
        Assert.Equal(LanguageToolEngine.EngineId, result.Engine);
    }

    [Fact]
    public void AMatchAcrossALineBreakIsDropped()
    {
        Assert.Equal("one\ntwo", Answer("one\ntwo", Matches(Match(0, 7, "one two", "SOME_RULE", "GRAMMAR"))).Text);
    }

    [Fact]
    public void ProtectedTextIsNotChanged()
    {
        const string text = "Mail anna@exampel.com or see https://exampel.com/teh, file_nmae, v2beta, my.confg.json, --verboose\n> teh quote";
        string At(string word, string replacement) => Match(text.IndexOf(word, StringComparison.Ordinal), word.Length, replacement);
        var json = Matches(At("exampel.com", "example.com"), At("teh,", "the,"), At("file_nmae", "file_name"), At("v2beta", "v2 beta"),
            At("confg", "config"), At("verboose", "verbose"), At("teh quote", "the quote"));
        Assert.Equal(text, Answer(text, json).Text);
        Assert.Equal("See the cat.", Answer("See teh cat.", Matches(Match(4, 3, "the"))).Text);
    }

    [Fact]
    public void OverlappingMatchesKeepTheFirst()
    {
        var result = Answer("I saw teh cat.", Matches(Match(6, 7, "the dog", "X", "GRAMMAR"), Match(6, 3, "the")));
        Assert.Equal("I saw the dog.", result.Text);
        Assert.Single(result.Edits);
    }

    [Fact]
    public void SpellerNamePolicy()
    {
        // A word of the other language, a capital inside the word, and an English capital inside a sentence stay.
        const string english = "The Meeting with iPhoen user Jonh.";
        Assert.Equal(english, Answer(english, Matches(Match(4, 7, "Meetings"), Match(17, 6, "iPhone"), Match(29, 4, "John"))).Text);
        // A non-speller rule is not subject to the name policy.
        Assert.Equal("The meeting.", Answer("The Meeting.", Matches(Match(4, 7, "meeting", "SOME_CASE_RULE", "CASING"))).Text);

        // German: a capitalised word inside a sentence is fixed only when the suggestion is one edit away
        // after removing spaces, hyphens and case.
        const string german = "Gestern kam der Brieftraeger und Brzenk mit Aufjedenfall.";
        var result = Answer(german, Matches(
            Match(16, 12, "Briefträger", "GERMAN_SPELLER_RULE"),
            Match(33, 6, "Bank", "GERMAN_SPELLER_RULE"),
            Match(44, 12, "Auf jeden Fall", "GERMAN_SPELLER_RULE")), "de");
        Assert.Equal("Gestern kam der Brieftraeger und Brzenk mit Auf jeden Fall.", result.Text);
        Assert.Equal("Gestern kam der Briefträger.", Answer("Gestern kam der Briefträgr.", Matches(Match(16, 10, "Briefträger", "GERMAN_SPELLER_RULE")), "de").Text);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(12, 5)]
    [InlineData(0, -2)]
    public void AnOffsetOutsideTheTextFallsBackToTheWordLists(int offset, int length)
    {
        var result = Answer("I saw teh cat.", Matches(Match(6, 3, "the"), Match(offset, length, "x")));
        Assert.Equal(SymSpellCorrector.EngineId, result.Engine);
        Assert.Equal(LanguageToolEngine.FailedRequestNotice, result.Notice);
        Assert.Equal("I saw the cat.", result.Text);
    }

    [Fact]
    public void AFailedOrSlowRequestFallsBackToTheWordLists()
    {
        var (engine, fake, server) = Ready(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using (server)
        {
            var failed = engine.Correct("I saw teh cat.", "en");
            Assert.Equal(SymSpellCorrector.EngineId, failed.Engine);
            Assert.Equal(LanguageToolEngine.FailedRequestNotice, failed.Notice);
            Assert.Equal("I saw the cat.", failed.Text);

            fake.Check = _ => FakeLanguageTool.Json(Matches());
            fake.CheckDelay = TimeSpan.FromSeconds(5);
            var slow = new LanguageToolEngine(server, WordLists) { RequestTimeout = TimeSpan.FromMilliseconds(100) };
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Assert.Equal(SymSpellCorrector.EngineId, slow.Correct("I saw teh cat.", "en").Engine);
            Assert.True(clock.ElapsedMilliseconds < 1500);
        }
    }

    [Fact]
    public void WhileStartingTheWordListsAreUsedWithOneNoticePerStart()
    {
        var fake = new FakeLanguageTool { Listening = false };
        using var server = fake.Server();
        server.Start();
        var engine = new LanguageToolEngine(server, WordLists);
        var first = engine.Correct("I saw teh cat.", "en");
        Assert.Equal("I saw the cat.", first.Text);
        Assert.Equal(SymSpellCorrector.EngineId, first.Engine);
        Assert.Equal(LanguageToolEngine.StartingNotice, first.Notice);
        Assert.Null(engine.Correct("I saw teh cat.", "en").Notice);

        server.Stop();
        Assert.Null(engine.Correct("I saw teh cat.", "en").Notice);
        server.Start();
        Assert.Equal(LanguageToolEngine.StartingNotice, engine.Correct("I saw teh cat.", "en").Notice);
    }

    [Fact]
    public void NotInstalledUsesTheWordListsQuietly()
    {
        var fake = new FakeLanguageTool();
        using var server = fake.Server(FakeLanguageTool.Install(complete: false));
        server.Start();
        var result = new LanguageToolEngine(server, WordLists).Correct("I saw teh cat.", "en");
        Assert.Equal("I saw the cat.", result.Text);
        Assert.Null(result.Notice);
    }
}
