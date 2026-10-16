using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTendedero.Core;
using WinTendedero.UI;
using Line=WinTendedero.Core.Line;

internal static class LooksChecks
{
    public static void Run(string root,Action<bool,string> check)
    {
        var s=Settings.Current;var folder=s.InboxFolder;var key=s.HotKey;
        s.Elasticity=.8;
        foreach(var preset in LookPreset.All)
        {
            preset.Apply();
            check(s.CardMaterial==preset.Material && s.AccentColor==preset.Accent && s.RopeMaterial==preset.Cord,"preset applies "+preset.Key);
        }
        check(s.InboxFolder==folder && s.HotKey==key,"style presets preserve capture folder and shortcut");
        check(!MotionFeel.PullsOut(200,45,160) && MotionFeel.PullsOut(0,100,160),"horizontal sorting and deliberate outward pull have distinct thresholds");
        check(MotionFeel.PullThreshold(4)>=64 && MotionFeel.PullThreshold(1000)<=112,"outward drag threshold stays usable across photo sizes");
        foreach(var amount in new[]{0d,.5,1})
        {
            s.Elasticity=amount;var spring=MotionFeel.Spring(.35);spring.Target=1;
            double peak=0;
            for(int n=0;n<240;n++){spring.Step(1d/120);peak=Math.Max(peak,spring.Value);}
            check(double.IsFinite(peak) && peak<1.3 && spring.Resting,"bounded spring settles at elasticity "+amount);
        }
        s.Elasticity=.8;s.AutoArrange=true;s.CardMaterial="glass";
        var line=new Line(false);var source=Path.Combine(root,"external","capture.png");
        for(int n=0;n<3;n++){var path=Path.Combine(root,$"look-{n}.png");File.Copy(source,path);line.Hang(path,true);}
        var canvas=new LineCanvas(line){Width=1100};Arrange(canvas);canvas.Revealed=true;canvas.RelayoutNow();canvas.Advance(3);
        var card=canvas.Cards[0];double target=canvas.Cards[2].TargetX;
        canvas.BeginReorder(card);canvas.ReorderTo(card,target,25,700);
        check(Math.Abs(card.CenterX-target)<.01,"held card follows pointer immediately without waiting for a frame");
        check(line.IndexOf(card.Item.Id)==2,"dragging across slots reorders the correct photo");
        canvas.EndReorder(card);canvas.Advance(3);
        check(Math.Abs(card.CenterX-card.TargetX)<.1 && !card.Active,"release spring settles back into its slot");
        var page=SettingsWindow.ForDemo(()=>{});
        page.Measure(new Size(600,double.PositiveInfinity));page.Arrange(new Rect(new Point(),page.DesiredSize));page.UpdateLayout();
        var elasticity=Find<Slider>(page,"Elasticity");elasticity.Value=90;
        check(Math.Abs(s.Elasticity-.9)<.001,"real elasticity slider updates settings");
        var arrival=Find<ComboBox>(page,"ArrivalEffect");
        check(s.ArrivalEffect=="bounce" && arrival.SelectedIndex==0,"new captures default to local bounce");
        arrival.SelectedIndex=1;check(s.ArrivalEffect=="flight","capture flight remains selectable");arrival.SelectedIndex=0;
        Find<Button>(page,"LookPreset_warm").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(s.CardMaterial=="paper" && s.PegStyle=="wood","real preset button applies material and peg style");
        Theme.Override(false);
        var sheet=new DrawingVisual();
        using(var dc=sheet.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245,246,248)),null,new Rect(0,0,1100,LookPreset.All.Length*340));
            for(int i=0;i<LookPreset.All.Length;i++)
            {
                var preset=LookPreset.All[i];preset.Apply();
                var demoLine=new Line(false);
                for(int n=0;n<3;n++)demoLine.Hang(Path.Combine(root,$"look-{n}.png"),true);
                var demo=new LineCanvas(demoLine){Width=1100};Arrange(demo);demo.RelayoutNow();demo.Revealed=true;demo.Advance(3);Arrange(demo);
                WatcherChecks.PumpUntil(()=>demo.Cards.All(item=>item.Item.HasThumbnail));demo.Advance(3);Arrange(demo);
                var dragImage=(BitmapSource)typeof(PeggedControl).GetMethod("RenderDragImage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(demo.Cards[0],new object[]{1.5})!;
                check(dragImage.PixelWidth>0 && dragImage.PixelHeight>0,"external drag preview renders for material "+preset.Material);
                var bitmap=new RenderTargetBitmap(1100,(int)WinTendedero.UI.Layout.PanelHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(demo);
                dc.DrawText(new FormattedText(Strings.PresetName(preset.Key)+" · "+Strings.MaterialName(preset.Material),System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),18,Brushes.DimGray,1),new Point(28,i*340+10));
                dc.DrawImage(bitmap,new Rect(0,i*340+46,1100,280));
            }
        }
        var result=new RenderTargetBitmap(1100,LookPreset.All.Length*340,96,96,PixelFormats.Pbgra32);result.Render(sheet);Thumbnails.SavePng(result,Path.Combine(root,"material-presets.png"));
    }
    private static void Arrange(FrameworkElement canvas){canvas.Measure(new Size(1100,WinTendedero.UI.Layout.PanelHeight));canvas.Arrange(new Rect(0,0,1100,WinTendedero.UI.Layout.PanelHeight));canvas.UpdateLayout();}
    private static T Find<T>(DependencyObject root,string id) where T:DependencyObject
    {
        if(root is T found && AutomationProperties.GetAutomationId(root)==id)return found;
        for(int n=0;n<VisualTreeHelper.GetChildrenCount(root);n++){try{return Find<T>(VisualTreeHelper.GetChild(root,n),id);}catch(InvalidOperationException){}}
        throw new InvalidOperationException(id);
    }
}
