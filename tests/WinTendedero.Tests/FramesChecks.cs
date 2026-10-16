using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTendedero.Core;
using WinTendedero.UI;
using Line=WinTendedero.Core.Line;
using Layout=WinTendedero.UI.Layout;

internal static class FramesChecks
{
    public static void Run(string root,Action<bool,string> check)
    {
        var s=Settings.Current;s.CardMaterial="paper";s.CardScale=1.4;s.AccentColor="#B18B58";s.PegStyle="wood";
        var sheet=new DrawingVisual();
        using(var dc=sheet.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(246,244,240)),null,new Rect(0,0,1100,1500));
            for(int i=0;i<PhotoFrames.Styles.Length;i++)
            {
                s.FrameStyle=PhotoFrames.Styles[i];s.FrameWidth=s.FramePadding=s.FrameRoundness=null;
                bool fits=true;
                foreach(var scale in new[]{1d,2})foreach(var shape in new[]{new Size(4000,200),new Size(200,4000),new Size(4000,3000)})
                {
                    s.CardScale=scale;
                    foreach(bool extreme in new[]{false,true})
                    {
                        s.FrameWidth=extreme?12:null;s.FramePadding=extreme?20:null;s.FrameRoundness=extreme?32:null;
                        var card=Layout.CardSize(shape.Width,shape.Height);var photo=PhotoFrames.PhotoRect(card);
                        fits &= card.Width<=Layout.CardWidth+.001 && photo.Width>0 && photo.Height>0 && Math.Abs(photo.Width/photo.Height-shape.Width/shape.Height)<.001;
                        fits &= card.Height+Layout.CardOffsetBelowTop+Layout.Sag(1100)+40<Layout.PanelHeight;
                    }
                }
                check(fits,"complete photo and frame fit at every supported size: "+s.FrameStyle);
                s.CardScale=1.4;s.FrameWidth=s.FramePadding=s.FrameRoundness=null;
                var line=new Line(false);for(int n=0;n<3;n++)line.Hang(Path.Combine(root,$"look-{n}.png"),true);
                var canvas=new LineCanvas(line){Width=1100};Arrange(canvas);canvas.Revealed=true;canvas.RelayoutNow();canvas.Advance(3);
                WatcherChecks.PumpUntil(()=>canvas.Cards.All(c=>c.Item.HasThumbnail));canvas.Advance(3);Arrange(canvas);
                var first=canvas.Cards[0];
                var preview=(BitmapSource)typeof(PeggedControl).GetMethod("RenderDragImage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(first,new object[]{1.5})!;
                var size=Layout.CardSize(first.Item.PixelWidth,first.Item.PixelHeight);
                check(preview.PixelWidth==(int)(size.Width*1.5) && preview.PixelHeight==(int)(size.Height*1.5),"drag preview includes complete frame: "+s.FrameStyle);
                var bitmap=new RenderTargetBitmap(1100,(int)Layout.PanelHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(canvas);
                dc.DrawText(new FormattedText(Strings.FrameName(s.FrameStyle),System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),20,Brushes.DimGray,1),new Point(28,i*300+14));
                dc.DrawImage(bitmap,new Rect(0,i*300+45,1100,250));
                if(s.FrameStyle=="polaroid")check(PhotoFrames.Insets.Bottom>PhotoFrames.Insets.Top+20,"instant photo retains its characteristic bottom mat");
            }
        }
        var result=new RenderTargetBitmap(1100,1500,96,96,PixelFormats.Pbgra32);result.Render(sheet);Thumbnails.SavePng(result,Path.Combine(root,"frame-styles.png"));
        s.FrameStyle="polaroid";s.FrameWidth=s.FramePadding=s.FrameRoundness=null;
        var page=SettingsWindow.ForDemo(()=>{});page.Measure(new Size(600,double.PositiveInfinity));page.Arrange(new Rect(new Point(),page.DesiredSize));page.UpdateLayout();
        Find<Slider>(page,"FrameWidth").Value=6;Find<Slider>(page,"FramePadding").Value=14;Find<Slider>(page,"FrameRoundness").Value=18;
        check(s.FrameWidth==6 && s.FramePadding==14 && s.FrameRoundness==18,"frame sliders update independent dimensions");
        s.Save();var json=File.ReadAllText(Path.Combine(Settings.Folder,"settings.json"));
        check(json.Contains("\"frameStyle\": \"polaroid\"") && json.Contains("\"framePadding\": 14"),"frame preferences persist to profile");
        Find<Button>(page,"ResetFrame").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(s.FrameWidth is null && s.FramePadding is null && s.FrameRoundness is null,"reset restores style defaults");
        s.FrameStyle="compact";
    }
    private static void Arrange(FrameworkElement view){view.Measure(new Size(1100,Layout.PanelHeight));view.Arrange(new Rect(0,0,1100,Layout.PanelHeight));view.UpdateLayout();}
    private static T Find<T>(DependencyObject root,string id) where T:DependencyObject
    {
        if(root is T found && AutomationProperties.GetAutomationId(root)==id)return found;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){try{return Find<T>(VisualTreeHelper.GetChild(root,i),id);}catch(InvalidOperationException){}}
        throw new InvalidOperationException(id);
    }
}
