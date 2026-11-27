using LocalCorrect.Core;

namespace LocalCorrect.Tests;

public class PreviewSegmentsTests
{
    const SegmentKind Same = SegmentKind.Same, Removed = SegmentKind.Removed, Inserted = SegmentKind.Inserted;

    static CorrectionEdit Edit(string text, string wrong, string right) => new(text.IndexOf(wrong, StringComparison.Ordinal), wrong.Length, wrong, right);

    /// <summary>Builds the segments and checks what every preview relies on: they spell the original and the pasted text.</summary>
    static IReadOnlyList<PreviewSegment> Build(string original, string corrected, params CorrectionEdit[] edits)
    {
        var segments = PreviewSegments.Build(original, corrected, edits);
        Assert.Equal(original, string.Concat(segments.Where(s => s.Kind != Inserted).Select(s => s.Text)));
        Assert.Equal(corrected, string.Concat(segments.Where(s => s.Kind != Removed).Select(s => s.Text)));
        Assert.DoesNotContain(segments, s => s.Text.Length == 0);
        for (var i = 1; i < segments.Count; i++)
        {
            Assert.NotEqual(segments[i - 1].Kind, segments[i].Kind);
            // Removed text is always shown before its replacement.
            Assert.False(segments[i - 1].Kind == Inserted && segments[i].Kind == Removed);
        }
        return segments;
    }

    static IReadOnlyList<PreviewSegment> BuildFromEdits(string original, params CorrectionEdit[] edits) =>
        Build(original, CorrectionEdit.Apply(original, edits), edits);

    [Fact]
    public void ChangedWordsAreShownWholeAndInPlace()
    {
        var text = "Ich habe keine zeit, aber morgen könen wir.";
        var segments = BuildFromEdits(text, Edit(text, "zeit", "Zeit"), Edit(text, "könen", "können"));
        Assert.Equal(new PreviewSegment[]
        {
            new(Same, "Ich habe keine "), new(Removed, "zeit"), new(Inserted, "Zeit"), new(Same, ", aber morgen "),
            new(Removed, "könen"), new(Inserted, "können"), new(Same, " wir.")
        }, segments);
        Assert.Equal(2, PreviewSegments.CountChanges(segments));
    }

    [Fact]
    public void OnlyTheChangedPartOfAnEditIsMarked()
    {
        // LanguageTool reports a missing comma as "weiß" to "weiß,": only the comma is new.
        var text = "Ich weiß ob das klappt";
        var segments = BuildFromEdits(text, Edit(text, "weiß", "weiß,"));
        Assert.Equal(new PreviewSegment[] { new(Same, "Ich weiß"), new(Inserted, ","), new(Same, " ob das klappt") }, segments);
    }

    [Fact]
    public void EmptyReplacementIsARemovalAndEmptyOriginalAnInsertion()
    {
        var text = "the the cat sat";
        var segments = BuildFromEdits(text, new CorrectionEdit(4, 4, "the ", ""), new CorrectionEdit(11, 0, "", " down"));
        Assert.Equal(new PreviewSegment[] { new(Same, "the "), new(Removed, "the "), new(Same, "cat"), new(Inserted, " down"), new(Same, " sat") }, segments);
        Assert.Equal(2, PreviewSegments.CountChanges(segments));
    }

    [Fact]
    public void LineBreaksStayWhole()
    {
        var text = "Erste zeile.\r\nZweite zeile.\r\n";
        var segments = BuildFromEdits(text, new CorrectionEdit(6, 5, "zeile", "Zeile"), new CorrectionEdit(21, 5, "zeile", "Zeile"));
        Assert.Equal(".\r\nZweite ", segments[3].Text);
        Assert.Equal(".\r\n", segments[^1].Text);
        // A break that changes is replaced as one piece, never as a lone \r or \n.
        var rewritten = Build("one \r\ntwo", "one\r\ntwo", new CorrectionEdit(0, 9, "one \r\ntwo", "one\r\ntwo"));
        Assert.Equal(new PreviewSegment[] { new(Same, "one"), new(Removed, " \r\n"), new(Inserted, "\r\n"), new(Same, "two") }, rewritten);
    }

