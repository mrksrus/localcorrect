namespace LocalCorrect.Core;

/// <summary>
/// Saves every clipboard format we can round-trip, then puts that snapshot back
/// after a correction. The Windows clipboard object and the in-memory test double
/// both sit behind <see cref="IClipboardAccess"/>.
/// </summary>
public static class ClipboardSession
{
    public const int DefaultAttempts = 8;
    public const int DefaultMaxBytes = 48 * 1024 * 1024;

    public static ClipboardArchive Capture(IClipboardAccess clipboard, int attempts = DefaultAttempts, Action<int>? wait = null, int maxBytes = DefaultMaxBytes)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (clipboard.TryOpen())
            {
                try
                {
                    var entries = clipboard.ReadAll();
                    long total = 0;
                    var kept = new List<ClipboardEntry>(entries.Count);
                    foreach (var entry in entries)
                    {
                        if (!ClipboardFormatPolicy.IsMemoryFormat(entry.Format)) continue;
                        total += entry.Data.LongLength;
                        if (total > maxBytes) throw new ClipboardTooLargeException();
                        kept.Add(entry);
                    }
                    return new ClipboardArchive(kept);
                }
                finally
                {
                    clipboard.Close();
                }
            }
            if (attempt + 1 < attempts) Pause(wait, 30);
        }
        throw new ClipboardBusyException();
    }

    public static void Restore(IClipboardAccess clipboard, ClipboardArchive archive, int attempts = DefaultAttempts, Action<int>? wait = null)
    {
        ArgumentNullException.ThrowIfNull(archive);
        WriteEntries(clipboard, archive.Entries, attempts, wait);
    }

    public static void WriteText(IClipboardAccess clipboard, string text, int attempts = DefaultAttempts, Action<int>? wait = null)
    {
        var entry = new ClipboardEntry(ClipboardFormatPolicy.UnicodeText, ClipboardText.Encode(text));
        WriteEntries(clipboard, new[] { entry }, attempts, wait);
    }

    public static string? ReadUnicode(IClipboardAccess clipboard, int attempts = DefaultAttempts, Action<int>? wait = null)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (clipboard.TryOpen())
            {
                try
                {
                    foreach (var entry in clipboard.ReadAll())
                    {
                        if (entry.Format == ClipboardFormatPolicy.UnicodeText)
                            return ClipboardText.Decode(entry.Data);
                    }
                    return null;
                }
                finally
                {
                    clipboard.Close();
                }
            }
            if (attempt + 1 < attempts) Pause(wait, 30);
        }
        throw new ClipboardBusyException();
    }

    static void WriteEntries(IClipboardAccess clipboard, IReadOnlyList<ClipboardEntry> entries, int attempts, Action<int>? wait)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (clipboard.TryOpen())
            {
                try
                {
                    clipboard.WriteAll(entries);
                    return;
                }
                finally
                {
                    clipboard.Close();
                }
            }
            if (attempt + 1 < attempts) Pause(wait, 30);
        }
        throw new ClipboardBusyException();
    }

    static void Pause(Action<int>? wait, int milliseconds)
    {
        if (wait != null) wait(milliseconds);
        else Thread.Sleep(milliseconds);
    }
}
