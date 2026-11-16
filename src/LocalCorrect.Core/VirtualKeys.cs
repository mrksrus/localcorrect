namespace LocalCorrect.Core;

public static class VirtualKeys
{
    public static bool TryGet(string? key, out int virtualKey)
    {
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var name = key.Trim().ToUpperInvariant();
        if (name.Length == 1 && name[0] is >= 'A' and <= 'Z')
        {
            virtualKey = name[0];
            return true;
        }
        if (name.Length is >= 2 and <= 3 && name[0] == 'F'
            && int.TryParse(name.AsSpan(1), out var number)
            && number is >= 1 and <= 12)
        {
            virtualKey = 0x70 + (number - 1);
            return true;
        }
        return false;
    }

    public static string Normalize(string? key)
    {
        if (!TryGet(key, out var virtualKey)) return "L";
        if (virtualKey is >= 'A' and <= 'Z') return ((char)virtualKey).ToString();
        return "F" + (virtualKey - 0x70 + 1).ToString();
    }
}
