namespace LocalCorrect.Core;

public sealed class HotkeySpec
{
    public bool Ctrl { get; set; } = true;
    public bool Alt { get; set; } = true;
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = "L";

    public static HotkeySpec Default { get; } = new();

    public bool IsValid
    {
        get
        {
            if (!Ctrl && !Alt) return false;
            if (!VirtualKeys.TryGet(Key, out _)) return false;
            if (Alt && !Ctrl && !Shift && !Win && string.Equals(VirtualKeys.Normalize(Key), "F4", StringComparison.Ordinal))
                return false;
            return true;
        }
    }

    /// <summary>Win32 MOD_* bits, without MOD_NOREPEAT. The app adds that when it registers.</summary>
    public uint NativeModifiers
    {
        get
        {
            uint value = 0;
            if (Alt) value |= 1;
            if (Ctrl) value |= 2;
            if (Shift) value |= 4;
            if (Win) value |= 8;
            return value;
        }
    }

    public string Display
    {
        get
        {
            var parts = new List<string>(5);
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(VirtualKeys.Normalize(Key));
            return string.Join("+", parts);
        }
    }

    public HotkeySpec Clone() => new()
    {
        Ctrl = Ctrl,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        Key = Key
    };
}
