using System.Diagnostics;
using System.Runtime.InteropServices;
using LocalCorrect.Core;

namespace LocalCorrect.App.Win32;

internal static class KeyboardChord
{
    const ushort Shift = 0x10, Ctrl = 0x11, End = 0x23, Home = 0x24, Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28;

    public static bool SendCopy() => Send(Chord(Ctrl, 0x43));
    public static bool SendPaste() => Send(Chord(Ctrl, 0x56));

    public static bool SendSelect(SelectKeys keys) => Send(keys switch
    {
        SelectKeys.Paragraph => Chord(Ctrl, Down).Concat(Chord(Ctrl, Up)).Concat(Chord(Ctrl, Shift, Down)),
        SelectKeys.Line => Chord(Home).Concat(Chord(Shift, End)),
        SelectKeys.DropLast => Chord(Shift, Left),
        _ => Chord(Right)
    });

    public static int InputSize => Marshal.SizeOf<INPUT>();

    /// <summary>
    /// Waits until the hotkey's keys are physically up, so a held letter does not auto-repeat into the target
    /// once the app's own key-ups have cleared the modifiers. False after the timeout.
    /// </summary>
    public static bool WaitForRelease(HotkeySpec hotkey, int timeoutMilliseconds)
    {
        VirtualKeys.TryGet(hotkey.Key, out var key);
        int[] keys = { key, 0x10, 0x11, 0x12, 0x5B, 0x5C };
        var clock = Stopwatch.StartNew();
        while (keys.Any(k => k != 0 && (NativeMethods.GetAsyncKeyState(k) & 0x8000) != 0))
        {
            if (clock.ElapsedMilliseconds >= timeoutMilliseconds) return false;
            Thread.Sleep(10);
        }
        return true;
    }

    /// <summary>Presses the keys in order and releases them in reverse.</summary>
    static IEnumerable<INPUT> Chord(params ushort[] keys) =>
        keys.Select(k => Key(k, up: false)).Concat(keys.Reverse().Select(k => Key(k, up: true)));

    static bool Send(IEnumerable<INPUT> sequence)
    {
        ReleaseModifiers();
        Thread.Sleep(30);
        var inputs = sequence.ToArray();
        return NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    static void ReleaseModifiers()
    {
        // Left and right Ctrl, Alt, Shift, and Win, then the generic versions.
        ushort[] keys = { 0xA2, 0xA3, 0xA4, 0xA5, 0xA0, 0xA1, 0x5B, 0x5C, 0x11, 0x12, 0x10 };
        var inputs = new INPUT[keys.Length];
        for (var i = 0; i < keys.Length; i++) inputs[i] = Key(keys[i], up: true);
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    static INPUT Key(ushort virtualKey, bool up) => new()
    {
        type = NativeMethods.InputKeyboard,
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = virtualKey,
                wScan = (ushort)NativeMethods.MapVirtualKey(virtualKey, 0),
                // Without the extended flag Windows reads arrows, Home and End as number-pad keys and drops the Shift.
                dwFlags = (up ? NativeMethods.KeyeventfKeyup : 0) | (virtualKey is >= End and <= Down ? NativeMethods.KeyeventfExtendedkey : 0)
            }
        }
    };
}

internal sealed class Win32SelectionTransport : ISelectionTransport
{
    public nint Target { get; set; }

    public void Copy()
    {
        if (!KeyboardChord.SendCopy())
            throw new IOException("Could not send Ctrl+C.");
    }

    public void Paste()
    {
        WindowFocus.TryRestore(Target);
        Thread.Sleep(40);
        if (!KeyboardChord.SendPaste())
            throw new IOException("Could not send Ctrl+V.");
    }

    public void Select(SelectKeys keys)
    {
        // After the preview the target has to get focus back before the keys mean anything.
        WindowFocus.TryRestore(Target);
        if (!KeyboardChord.SendSelect(keys))
            throw new IOException("Could not send the selection keys.");
    }
}

internal static class WindowFocus
{
    public static void TryRestore(nint hwnd)
    {
        if (hwnd == 0) return;
        try
        {
            var foreground = NativeMethods.GetForegroundWindow();
            var foregroundThread = foreground == 0 ? 0 : NativeMethods.GetWindowThreadProcessId(foreground, out _);
            var targetThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
            var current = NativeMethods.GetCurrentThreadId();
            var attachedCurrent = targetThread != 0 && NativeMethods.AttachThreadInput(current, targetThread, true);
            var attachedForeground = foregroundThread != 0 && foregroundThread != targetThread && NativeMethods.AttachThreadInput(foregroundThread, targetThread, true);
            try
            {
                if (NativeMethods.IsIconic(hwnd)) NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
                NativeMethods.SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attachedForeground) NativeMethods.AttachThreadInput(foregroundThread, targetThread, false);
                if (attachedCurrent) NativeMethods.AttachThreadInput(current, targetThread, false);
            }
        }
        catch
        {
            // Pasting still has a chance if the target kept the foreground.
        }
    }
}
