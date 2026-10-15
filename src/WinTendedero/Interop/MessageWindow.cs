using System.Windows.Interop;
using static WinTendedero.Interop.Native;

namespace WinTendedero.Interop;

/// <summary>A hidden message-only window for hot keys and clipboard notifications.</summary>
public sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;
    public IntPtr Handle => _source.Handle;

    public event Action<int, IntPtr, IntPtr>? Message;

    public MessageWindow()
    {
        var p = new HwndSourceParameters("win-tendedero.Messages")
        {
            Width = 0, Height = 0, PositionX = 0, PositionY = 0,
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE,
        };
        _source = new HwndSource(p);
        _source.AddHook(Hook);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        Message?.Invoke(msg, wParam, lParam);
        return IntPtr.Zero;
    }

    public void Dispose() => _source.Dispose();
}

/// <summary>A single global shortcut, through RegisterHotKey. No hooks, no permissions.</summary>
public sealed class HotKey : IDisposable
{
    private readonly MessageWindow _window;
    private readonly int _id;
    private static int _nextId = 0x4C49; // "LI"
    public bool Registered { get; }
    public string Text { get; }
    private readonly Action<int, IntPtr, IntPtr>? _handler;
    private bool _disposed;

    public HotKey(MessageWindow window, string text, Action action)
    {
        _window = window;
        _id = _nextId++;
        Text = text;
        if (!Parse(text, out var mods, out var vk)) { Registered = false; return; }
        Registered = RegisterHotKey(window.Handle, _id, mods | MOD_NOREPEAT, vk);
        if (!Registered) return;
        _handler = (msg, w, _) =>
        {
            if (msg == WM_HOTKEY && w.ToInt32() == _id) action();
        };
        window.Message += _handler;
    }

    /// <summary>Parses "Ctrl+Alt+T", "Win+Shift+F9" and so on.</summary>
    public static bool Parse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        int primaryKeys = 0;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Any(string.IsNullOrEmpty)) return false;
        foreach (var raw in parts)
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win" or "windows" or "meta": modifiers |= MOD_WIN; break;
                default:
                    if (++primaryKeys > 1) return false;
                    if (raw.Length == 1 && char.IsAsciiLetterOrDigit(raw[0])) vk = char.ToUpperInvariant(raw[0]);
                    else if (!int.TryParse(raw, out _) && Enum.TryParse<System.Windows.Forms.Keys>(raw, true, out var key)) vk = (uint)key;
                    else return false;
                    if (vk is < 0x08 or > 0xFE or 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5) return false;
                    break;
            }
        }
        return primaryKeys == 1 && ((modifiers & (MOD_CONTROL | MOD_ALT | MOD_WIN)) != 0 || vk is >= 0x70 and <= 0x87);
    }

    public static bool TryNormalize(string text, out string normalized)
    {
        normalized = "";
        if (!Parse(text, out var mods, out var vk)) return false;
        var parts = new List<string>();
        if ((mods & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mods & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(((System.Windows.Forms.Keys)vk).ToString());
        normalized = string.Join("+", parts);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Registered) UnregisterHotKey(_window.Handle, _id);
        if (_handler is not null) _window.Message -= _handler;
    }
}
