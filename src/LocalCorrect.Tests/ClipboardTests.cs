using LocalCorrect.Core;

namespace LocalCorrect.Tests;

public sealed class MemoryClipboard : IClipboardAccess
{
    readonly List<ClipboardEntry> _entries = new();
    int _failedOpens;

    public uint SequenceNumber { get; private set; } = 1;
    public bool IsOpen { get; private set; }
    public int FailOpenTimes { get; set; }

    public bool TryOpen()
    {
        if (IsOpen) return false;
        if (_failedOpens < FailOpenTimes)
        {
            _failedOpens++;
            return false;
        }
        IsOpen = true;
        return true;
    }

    public void Close() => IsOpen = false;

    public IReadOnlyList<ClipboardEntry> ReadAll()
    {
        if (!IsOpen) throw new InvalidOperationException("The clipboard is not open.");
        var copy = new ClipboardEntry[_entries.Count];
        for (var i = 0; i < _entries.Count; i++) copy[i] = _entries[i].Clone();
        return copy;
    }

    public void WriteAll(IReadOnlyList<ClipboardEntry> entries)
    {
        if (!IsOpen) throw new InvalidOperationException("The clipboard is not open.");
        _entries.Clear();
        foreach (var entry in entries) _entries.Add(entry.Clone());
        SequenceNumber++;
    }
}

public class ClipboardTests
{
    static readonly Action<int> NoWait = _ => { };

    [Fact]
    public void UnicodeTextRoundTripsWithEmojiAndUmlauts()
    {
        const string text = "für 😀\r\nline";
        var decoded = ClipboardText.Decode(ClipboardText.Encode(text));
        Assert.Equal(text, decoded);
    }

    [Fact]
    public void EmptyTextRoundTrips()
    {
        Assert.Equal("", ClipboardText.Decode(ClipboardText.Encode("")));
        Assert.Equal("", ClipboardText.Decode(Array.Empty<byte>()));
    }

    [Fact]
    public void MemoryFormatsAreTheOnesThatCanBeRestored()
    {
        Assert.True(ClipboardFormatPolicy.IsMemoryFormat(ClipboardFormatPolicy.UnicodeText));
        Assert.True(ClipboardFormatPolicy.IsMemoryFormat(ClipboardFormatPolicy.Text));
        Assert.True(ClipboardFormatPolicy.IsMemoryFormat(ClipboardFormatPolicy.Dib));
        Assert.True(ClipboardFormatPolicy.IsMemoryFormat(0xC100));
        Assert.False(ClipboardFormatPolicy.IsMemoryFormat(2));
        Assert.False(ClipboardFormatPolicy.IsMemoryFormat(3));
        Assert.False(ClipboardFormatPolicy.IsMemoryFormat(14));
    }

    [Fact]
    public void CaptureDropsHandleFormatsAndKeepsCustomBytes()
    {
        var clip = new MemoryClipboard();
        var custom = new byte[] { 0, 1, 2, 255, 0, 10 };
        ClipboardSession.WriteText(clip, "secret", attempts: 1, wait: NoWait);
        var archive = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        var withBitmap = archive.Entries.Append(new ClipboardEntry(2, new byte[] { 9, 9 })).Append(new ClipboardEntry(0xC100, custom)).ToList();
        ClipboardSession.Restore(clip, new ClipboardArchive(withBitmap), attempts: 1, wait: NoWait);

        var saved = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        Assert.Equal(2, saved.Entries.Count);
        Assert.Equal(ClipboardFormatPolicy.UnicodeText, saved.Entries[0].Format);
        Assert.Equal("secret", ClipboardText.Decode(saved.Entries[0].Data));
        Assert.Equal(0xC100u, saved.Entries[1].Format);
        Assert.Equal(custom, saved.Entries[1].Data);
    }

    [Fact]
    public void WriteTextThenRestorePutsEveryFormatBack()
    {
        var clip = new MemoryClipboard();
        var custom = new byte[] { 7, 0, 7 };
        ClipboardSession.WriteText(clip, "before", attempts: 1, wait: NoWait);
        var seeded = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        ClipboardSession.Restore(clip, new ClipboardArchive(seeded.Entries.Append(new ClipboardEntry(0xC200, custom)).ToList()), attempts: 1, wait: NoWait);
        var original = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);

        ClipboardSession.WriteText(clip, "corrected", attempts: 1, wait: NoWait);
        Assert.Equal("corrected", ClipboardSession.ReadUnicode(clip, attempts: 1, wait: NoWait));
        var during = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        Assert.Single(during.Entries);

        ClipboardSession.Restore(clip, original, attempts: 1, wait: NoWait);
        Assert.Equal("before", ClipboardSession.ReadUnicode(clip, attempts: 1, wait: NoWait));
        var after = ClipboardSession.Capture(clip, attempts: 1, wait: NoWait);
        Assert.Equal(custom, Assert.Single(after.Entries, entry => entry.Format == 0xC200).Data);
    }

    [Fact]
    public void BusyClipboardFailsAfterTheRetries()
    {
        var clip = new MemoryClipboard { FailOpenTimes = 20 };
        Assert.Throws<ClipboardBusyException>(() => ClipboardSession.Capture(clip, attempts: 3, wait: NoWait));
    }

    [Fact]
    public void OpenSucceedsAfterAFewBusyAttempts()
    {
        var clip = new MemoryClipboard { FailOpenTimes = 2 };
        ClipboardSession.WriteText(clip, "ok", attempts: 5, wait: NoWait);
        Assert.Equal("ok", ClipboardSession.ReadUnicode(clip, attempts: 1, wait: NoWait));
    }

    [Fact]
    public void OversizedSnapshotIsRefusedBeforeAnythingIsReplaced()
    {
        var clip = new MemoryClipboard();
        ClipboardSession.WriteText(clip, "keep", attempts: 1, wait: NoWait);
        var big = new ClipboardArchive(new[] { new ClipboardEntry(ClipboardFormatPolicy.UnicodeText, new byte[32]) });
        ClipboardSession.Restore(clip, big, attempts: 1, wait: NoWait);
        Assert.Throws<ClipboardTooLargeException>(() => ClipboardSession.Capture(clip, attempts: 1, wait: NoWait, maxBytes: 8));
        Assert.Equal(32, ClipboardSession.Capture(clip, attempts: 1, wait: NoWait, maxBytes: 100).Entries[0].Data.Length);
    }
}
