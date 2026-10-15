namespace WinTendedero.Core;

/// <summary>Continuous time inside a screen's activation band, independent of pointer motion.</summary>
public sealed class EdgeActivation
{
    private TimeSpan? _enteredAt;
    private IntPtr _display;

    public static double Delay(double seconds) => double.IsFinite(seconds) ? Math.Clamp(seconds, 0, 1.5) : 0.2;
    public static double Band(double pixels) => double.IsFinite(pixels) ? Math.Clamp(pixels, 2, 24) : 8;
    public static int PhysicalBand(double pixels, double scale) =>
        Math.Max(1, (int)Math.Round(Band(pixels) * (double.IsFinite(scale) && scale > 0 ? scale : 1)));

    public bool Update(bool eligible, IntPtr display, TimeSpan now, double delay)
    {
        if (!eligible) { Reset(); return false; }
        if (_enteredAt is null || display != _display)
        {
            _enteredAt = now;
            _display = display;
        }
        return now - _enteredAt.Value >= TimeSpan.FromSeconds(Delay(delay));
    }

    public void Reset() { _enteredAt = null; _display = IntPtr.Zero; }
}
