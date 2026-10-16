using System.Windows;

namespace WinTendedero.UI;

/// <summary>Where everything goes, in device independent pixels.</summary>
public static class Layout
{
    public static double Scale => Math.Clamp(Core.Settings.Current.CardScale, 1, 2);
    public static double PanelHeight => 180 + 120 * Scale;
    public const double RopeTop = 10;
    public static double Spacing => CardWidth + 22;
    public static double CardWidth => 150 * Scale;
    /// <summary>How far above the rope the top of the clip sits.</summary>
    public const double PinAbove = 9.5;
    public const double PinHeight = 26;
    public const double PinWidth = 9;
    /// <summary>Distance from the top of the hanging view (the clip) to the card.</summary>
    public const double CardOffsetBelowTop = PinHeight - 12;
    public static double FrameRadius => Surfaces.Radius;
    public static double FrameInset => Surfaces.Inset;
    public static double PhotoMaxHeight => 118 * Scale;

    public static int PageSize(double width) => Math.Max(1, (int)((width - 160) / Spacing));

    /// <summary>The rope hangs as a parabola from edge to edge of the screen.</summary>
    public static double Sag(double width) => Math.Min(58, width * 0.036);

    public static double RopeY(double x, double width)
    {
        if (width <= 0) return RopeTop;
        double f = x / width;
        return RopeTop + 4 * Sag(width) * f * (1 - f);
    }

    public static double X(int index, int count, double width)
    {
        double spacing = Math.Min(Spacing, Math.Max(1, (width - CardWidth - 160) / Math.Max(1, count - 1)));
        double total = Math.Max(count - 1, 0) * spacing;
        return width / 2 - total / 2 + index * spacing;
    }

    /// <summary>The photo fits inside the card area keeping its proportions.</summary>
    public static Size PhotoSize(double pixelWidth, double pixelHeight)
    {
        double maxW = CardWidth - 14, maxH = PhotoMaxHeight;
        if (pixelWidth <= 0 || pixelHeight <= 0) return new Size(maxW, maxH);
        double scale = Math.Min(maxW / pixelWidth, maxH / pixelHeight);
        return new Size(pixelWidth * scale, pixelHeight * scale);
    }

    /// <summary>The card around the photo: the photo plus the glass inset.</summary>
    public static Size CardSize(double pixelWidth, double pixelHeight)
    {
        var p = PhotoSize(pixelWidth, pixelHeight);
        return new Size(p.Width + FrameInset * 2, p.Height + FrameInset * 2);
    }
}
