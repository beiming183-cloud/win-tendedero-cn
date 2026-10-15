namespace WinTendedero.Interop;

/// <summary>Acquire the replacement first, so a conflict cannot disable the working shortcut.</summary>
public sealed class ShortcutBinding : IDisposable
{
    private readonly MessageWindow _window;
    private readonly Action _action;
    private HotKey? _current;
    public string? Text => _current?.Text;
    public bool Registered => _current?.Registered == true;

    public ShortcutBinding(MessageWindow window, Action action) { _window = window; _action = action; }

    public bool TrySet(string text)
    {
        if (!HotKey.TryNormalize(text, out var normalized)) return false;
        if (Registered && Text == normalized) return true;
        var next = new HotKey(_window, normalized, _action);
        if (!next.Registered) { next.Dispose(); return false; }
        var old = _current;
        _current = next;
        old?.Dispose();
        return true;
    }

    public void Dispose() { _current?.Dispose(); _current = null; }
}
