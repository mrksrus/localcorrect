using System.Text;

namespace LocalCorrect.App.Win32;

internal readonly record struct ForegroundTarget(nint Handle, string ProcessName, bool Blocked, bool Password);

internal static class ForegroundProbe
{
    public static ForegroundTarget Capture()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == 0) return new ForegroundTarget(0, "", false, false);
        return new ForegroundTarget(hwnd, ProcessName(hwnd), IsBlocked(hwnd), IsPassword(hwnd));
    }

    static string ProcessName(nint hwnd)
    {
        if (!TryOpen(hwnd, out var process)) return "";
        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref size) || size <= 0) return "";
            return Path.GetFileNameWithoutExtension(buffer.ToString(0, size));
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    /// <summary>
    /// True when this process cannot send keystrokes to the window.
    /// An elevated window ignores input from a normal tray app, so we stop
    /// instead of copying nothing and looking like a failure.
    /// </summary>
    static bool IsBlocked(nint hwnd)
    {
        if (!TryOpen(hwnd, out var process)) return true;
        try
        {
            var targetElevated = TokenIsElevated(process);
            if (!targetElevated) return false;
            return !TokenIsElevated(NativeMethods.GetCurrentProcess());
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    static bool TryOpen(nint hwnd, out nint process)
    {
        process = 0;
        var thread = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (thread == 0 || pid == 0) return false;
        process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, pid);
        return process != 0;
    }

    static bool TokenIsElevated(nint process)
    {
        if (!NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery, out var token)) return false;
        try
        {
            var elevation = 0;
            if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenElevation, ref elevation, sizeof(int), out _))
                return false;
            return elevation != 0;
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    static bool IsPassword(nint hwnd)
    {
        var thread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        if (thread == 0) return false;
        var info = new GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
        if (!NativeMethods.GetGUIThreadInfo(thread, ref info) || info.hwndFocus == 0) return false;
        var name = new StringBuilder(64);
        if (NativeMethods.GetClassName(info.hwndFocus, name, name.Capacity) <= 0) return false;
        if (!string.Equals(name.ToString(), "Edit", StringComparison.Ordinal)) return false;
        var sent = NativeMethods.SendMessageTimeout(info.hwndFocus, NativeMethods.EmGetPasswordChar, 0, 0, NativeMethods.SmtoAbortIfHung, 200, out var passwordChar);
        return sent != 0 && passwordChar != 0;
    }
}
