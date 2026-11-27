using System.Diagnostics;
using LocalCorrect.Engine;

namespace LocalCorrect.Tests;

public class SymSpellAlgorithmTests
{
    readonly SymSpellCorrector _corrector = new(
        "the 1000\nThe 1000\ncat 400\nCat 400\nfur 20\nFur 20\n",
        "und 1000\nUnd 1000\nfür 800\nFür 800\ndie 500\nDie 500\n");

    [Fact]
    public void OneSwapAndAKnownWord()
    {
        Assert.Equal("the cat", _corrector.Correct("teh cat", "en").Text);
        Assert.Equal("the cat", _corrector.Correct("the cat", "en").Text);
    }

    [Fact]
    public void UrlsAreNotEdited()
    {
        const string text = "see http://teh.example/cat";
        Assert.Equal(text, _corrector.Correct(text, "en").Text);
    }

    [Fact]
    public void GermanUmlautStillFixesAWordThatIsValidEnglish()
    {
        Assert.Equal("für", _corrector.Correct("fur", "de").Text);
        Assert.Equal("fur", _corrector.Correct("fur", "en").Text);
    }

    [Fact]
    public void EditOffsetsPointAtTheOriginalText()
    {
        const string text = "teh cat";
        var result = _corrector.Correct(text, "en");
        Assert.NotEmpty(result.Edits);
        foreach (var edit in result.Edits)
            Assert.Equal(edit.Original, text.Substring(edit.Offset, edit.Length));
    }

    [Fact]
    public void EmptyTextAndOverlongText()
    {
        Assert.Equal("", _corrector.Correct("", "auto").Text);
        Assert.Throws<LocalCorrect.Core.LocalEngineException>(() => _corrector.Correct(new string('a', 200_001), "en"));
    }
}

public class SymSpellLexiconTests
{
    static readonly string[][] Fixes =
    {
        new[] { "I recieve teh email yesterday and wil anser it tomorow.", "I receive the email yesterday and will answer it tomorrow." },
        new[] { "Thsi is a realy importnat mesage abuot our project.", "This is a really important message about our project." },
        new[] { "Can you plese send me the documnet?", "Can you please send me the document?" },
        new[] { "i dont think we can make it, im sorry.", "I don't think we can make it, I'm sorry." },
        new[] { "We shoud definately meet nxet week.", "We should definitely meet next week." },
        new[] { "Ich habe die Email gestren bekomen und werde morgne antworen.", "Ich habe die Email gestern bekommen und werde morgen antworten." },
        new[] { "Könnten Sie mir bitte das Dokumnet schiken?", "Könnten Sie mir bitte das Dokument schicken?" },
        new[] { "Vielen Dnak fur Ihre schnelel Antwrot.", "Vielen Dank für Ihre schnelle Antwort." },
        new[] { "Ich wuerde gerne ueber das Thema reden.", "Ich würde gerne über das Thema reden." },
        new[] { "Das haus ist gross.", "Das Haus ist groß." },
        new[] { "Wir haben das Problem leider noch nicht gelöst, aber wir arbieten daran.", "Wir haben das Problem leider noch nicht gelöst, aber wir arbeiten daran." }
    };

    static readonly string[] Unchanged =
    {
        "Meeting with Sam Markus and Joahnnes at https://exmaple.com/teh-page.",
        "Mail teh.user@exmaple.com or ping @tehdev about #recieve.",
        "Run npm install --save-dev and check src/utils/helpr.js, then call getUsr(id).",
        "Version 2.0.1 ships on 15.11.2026 with ID abc123.",
        "> I recieve teh quoted line\nThanks!",
        "NASA and the EU use HTML, JSON and OAuth.",
        "Hallo Sam, wie gehts? Ich freue mich auf die Kundenanfrage zur Rechnungsadresse.",
        "Die Projektbesprechungstermine sind am Montag, Grüße an Herrn Schmidtke.",
        "Das Deployment im Büro ist fertig, das Meeting war gut.",
        "This is a perfectly normal sentence without any mistakes.",
        "Dies ist ein ganz normaler Satz ohne Fehler."
    };

    public static SymSpellCorrector Shared { get; } = SymSpellCorrector.FromEmbeddedLexicons();

    [Fact]
    public void WordListsLoaded()
    {
        Assert.True(Shared.EnglishWords > 50_000);
        Assert.True(Shared.GermanWords > 50_000);
    }

    [Fact]
    public void FixesCommonTypos()
    {
        foreach (var pair in Fixes)
        {
            var got = Shared.Correct(pair[0], "auto").Text;
            Assert.True(got == pair[1], "input: " + pair[0] + "\n got: " + got + "\nwant: " + pair[1]);
        }
    }

    [Fact]
    public void LeavesNamesLinksCodeNumbersQuotesAndValidTextAlone()
    {
        foreach (var input in Unchanged)
            Assert.True(Shared.Correct(input, "auto").Text == input, input);
    }

    [Fact]
    public void LanguageDetectionAndForcedLanguage()
    {
        Assert.Equal("de", Shared.Correct("Das ist gut, danke für alles.", "auto").Language);
        Assert.Equal("en", Shared.Correct("This is good, thanks for everything.", "auto").Language);
        Assert.Equal("Das ist gut, danke für alles.", Shared.Correct("Das ist gut, danke fur alles.", "auto").Text);
        Assert.Equal("Das ist gut, danke fur alles.", Shared.Correct("Das ist gut, danke fur alles.", "en").Text);
    }

    [Fact]
    public void EditOffsetsPointIntoTheOriginalText()
    {
        const string text = "A teh B wil C";
        var result = Shared.Correct(text, "en");
        Assert.NotEmpty(result.Edits);
        foreach (var edit in result.Edits)
            Assert.Equal(edit.Original, text.Substring(edit.Offset, edit.Length));
    }

    [Fact]
    public void TwoLeadingCapitalsAreASlipButOtherInnerCapitalsAreNames()
    {
        Assert.Equal("The cat sat. I said Hello.", Shared.Correct("THe cat sat. I said HEllo.", "en").Text);
        Assert.Equal("Danke, wir haben das Problem gelöst.", Shared.Correct("DAnke, wir haben das PRoblem gelöst.", "de").Text);
        foreach (var input in new[] { "Our IDs and PCs are fine.", "My iPhone at McDonald was ABCd." })
            Assert.Equal(input, Shared.Correct(input, "en").Text);
    }

    [Fact]
    public void WindowsNewlinesStayPut()
    {
        Assert.Equal("the\r\ncat", Shared.Correct("teh\r\ncat", "en").Text);
    }

    [Fact]
    public void AThousandWordEmailFinishesPromptly()
    {
        var email = string.Concat(Enumerable.Repeat("Hallo zusammen, ich habe die Unterlagen gestren bekomen und schike sie morgne weiter. ", 80));
        var clock = Stopwatch.StartNew();
        var result = Shared.Correct(email, "auto");
        clock.Stop();
        Assert.Contains("gestern", result.Text, StringComparison.Ordinal);
        Assert.True(clock.ElapsedMilliseconds < 5000, "correction took " + clock.ElapsedMilliseconds + " ms");
    }
}
