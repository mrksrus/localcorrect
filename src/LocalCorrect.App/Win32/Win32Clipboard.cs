using System.Runtime.InteropServices;
using LocalCorrect.Core;

namespace LocalCorrect.App.Win32;

/// <summary>
/// Clipboard access through the Win32 API so non-text formats can be copied as bytes and put back.
/// Bitmap and metafile formats are handles, not memory, and are skipped.
/// </summary>
internal sealed class Win32Clipboard : IClipboardAccess
{
    const int MaxFormatBytes = 32 * 1024 * 1024;
    public nint Owner { get; set; }

    public uint SequenceNumber => NativeMethods.GetClipboardSequenceNumber();

    public bool TryOpen() => NativeMethods.OpenClipboard(Owner);

    public void Close() => NativeMethods.CloseClipboard();

    public IReadOnlyList<ClipboardEntry> ReadAll()
    {
        var entries = new List<ClipboardEntry>();
        uint format = 0;
        while (true)
        {
            format = NativeMethods.EnumClipboardFormats(format);
            if (format == 0) break;
            if (!ClipboardFormatPolicy.IsMemoryFormat(format)) continue;
            var handle = NativeMethods.GetClipboardData(format);
            if (handle == 0) continue;
            var size = NativeMethods.GlobalSize(handle);
            if (size == 0) continue;
            if (size > MaxFormatBytes) throw new ClipboardTooLargeException();
            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == 0) continue;
            try
            {
                var bytes = new byte[(int)size];
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
                entries.Add(new ClipboardEntry(format, bytes));
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        return entries;
    }

    public void WriteAll(IReadOnlyList<ClipboardEntry> entries)
    {
        if (!NativeMethods.EmptyClipboard())
            throw new IOException("Could not empty the clipboard.");
        foreach (var entry in entries)
        {
            if (!ClipboardFormatPolicy.IsMemoryFormat(entry.Format) || entry.Data.Length == 0) continue;
            var memory = NativeMethods.GlobalAlloc(NativeMethods.GmemMoveable, (nuint)entry.Data.Length);
            if (memory == 0) throw new IOException("Could not allocate clipboard memory.");
            var pointer = NativeMethods.GlobalLock(memory);
            if (pointer == 0)
            {
                NativeMethods.GlobalFree(memory);
                throw new IOException("Could not lock clipboard memory.");
            }
            try
            {
                Marshal.Copy(entry.Data, 0, pointer, entry.Data.Length);
            }
            finally
            {
                NativeMethods.GlobalUnlock(memory);
            }
            if (NativeMethods.SetClipboardData(entry.Format, memory) == 0)
            {
                NativeMethods.GlobalFree(memory);
                throw new IOException("Could not write a clipboard format.");
            }
        }
    }
}
