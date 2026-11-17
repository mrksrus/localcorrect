using System.Globalization;

namespace LocalCorrect.Core;

public enum SegmentKind { Same, Removed, Inserted }

public readonly record struct PreviewSegment(SegmentKind Kind, string Text);

/// <summary>
/// Turns a text and its correction into the pieces the preview draws, in reading order: unchanged text,
/// removed text, inserted text. Same plus Removed is the original; Same plus Inserted is the text that is pasted.
/// </summary>
public static class PreviewSegments
{
    public static IReadOnlyList<PreviewSegment> Build(string original, string corrected, IReadOnlyList<CorrectionEdit> edits)
    {
        // The preview must show exactly what is pasted. Edits that do not add up to it are replaced by a comparison of the two texts.
        if (!AddUp(original, corrected, edits))
            edits = new[] { new CorrectionEdit(0, original.Length, original, corrected) };
        var output = new List<PreviewSegment>();
        var position = 0;
        foreach (var edit in edits)
        {
            Add(output, SegmentKind.Same, original.Substring(position, edit.Offset - position));
            CompareLines(output, original.Substring(edit.Offset, edit.Length), edit.Replacement);
            position = edit.Offset + edit.Length;
        }
        Add(output, SegmentKind.Same, original.Substring(position));
        return output;
    }

    /// <summary>Places where the text changes: a removal, an insertion, or a removal with its replacement.</summary>
    public static int CountChanges(IReadOnlyList<PreviewSegment> segments)
    {
        var count = 0;
        for (var i = 0; i < segments.Count; i++)
            if (segments[i].Kind != SegmentKind.Same && (i == 0 || segments[i - 1].Kind == SegmentKind.Same)) count++;
        return count;
    }

    static bool AddUp(string original, string corrected, IReadOnlyList<CorrectionEdit> edits)
    {
        var position = 0;
        foreach (var edit in edits)
        {
            if (edit.Offset < position || edit.Length > original.Length - edit.Offset) return false;
            position = edit.Offset + edit.Length;
        }
        return CorrectionEdit.Apply(original, edits) == corrected;
    }

    /// <summary>
    /// A rewrite that keeps its lines, which the local model must, is compared line by line. That keeps a long text
    /// with a typo at each end inside the limit of <see cref="Compare"/>.
    /// </summary>
    static void CompareLines(List<PreviewSegment> output, string before, string after)
    {
        string[] a = before.Split('\n'), b = after.Split('\n');
        if (a.Length != b.Length)
        {
            Compare(output, Tokens(before), Tokens(after));
            return;
        }
        for (var i = 0; i < a.Length; i++)
        {
            var lineEnd = i < a.Length - 1 ? "\n" : "";
            Compare(output, Tokens(a[i] + lineEnd), Tokens(b[i] + lineEnd));
        }
    }

    /// <summary>
    /// Compares word by word, so a changed word is shown whole ("zeit" struck, "Zeit" added) and a whole-text edit
    /// from the local model still shows only its changed words. Between two unchanged words, removals come first.
    /// </summary>
    static void Compare(List<PreviewSegment> output, List<string> before, List<string> after)
    {
        int start = 0, n = before.Count, m = after.Count;
        while (start < n && start < m && before[start] == after[start]) Add(output, SegmentKind.Same, before[start++]);
        var tail = 0;
        while (n - tail > start && m - tail > start && before[n - 1 - tail] == after[m - 1 - tail]) tail++;
        n -= tail + start;
        m -= tail + start;
        // shortcut: the table is n by m; a changed stretch beyond about 2,000 by 2,000 words is one removed and one added block. Needs a linear-space comparison to do better.
        if ((long)n * m > 4_000_000)
        {
            Add(output, SegmentKind.Removed, string.Concat(before.Skip(start).Take(n)));
            Add(output, SegmentKind.Inserted, string.Concat(after.Skip(start).Take(m)));
        }
        else
        {
            // common[i, j]: most words that the changed stretches share in order from i and j on.
            var common = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
                for (var j = m - 1; j >= 0; j--)
                    common[i, j] = before[start + i] == after[start + j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            var inserting = false;
            for (int i = 0, j = 0; i < n || j < m;)
            {
                // In the middle of an insertion, a word that could be kept is inserted too when a later one can be kept in its place.
                // That makes "alot" to "a lot" one insertion, not "a" and "lot " around the space.
                if (i < n && j < m && before[start + i] == after[start + j] && !(inserting && common[i, j + 1] == common[i, j]))
                {
                    Add(output, SegmentKind.Same, before[start + i]);
                    i++;
                    j++;
                    inserting = false;
                }
                else if (j == m || (i < n && common[i + 1, j] >= common[i, j + 1])) Add(output, SegmentKind.Removed, before[start + i++]);
                else
                {
                    Add(output, SegmentKind.Inserted, after[start + j++]);
                    inserting = true;
                }
            }
        }
        for (var i = before.Count - tail; i < before.Count; i++) Add(output, SegmentKind.Same, before[i]);
    }

    /// <summary>Words, runs of white space (so a CRLF stays whole), and single other characters. Never splits a surrogate pair or a combining mark from its base.</summary>
    static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        for (var i = 0; i < text.Length;)
        {
            var start = i;
            var kind = Kind(text[i++]);
            while (i < text.Length && (Joins(text[i]) || (kind != 0 && Kind(text[i]) == kind))) i++;
            tokens.Add(text.Substring(start, i - start));
        }
        return tokens;
    }

    static int Kind(char c) => char.IsLetterOrDigit(c) ? 1 : char.IsWhiteSpace(c) ? 2 : 0;

    static bool Joins(char c) => char.IsLowSurrogate(c) || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark
        or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format;

    static void Add(List<PreviewSegment> output, SegmentKind kind, string text)
    {
        if (text.Length == 0) return;
        if (output.Count > 0 && output[^1].Kind == kind) output[^1] = new PreviewSegment(kind, output[^1].Text + text);
        else output.Add(new PreviewSegment(kind, text));
    }
}
