using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using WinTendedero.Core;

namespace WinTendedero.UI;

public sealed partial class SettingsWindow
{
    private UIElement BuildLooks()
    {
        var s=Settings.Current;
        var panel=new StackPanel();
        panel.Children.Add(new TextBlock { Text=Strings.StylePresets, Margin=new Thickness(0,4,0,8),FontSize=12 });
        var presets=new WrapPanel { Margin=new Thickness(-3,0,-3,12) };
        foreach(var preset in LookPreset.All)
        {
            var content=new StackPanel();
            content.Children.Add(Swatch(preset));
            content.Children.Add(new TextBlock { Text=Strings.PresetName(preset.Key), HorizontalAlignment=HorizontalAlignment.Center, Margin=new Thickness(0,3,0,0), FontSize=12 });
            var selected=s.CardMaterial==preset.Material && s.AccentColor==preset.Accent && s.PegStyle==preset.Peg;
            var button=new Button { Content=content,Width=160,Padding=new Thickness(4,6,4,6),Margin=new Thickness(3,0,3,7),BorderThickness=new Thickness(selected?2:1), BorderBrush=selected?new SolidColorBrush(Surfaces.ColorOf(preset.Accent)):(Brush)Application.Current.Resources["MenuBorder"] };
            button.SetResourceReference(BackgroundProperty,"MenuBg");
            button.SetResourceReference(ForegroundProperty,"MenuFg");
            AutomationProperties.SetAutomationId(button,"LookPreset_"+preset.Key);
            AutomationProperties.SetName(button,Strings.PresetName(preset.Key));
            button.Click+=(_,_)=>
            {
                double offset=(Content as ScrollViewer)?.VerticalOffset??0;
                preset.Apply(); Changed(); Content=Build();
                Dispatcher.BeginInvoke(()=> (Content as ScrollViewer)?.ScrollToVerticalOffset(offset));
            };
            presets.Children.Add(button);
        }
        panel.Children.Add(presets);
        panel.Children.Add(BuildFrames());
        var materials=Choice(Surfaces.Materials.Select(key=>(key,Strings.MaterialName(key))).ToArray(),s.CardMaterial,key=>s.CardMaterial=key);
        AutomationProperties.SetAutomationId(materials,"CardMaterial");
        panel.Children.Add(Row(Strings.MaterialLabel,materials));
        var cord=Choice(new[]{("thread",Strings.CordName("thread")),("cotton",Strings.CordName("cotton")),("wire",Strings.CordName("wire"))},s.RopeMaterial,key=>s.RopeMaterial=key);
        AutomationProperties.SetAutomationId(cord,"RopeMaterial");
        panel.Children.Add(Row(Strings.CordLabel,cord));
        var colors=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,2) };
        var swatch=new Border { Width=22,Height=22,CornerRadius=new CornerRadius(6),Background=new SolidColorBrush(Surfaces.ColorOf(s.AccentColor)),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,8,0) };
        swatch.SetResourceReference(BorderBrushProperty,"MenuBorder");
        var readout=new TextBlock { Text=s.AccentColor,VerticalAlignment=VerticalAlignment.Center,FontSize=12,Width=80 };
        AutomationProperties.SetAutomationId(readout,"AccentColorValue");
        colors.Children.Add(swatch);colors.Children.Add(readout);
        void Pick(string hex){s.AccentColor=hex;swatch.Background=new SolidColorBrush(Surfaces.ColorOf(hex));readout.Text=hex;Changed();}
        string[] palette={"#769AB9","#E9A0BC","#75B8A0","#AB97CE","#D9AD64"};
        foreach(var hex in palette)
        {
            var choice=new Button { Width=24,Height=24,Margin=new Thickness(0,0,4,0),Padding=new Thickness(3),ToolTip=hex, Content=new Ellipse { Width=14,Height=14,Fill=new SolidColorBrush(Surfaces.ColorOf(hex)) } };
            AutomationProperties.SetAutomationId(choice,"Accent_"+hex[1..]);
            AutomationProperties.SetName(choice,hex);
            choice.Click+=(_,_)=>Pick(hex); colors.Children.Add(choice);
        }
        var custom=new Button { Content=Strings.CustomColor,Padding=new Thickness(8,3,8,3),Margin=new Thickness(4,0,0,0) };
        custom.Click+=(_,_)=>
        {
            SelectColor(Surfaces.ColorOf(s.AccentColor),Pick);
        };
        colors.Children.Add(custom);
        panel.Children.Add(new TextBlock { Text=Strings.AccentLabel,Margin=new Thickness(0,10,0,0),FontSize=12 });
        panel.Children.Add(colors);
        panel.Children.Add(ActivationSlider(Strings.ElasticityLabel,Strings.ElasticityHint,"Elasticity",0,100,5,s.Elasticity*100,v=>$"{v:0}",v=>s.Elasticity=v/100));
        var arrival=Choice(new[]{("bounce",Strings.ArrivalBounce),("flight",Strings.ArrivalFlight)},s.ArrivalEffect,key=>s.ArrivalEffect=key);
        AutomationProperties.SetAutomationId(arrival,"ArrivalEffect");
        panel.Children.Add(Row(Strings.ArrivalLabel,arrival));
        return panel;
    }

    private void SelectColor(Color value,Action<string> apply)
    {
        using var dialog=new System.Windows.Forms.ColorDialog {FullOpen=true,Color=System.Drawing.Color.FromArgb(value.R,value.G,value.B)};
        if(dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle))==System.Windows.Forms.DialogResult.OK)
            apply($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
    }
    private static FrameworkElement Swatch(LookPreset preset)
    {
        var canvas=new Canvas { Width=144,Height=58,IsHitTestVisible=false };
        var rope=new System.Windows.Shapes.Path { Data=Geometry.Parse("M0,7 Q72,23 144,7"),Stroke=new SolidColorBrush(Pegs.RopeColorOf(preset.Rope)),StrokeThickness=preset.Cord=="cotton"?2.5:1.5 };
        canvas.Children.Add(rope);
        var paper=new Border { Width=68,Height=38,CornerRadius=new CornerRadius(preset.Material=="paper"?3:preset.Material=="soft"?9:6),Background=Surfaces.Fill(preset.Material,preset.Accent,Theme.AppsDark),BorderBrush=new SolidColorBrush(Surfaces.ColorOf(preset.Accent)),BorderThickness=new Thickness(1),Padding=new Thickness(5) };
        var image=new Grid { Background=Brushes.White };
        var photo=new System.Windows.Shapes.Path { Data=Geometry.Parse("M0,14 L9,4 16,12 21,7 30,18 H0 Z M23,2 A2,2 0 1 1 22.9,2"),Fill=new SolidColorBrush(Surfaces.ColorOf(preset.Accent)),Width=30,Height=18,Stretch=Stretch.Uniform };
        image.Children.Add(photo);paper.Child=image;
        Canvas.SetLeft(paper,38);Canvas.SetTop(paper,18);canvas.Children.Add(paper);
        var peg=Pegs.Create(preset.Peg,3);peg.LayoutTransform=new ScaleTransform(.6,.6);Canvas.SetLeft(peg,69);Canvas.SetTop(peg,8);canvas.Children.Add(peg);
        return canvas;
    }
    private sealed record DialogOwner(IntPtr Handle):System.Windows.Forms.IWin32Window;
}