    [Fact]
    public void EmojiCountAsTwoUnitsAndAreNeverSplit()
    {
        var text = "Gut 😀 aber keine zeit 👍🏽";
        var segments = BuildFromEdits(text, Edit(text, "zeit", "Zeit"));
        Assert.Equal("Gut 😀 aber keine ", segments[0].Text);
        Assert.Equal(" 👍🏽", segments[^1].Text);
        // One emoji replaced by another that shares its first surrogate: both are shown whole.
        var swapped = Build("ok 😀 ok", "ok 😁 ok", new CorrectionEdit(0, 8, "ok 😀 ok", "ok 😁 ok"));
        Assert.Equal(new PreviewSegment[] { new(Same, "ok "), new(Removed, "😀"), new(Inserted, "😁"), new(Same, " ok") }, swapped);
    }

    [Fact]
    public void AWholeTextEditShowsOnlyItsChangedWords()
    {
        // The local model returns one edit for the whole text.
        var text = "i will recieve the parcel tommorow.";
        var corrected = "I will receive the parcel tomorrow.";
        var segments = Build(text, corrected, new CorrectionEdit(0, text.Length, text, corrected));
        Assert.Equal(new PreviewSegment[]
        {
            new(Removed, "i"), new(Inserted, "I"), new(Same, " will "), new(Removed, "recieve"), new(Inserted, "receive"),
            new(Same, " the parcel "), new(Removed, "tommorow"), new(Inserted, "tomorrow"), new(Same, ".")
        }, segments);
        Assert.Equal(3, PreviewSegments.CountChanges(segments));
    }

    [Fact]
    public void AWordSplitInTwoIsOneInsertionEvenBesideOtherChanges()
    {
        var text = "we have alot of work and alot of tiem";
        var corrected = "we have a lot of work and a lot of time";
        var segments = Build(text, corrected, new CorrectionEdit(0, text.Length, text, corrected));
        Assert.Equal(new PreviewSegment[]
        {
            new(Same, "we have "), new(Removed, "alot"), new(Inserted, "a lot"), new(Same, " of work and "), new(Removed, "alot"), new(Inserted, "a lot"),
            new(Same, " of "), new(Removed, "tiem"), new(Inserted, "time")
        }, segments);
        Assert.Equal(new PreviewSegment[] { new(Removed, "a lot"), new(Inserted, "alot"), new(Same, " of work and "), new(Removed, "a lot"), new(Inserted, "alot"), new(Same, " of") },
            Build("a lot of work and a lot of", "alot of work and alot of", new CorrectionEdit(0, 26, "a lot of work and a lot of", "alot of work and alot of")));
    }

    [Fact]
    public void EditsThatDoNotAddUpAreReplacedByAComparison()
    {
        var text = "We saw teh cat and teh dog.";
        var corrected = "We saw the cat and the dog.";
        var first = new CorrectionEdit(7, 3, "teh", "the");
        var second = new CorrectionEdit(19, 3, "teh", "the");
        var expected = BuildFromEdits(text, first, second);
        Assert.Equal(expected, Build(text, corrected, second, first)); // out of order
        Assert.Equal(expected, Build(text, corrected, first, new CorrectionEdit(8, 3, "eh ", "he "), second)); // overlapping
        Assert.Equal(expected, Build(text, corrected, first, new CorrectionEdit(19, 30, "teh", "the"))); // past the end
        Assert.Equal(expected, Build(text, corrected, first)); // an edit is missing
    }

    [Fact]
    public void ALongTextWithATypoAtEachEndStillShowsOnlyTheTypos()
    {
        // 6,000 words in 60 lines: far beyond what one comparison table holds, so it is compared line by line.
        var middle = string.Join("\r\n", Enumerable.Repeat(string.Join(" ", Enumerable.Repeat("word", 100)), 60));
        var text = "teh start " + middle + " teh end";
        var corrected = "the start " + middle + " the end";
        var segments = Build(text, corrected, new CorrectionEdit(0, text.Length, text, corrected));
        Assert.Equal(new PreviewSegment[]
        {
            new(Removed, "teh"), new(Inserted, "the"), new(Same, " start " + middle + " "), new(Removed, "teh"), new(Inserted, "the"), new(Same, " end")
        }, segments);
    }

    [Fact]
    public void AVeryLongRewriteFallsBackToOneBlock()
    {
        // One line of 1,500 words, all different: the documented limit of the word-by-word comparison.
        var before = string.Join(" ", Enumerable.Range(0, 1500).Select(i => "a" + i));
        var after = string.Join(" ", Enumerable.Range(0, 1500).Select(i => "b" + i));
        var segments = Build(before, after, new CorrectionEdit(0, before.Length, before, after));
        Assert.Equal(new PreviewSegment[] { new(Removed, before), new(Inserted, after) }, segments);
    }

