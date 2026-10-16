using System.Windows;
using System.Windows.Media;
using WinTendedero.Core;

namespace WinTendedero.UI;

/// <summary>One geometry and painter for hanging cards, flight and exported drag previews.</summary>
public sealed class PhotoFrame : FrameworkElement
{
    public double Progress { get; set; } = 1;
    protected override void OnRender(DrawingContext dc) => PhotoFrames.Draw(dc, new Size(ActualWidth, ActualHeight), Progress);
}

public static class PhotoFrames
{
    public static readonly string[] Styles = { "compact", "thin", "polaroid", "double", "wood" };
    public static string Style => Styles.Contains(Settings.Current.FrameStyle) ? Settings.Current.FrameStyle : "compact";
    private static double Value(double? value, double fallback, double min, double max) => value is double v && double.IsFinite(v) ? Math.Clamp(v,min,max) : fallback;
    public static double Width => Value(Settings.Current.FrameWidth, Style switch { "thin" => 1.5, "double" => 2, "wood" => 9, _ => .75 }, .5, 12);
    public static double Padding => Value(Settings.Current.FramePadding, Style switch { "thin" => 2, "polaroid" => 10, "double" => 8, "wood" => 4, _ => Surfaces.Inset-.75 }, 0, 20);
    public static double Radius => Value(Settings.Current.FrameRoundness, Style switch { "thin" => 4, "polaroid" => 3, "wood" => 5, _ => Surfaces.Radius }, 0, 32);
    public static Thickness Insets
    {
        get { double side=Width+Padding; return new Thickness(side,side,side,side+(Style=="polaroid"?18*Layout.Scale:0)); }
    }
    public static double PhotoRadius => Math.Max(0,Radius-Insets.Left);
    public static Rect PhotoRect(Size size, double progress=1)
    {
        var m=Insets;
        return new Rect(m.Left*progress,m.Top*progress,Math.Max(0,size.Width-(m.Left+m.Right)*progress),Math.Max(0,size.Height-(m.Top+m.Bottom)*progress));
    }
    public static void Draw(DrawingContext dc, Size size, double progress=1)
    {
        if(size.Width<=0 || size.Height<=0)return;
        double k=Math.Clamp(progress,0,1), radius=Radius*k, width=Width*k;
        var outer=new Rect(0,0,size.Width,size.Height);
        Brush fill=Surfaces.Fill(Settings.Current.CardMaterial,Settings.Current.AccentColor,Theme.AppsDark);
        if(Style=="polaroid") fill=Theme.Freeze(new SolidColorBrush(Theme.AppsDark?Color.FromRgb(67,64,60):Color.FromRgb(255,252,245)));
        if(Style=="wood") fill=Theme.Freeze(new LinearGradientBrush(Color.FromRgb(198,151,99),Color.FromRgb(132,88,50),110));
        dc.DrawRoundedRectangle(fill,null,outer,radius,radius);
        var edge=new Rect(width/2,width/2,Math.Max(0,size.Width-width),Math.Max(0,size.Height-width));
        if(width>0)dc.DrawRoundedRectangle(null,new Pen(Style=="wood"?new SolidColorBrush(Color.FromRgb(157,110,66)):Surfaces.Edge,width),edge,radius,radius);
        if(Style=="double" && k>0)
        {
            double inset=(Width+Padding*.55)*k;
            var inner=new Rect(inset,inset,Math.Max(0,size.Width-2*inset),Math.Max(0,size.Height-2*inset));
            dc.DrawRoundedRectangle(null,new Pen(Surfaces.Edge,Math.Max(.5,width*.6)),inner,Math.Max(0,radius-inset),Math.Max(0,radius-inset));
        }
        if(Style=="wood" && k>0)
        {
            var hole=PhotoRect(size,k);hole.Inflate(1,1);
            dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(outer,radius,radius),new RectangleGeometry(hole)));
            var grain=new Pen(new SolidColorBrush(Color.FromArgb(38,73,40,18)),.65);
            // A few vector strokes add grain without textures or a second image cache.
            for(double x=3;x<size.Width;x+=7)dc.DrawLine(grain,new Point(x,0),new Point(x+3,size.Height));
            var bevel=new Rect(1,1,Math.Max(0,size.Width-2),Math.Max(0,size.Height-2));
            dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromArgb(115,255,237,200)),1),bevel,radius,radius);
            dc.Pop();
        }
    }
}
