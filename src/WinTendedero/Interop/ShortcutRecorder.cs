using System.Runtime.InteropServices;
using System.Windows.Threading;
using static WinTendedero.Interop.Native;

namespace WinTendedero.Interop;

/// <summary>Capture a shortcut only while its editor has focus, before registered hotkeys consume it.</summary>
public sealed class ShortcutRecorder : IDisposable
{
    private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardData { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int kind, HookCallback callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly HookCallback _callback;
    private readonly Dispatcher _dispatcher;
    private readonly Func<bool> _active;
    private readonly Action<uint, uint> _recorded;
    private readonly HashSet<uint> _pressed = new();
    private readonly HashSet<uint> _primaryDown = new();
    private IntPtr _hook;
    private bool _disposed;
    private bool _pending;
    public bool Registered => _hook != IntPtr.Zero;

    public ShortcutRecorder(Func<bool> active, Action<uint, uint> recorded)
    {
        _active = active;
        _recorded = recorded;
        _dispatcher = Dispatcher.CurrentDispatcher;
        foreach (uint key in new uint[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
            if (GetAsyncKeyState((int)key) < 0) _pressed.Add(key);
        _callback = Hook;
        _hook = SetWindowsHookEx(13, _callback, GetModuleHandle(null), 0);
    }

    private static bool IsModifier(uint key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;
    private uint Modifiers =>
        (_pressed.Any(k => k is 0x11 or 0xA2 or 0xA3) ? MOD_CONTROL : 0) |
        (_pressed.Any(k => k is 0x12 or 0xA4 or 0xA5) ? MOD_ALT : 0) |
        (_pressed.Any(k => k is 0x10 or 0xA0 or 0xA1) ? MOD_SHIFT : 0) |
        (_pressed.Any(k => k is 0x5B or 0x5C) ? MOD_WIN : 0);

    private IntPtr Hook(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || _disposed) return CallNextHookEx(_hook, code, message, data);
        try
        {
            uint key = Marshal.PtrToStructure<KeyboardData>(data).Key;
            bool down = message.ToInt64() is 0x100 or 0x104;
            bool up = message.ToInt64() is 0x101 or 0x105;
            if (!down && !up) return CallNextHookEx(_hook, code, message, data);
            if (IsModifier(key))
            {
                if (down) _pressed.Add(key); else _pressed.Remove(key);
                return CallNextHookEx(_hook, code, message, data);
            }
            if (!_active()) return CallNextHookEx(_hook, code, message, data);
            uint modifiers = Modifiers;
            if (key == 0x09 && modifiers == 0) return CallNextHookEx(_hook, code, message, data);
            if (up) { _primaryDown.Remove(key); return new IntPtr(1); }
            if (_primaryDown.Add(key) && !_pending)
            {
                _pending = true;
                _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    _pending = false;
                    if (!_disposed && _active()) _recorded(key, modifiers);
                }));
            }
            return new IntPtr(1);
        }
        catch { return CallNextHookEx(_hook, code, message, data); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        GC.KeepAlive(_callback);
    }
}
