using System.Runtime.InteropServices;
using LocalCorrect.Core;

namespace LocalCorrect.App.Win32;

internal static class KeyboardChord
{
    public static bool SendCopy() => Send(0x43);
    public static bool SendPaste() => Send(0x56);

    public static int InputSize => Marshal.SizeOf<INPUT>();

    static bool Send(ushort virtualKey)
    {
        ReleaseModifiers();
        Thread.Sleep(30);
        var inputs = new[]
        {
            Key(0x11, up: false),
            Key(virtualKey, up: false),
            Key(virtualKey, up: true),
            Key(0x11, up: true)
        };
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
                dwFlags = up ? NativeMethods.KeyeventfKeyup : 0
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
