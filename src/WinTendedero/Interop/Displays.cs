using static WinTendedero.Interop.Native;

namespace WinTendedero.Interop;

/// <summary>One display, in physical pixels, with its DPI.</summary>
public sealed class Display : IEquatable<Display>
{
    public IntPtr Handle { get; }
    public RECT Bounds { get; }
    public RECT Work { get; }
    public uint Dpi { get; }
    public bool Primary { get; }
    public double Scale => Dpi / 96.0;

    internal Display(IntPtr handle, RECT bounds, RECT work, uint dpi, bool primary)
    {
        Handle = handle; Bounds = bounds; Work = work; Dpi = dpi; Primary = primary;
    }

    /// <summary>
    /// The configurable top activation strip, scaled into this display's physical pixels.
    /// </summary>
    public RECT HotBand
    {
        get
        {
            int height = Core.EdgeActivation.PhysicalBand(Core.Settings.Current.RevealBand, Scale);
            return new RECT(Bounds.Left, Bounds.Top, Bounds.Right, Math.Min(Bounds.Bottom, Bounds.Top + height));
        }
    }

    public bool Contains(POINT p) => Bounds.Contains(p);

    public bool Equals(Display? other) => other is not null && other.Handle == Handle;
    public override bool Equals(object? obj) => Equals(obj as Display);
    public override int GetHashCode() => Handle.GetHashCode();
    public override string ToString() => $"Display {Bounds} dpi {Dpi}{(Primary ? " primary" : "")}";
}

public static class Displays
{
    public static List<Display> All()
    {
        var list = new List<Display>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var m = FromHandle(h);
            if (m is not null) list.Add(m);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static Display? FromHandle(IntPtr h)
    {
        if (h == IntPtr.Zero) return null;
        var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(h, ref info)) return null;
        uint dpi = 96;
        if (GetDpiForMonitor(h, 0, out var dx, out _) == 0) dpi = dx;
        return new Display(h, info.rcMonitor, info.rcWork, dpi, (info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    }

    public static Display? Containing(POINT p) => FromHandle(MonitorFromPoint(p, MONITOR_DEFAULTTONULL));

    public static Display Nearest(POINT p) => FromHandle(MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST)) ?? Primary();

    public static Display? OfWindow(IntPtr hwnd) => FromHandle(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONULL));

    public static Display Primary() => FromHandle(MonitorFromPoint(new POINT(0, 0), MONITOR_DEFAULTTOPRIMARY))!;

    public static POINT Cursor()
    {
        GetCursorPos(out var p);
        return p;
    }

    /// <summary>The screen you are using: the one with the pointer.</summary>
    public static Display UnderPointer() => Nearest(Cursor());
}
