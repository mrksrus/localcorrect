using LocalCorrect.Core;

namespace LocalCorrect.App.Win32;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int Id = 1;
    public event Action? Pressed;

    public HotkeyWindow() => CreateHandle(new CreateParams());

    public bool Register(HotkeySpec spec)
    {
        Unregister();
        if (!spec.IsValid || !VirtualKeys.TryGet(spec.Key, out var virtualKey)) return false;
        return NativeMethods.RegisterHotKey(Handle, Id, spec.NativeModifiers | NativeMethods.ModNorepeat, (uint)virtualKey);
    }

    public void Unregister()
    {
        if (Handle != 0) NativeMethods.UnregisterHotKey(Handle, Id);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey && m.WParam == (nint)Id) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        if (Handle != 0) DestroyHandle();
    }
}
