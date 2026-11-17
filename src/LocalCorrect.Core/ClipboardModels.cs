namespace LocalCorrect.Core;

public sealed class ClipboardEntry
{
    public ClipboardEntry(uint format, byte[] data)
    {
        Format = format;
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public uint Format { get; }
    public byte[] Data { get; }

    public ClipboardEntry Clone() => new(Format, (byte[])Data.Clone());
}

public sealed class ClipboardArchive
{
    public ClipboardArchive(IReadOnlyList<ClipboardEntry> entries)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        var copy = new ClipboardEntry[entries.Count];
        for (var i = 0; i < entries.Count; i++) copy[i] = entries[i].Clone();
        Entries = copy;
    }

    public IReadOnlyList<ClipboardEntry> Entries { get; }
}

public static class ClipboardFormatPolicy
{
    public const uint Text = 1;
    public const uint OemText = 7;
    public const uint Dib = 8;
    public const uint UnicodeText = 13;
    public const uint HDrop = 15;
    public const uint Locale = 16;
    public const uint DibV5 = 17;
    public const uint RegisteredFormatBase = 0xC000;

    /// <summary>
    /// Formats we can snapshot as a block of memory and put back.
    /// GDI object formats (bitmap, metafile, palette) are handles, not bytes,
    /// and locking them as memory can crash, so they are left alone.
    /// </summary>
    public static bool IsMemoryFormat(uint format)
    {
        if (format >= RegisteredFormatBase) return true;
        return format is Text or OemText or Dib or UnicodeText or HDrop or Locale or DibV5;
    }
}

public static class ClipboardText
{
    public static byte[] Encode(string? text)
    {
        text ??= "";
        return System.Text.Encoding.Unicode.GetBytes(text + "\0");
    }

    public static string Decode(byte[] data)
    {
        if (data == null || data.Length < 2) return "";
        var length = data.Length - (data.Length % 2);
        if (length >= 2 && data[length - 1] == 0 && data[length - 2] == 0) length -= 2;
        return System.Text.Encoding.Unicode.GetString(data, 0, length);
    }
}

public interface IClipboardAccess
{
    uint SequenceNumber { get; }
    bool TryOpen();
    void Close();
    IReadOnlyList<ClipboardEntry> ReadAll();
    void WriteAll(IReadOnlyList<ClipboardEntry> entries);
}

/// <summary>Key sequences the no-selection fallback sends to the focused editor.</summary>
public enum SelectKeys
{
    /// <summary>Ctrl+Down, Ctrl+Up, Ctrl+Shift+Down: the paragraph and its end mark.</summary>
    Paragraph,
    /// <summary>Home, Shift+End: the line.</summary>
    Line,
    /// <summary>Shift+Left: drops the paragraph mark from the selection.</summary>
    DropLast,
    /// <summary>Right: collapses the selection to its end.</summary>
    Collapse
}

public interface ISelectionTransport
{
    void Copy();
    void Paste();
    void Select(SelectKeys keys);
}

public sealed class ClipboardBusyException : Exception
{
    public ClipboardBusyException() : base("The clipboard was busy.")
    {
    }
}

public sealed class ClipboardTooLargeException : Exception
{
    public ClipboardTooLargeException() : base("The clipboard is holding a very large item.")
    {
    }
}