    [Fact]
    public void NoEditsIsOneUnchangedSegment()
    {
        var segments = Build("Nothing wrong.", "Nothing wrong.");
        Assert.Equal(new PreviewSegment[] { new(Same, "Nothing wrong.") }, segments);
        Assert.Equal(0, PreviewSegments.CountChanges(segments));
        Assert.Empty(Build("", ""));
    }

    // ---- Doubts (W-11) ----

    const SegmentKind Doubt = SegmentKind.Doubt;

    static CorrectionDoubt Unsure(string text, string word, params string[] suggestions) =>
        new(text.IndexOf(word, StringComparison.Ordinal), word.Length, word, suggestions);

    [Fact]
    public void DoubtsAreShownInPlaceAndAChoiceBecomesAChange()
    {
        const string text = "Ask Jonh about teh plan, Smithe.";
        var edits = new[] { Edit(text, "teh", "the") };
        var corrected = CorrectionEdit.Apply(text, edits);
        var doubts = new[] { Unsure(text, "Jonh", "John", "Jon"), Unsure(text, "Smithe", "Smith") };

        var open = PreviewSegments.Build(text, corrected, edits, doubts);
        Assert.Equal(new PreviewSegment[]
        {
            new(Same, "Ask "), new(Doubt, "Jonh", 0), new(Same, " about "), new(Removed, "teh"), new(Inserted, "the"),
            new(Same, " plan, "), new(Doubt, "Smithe", 1), new(Same, ".")
        }, open);
        Assert.Equal(1, PreviewSegments.CountChanges(open));
        Assert.Equal(2, PreviewSegments.CountDoubts(open));
        Assert.Equal(corrected, PreviewSegments.Compose(text, corrected, edits, doubts, new string?[2]));

        var choices = new string?[] { "Jon", null };
        var chosen = PreviewSegments.Build(text, corrected, edits, doubts, choices);
        Assert.Equal(new PreviewSegment[] { new(Same, "Ask "), new(Removed, "Jonh", 0), new(Inserted, "Jon", 0), new(Same, " about ") }, chosen.Take(4));
        Assert.Equal(2, PreviewSegments.CountChanges(chosen));
        Assert.Equal(1, PreviewSegments.CountDoubts(chosen));
        Assert.Equal("Ask Jon about the plan, Smithe.", PreviewSegments.Compose(text, corrected, edits, doubts, choices));
        Assert.Equal("Ask Jon about the plan, Smith.", PreviewSegments.Compose(text, corrected, edits, doubts, new[] { "Jon", "Smith" }));
        // Only a suggestion of that doubt is pasted.
        Assert.Equal(corrected, PreviewSegments.Compose(text, corrected, edits, doubts, new[] { "Jo hn", "x" }));
    }

    [Fact]
    public void DoubtsThatDoNotFitTheTextAreDropped()
    {
        const string text = "Ask Jonh about teh plan.";
        var edits = new[] { Edit(text, "teh", "the") };
        var corrected = CorrectionEdit.Apply(text, edits);
        var doubts = new[]
        {
            new CorrectionDoubt(4, 4, "John", new[] { "x" }),          // not the text at that place
            new CorrectionDoubt(14, 5, " teh ", new[] { "x" }),       // overlaps the edit
            new CorrectionDoubt(20, 9, "plan.", new[] { "x" }),       // past the end
            new CorrectionDoubt(4, 4, "Jonh", Array.Empty<string>()), // nothing to offer
            new CorrectionDoubt(4, 4, "Jonh", new[] { "John" }),
            new CorrectionDoubt(5, 2, "on", new[] { "an" })           // overlaps the doubt before
        };
        var segments = PreviewSegments.Build(text, corrected, edits, doubts);
        Assert.Equal(new[] { 4 }, segments.Where(s => s.Kind == Doubt).Select(s => s.Doubt));
        Assert.Equal("Ask John about the plan.", PreviewSegments.Compose(text, corrected, edits, doubts, new[] { "x", "x", "x", null, "John", "an" }));

        // Edits that do not add up to the corrected text (the local model) show no doubts at all.
        var rewrite = new[] { new CorrectionEdit(0, 3, "Ask", "Tell") };
        Assert.DoesNotContain(PreviewSegments.Build(text, "Tell Jonh about the plan.", rewrite, doubts), s => s.Kind == Doubt);
        Assert.Equal("Tell Jonh about the plan.", PreviewSegments.Compose(text, "Tell Jonh about the plan.", rewrite, doubts, new[] { null, null, null, null, "John", null }));
    }
}
