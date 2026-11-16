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
}

public sealed class CorrectionResult
{
    public CorrectionResult(string text, string language, IReadOnlyList<CorrectionEdit> edits)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Language = language ?? throw new ArgumentNullException(nameof(language));
        Edits = edits ?? throw new ArgumentNullException(nameof(edits));
    }

    public string Text { get; }
    public string Language { get; }
    public IReadOnlyList<CorrectionEdit> Edits { get; }
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
