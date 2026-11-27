namespace LocalCorrect.Core;

public sealed class CorrectionEdit
{
    public CorrectionEdit(int offset, int length, string original, string replacement)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        Offset = offset;
        Length = length;
        Original = original ?? throw new ArgumentNullException(nameof(original));
        Replacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
    }

    public int Offset { get; }
    public int Length { get; }
    public string Original { get; }
    public string Replacement { get; }

    /// <summary>Applies non-overlapping edits, sorted by offset, to <paramref name="text"/>.</summary>
    public static string Apply(string text, IReadOnlyList<CorrectionEdit> edits)
    {
        var output = new System.Text.StringBuilder(text.Length + 16);
        var position = 0;
        foreach (var edit in edits)
        {
            output.Append(text, position, edit.Offset - position);
            output.Append(edit.Replacement);
            position = edit.Offset + edit.Length;
        }
        output.Append(text, position, text.Length - position);
        return output.ToString();
    }
}

/// <summary>
/// A word the engine would change but is not sure enough to (W-11): shown in the preview with its suggestions and
/// changed only when the user picks one. Offsets are in the original text.
/// </summary>
public sealed class CorrectionDoubt
{
    public CorrectionDoubt(int offset, int length, string original, IReadOnlyList<string> suggestions)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        Offset = offset;
        Length = length;
        Original = original ?? throw new ArgumentNullException(nameof(original));
        Suggestions = suggestions ?? throw new ArgumentNullException(nameof(suggestions));
    }

    public int Offset { get; }
    public int Length { get; }
    public string Original { get; }
    /// <summary>Up to three, best first.</summary>
    public IReadOnlyList<string> Suggestions { get; }
}

public sealed class CorrectionResult
{
    public CorrectionResult(string text, string language, IReadOnlyList<CorrectionEdit> edits, string engine = "", string? notice = null,
        IReadOnlyList<CorrectionDoubt>? doubts = null)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Language = language ?? throw new ArgumentNullException(nameof(language));
        Edits = edits ?? throw new ArgumentNullException(nameof(edits));
        Engine = engine ?? "";
        Notice = notice;
        Doubts = doubts ?? Array.Empty<CorrectionDoubt>();
    }

    public string Text { get; }
    public string Language { get; }
    public IReadOnlyList<CorrectionEdit> Edits { get; }

    /// <summary>Id of the engine that produced this result (the word lists when LanguageTool fell back).</summary>
    public string Engine { get; }

    /// <summary>Something the user should be told, such as "the word lists were used this time". Never contains the text.</summary>
    public string? Notice { get; }

    /// <summary>What the engine left alone but may be wrong; only the preview shows these.</summary>
    public IReadOnlyList<CorrectionDoubt> Doubts { get; }
}

/// <summary>
/// One spelling engine. The tray app talks only to this, so a better local model
/// can replace the built-in word lists without changing the hotkey or the clipboard.
/// Implementations must not send <paramref name="text"/> to any machine but this one.
/// </summary>
public interface ICorrectionEngine
{
    string Id { get; }
    string DisplayName { get; }
    CorrectionResult Correct(string text, string language);
}

/// <summary>
/// A failure whose message is safe to show and to write in the log.
/// The message must not contain the user's text.
/// </summary>
public sealed class LocalEngineException : Exception
{
    public LocalEngineException(string safeMessage) : base(safeMessage)
    {
    }
}
