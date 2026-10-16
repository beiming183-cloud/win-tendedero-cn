using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinTendedero.Core;

namespace WinTendedero.UI;

public static class Surfaces
{
    public static readonly string[] Materials = { "glass", "paper", "metal", "soft" };
    public static Color ColorOf(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(108, 145, 184); }
    }
    private static Color Mix(Color a, Color b, double k) => Color.FromRgb((byte)(a.R+(b.R-a.R)*k),(byte)(a.G+(b.G-a.G)*k),(byte)(a.B+(b.B-a.B)*k));
    public static double Radius => Settings.Current.CardMaterial switch { "paper" => 5, "metal" => 9, "soft" => 16, _ => 12 };
    public static double Inset => Settings.Current.CardMaterial switch { "paper" => 7, "metal" => 4, "soft" => 5, _ => 3 };
    public static double Shadow => Settings.Current.CardMaterial == "soft" ? 0.10 : 0.14;
    public static Brush Fill(string material, string accent, bool dark)
    {
        var color=ColorOf(accent);
        Brush fill=material switch
        {
            "paper" => new SolidColorBrush(dark ? Color.FromRgb(67,62,55) : Color.FromRgb(255,249,235)),
            "metal" => new LinearGradientBrush(dark ? Color.FromRgb(83,92,101) : Color.FromRgb(239,243,246), dark ? Color.FromRgb(34,42,50) : Color.FromRgb(160,173,185), 100),
            "soft" => new SolidColorBrush(Mix(color,dark ? Color.FromRgb(35,39,47) : Colors.White,dark ? .58 : .72)),
            _ => new LinearGradientBrush(Mix(color,dark ? Color.FromRgb(51,58,69) : Colors.White,.82), dark ? Color.FromArgb(210,30,37,48) : Color.FromArgb(210,234,243,251),90),
        };
        fill.Freeze(); return fill;
    }
    public static Brush Edge => Theme.Freeze(new SolidColorBrush(Mix(ColorOf(Settings.Current.AccentColor),Theme.AppsDark ? Colors.White : Colors.Black,.15)));
    public static void PaintFrame(Border frame)
    {
        frame.Background=Fill(Settings.Current.CardMaterial,Settings.Current.AccentColor,Theme.AppsDark);
        frame.BorderBrush=Edge;
    }
}

public sealed record LookPreset(string Key,string Material,string Accent,string Rope,string Peg,string Cord,bool Bows)
{
    public static readonly LookPreset[] All =
    {
        new("clear","glass","#769AB9","blue","metal","thread",false),
        new("warm","paper","#B18B58","bronze","wood","cotton",true),
        new("steel","metal","#7C95A6","gray","metal","wire",false),
        new("candy","soft","#E9A0BC","pink","mixed","cotton",true),
        new("mint","soft","#75B8A0","green","green","thread",false),
    };
    public void Apply()
    {
        var s=Settings.Current;
        s.CardMaterial=Material;s.AccentColor=Accent;s.RopeColor=Rope;s.PegStyle=Peg;s.RopeMaterial=Cord;s.Bows=Bows;
    }
}

public static class MotionFeel
{
    public static double Amount => SystemParameters.ClientAreaAnimation && double.IsFinite(Settings.Current.Elasticity) ? Math.Clamp(Settings.Current.Elasticity,0,1) : 0;
    public static Spring Spring(double response,double value=0) => global::WinTendedero.UI.Spring.FromResponse(response,.96-.40*Amount,value);
    public static double PullThreshold(double cardHeight) => Math.Clamp(cardHeight*.55,64,112);
    public static bool PullsOut(double dx,double dy,double cardHeight) => Math.Abs(dy)>PullThreshold(cardHeight) && Math.Abs(dy)>Math.Abs(dx)*.65;
}
