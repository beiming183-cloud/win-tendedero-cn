using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using WinTendedero.Core;

namespace WinTendedero.UI;

public sealed partial class SettingsWindow
{
    private UIElement BuildFrames()
    {
        var s=Settings.Current;
        var panel=new StackPanel { Margin=new Thickness(0,0,0,12) };
        void Rebuild()
        {
            double offset=(Content as ScrollViewer)?.VerticalOffset??0;
            Content=Build();
            Dispatcher.BeginInvoke(()=> (Content as ScrollViewer)?.ScrollToVerticalOffset(offset));
        }
        var style=Choice(PhotoFrames.Styles.Select(key=>(key,Strings.FrameName(key))).ToArray(),PhotoFrames.Style,key=>
        {
            s.FrameStyle=key;s.FrameWidth=null;s.FramePadding=null;s.FrameRoundness=null;
            Dispatcher.BeginInvoke(Rebuild);
        });
        AutomationProperties.SetAutomationId(style,"FrameStyle");
        panel.Children.Add(Row(Strings.FrameLabel,style));
        panel.Children.Add(new TextBlock { Text=Strings.FrameHint,TextWrapping=TextWrapping.Wrap,FontSize=11,Opacity=.65,Margin=new Thickness(0,2,0,5) });
        panel.Children.Add(ActivationSlider(Strings.FrameWidthLabel,"","FrameWidth",.5,12,.5,PhotoFrames.Width,v=>$"{v:0.#} px",v=>s.FrameWidth=v));
        panel.Children.Add(ActivationSlider(Strings.FramePaddingLabel,"","FramePadding",0,20,1,PhotoFrames.Padding,v=>$"{v:0} px",v=>s.FramePadding=v));
        panel.Children.Add(ActivationSlider(Strings.FrameRadiusLabel,"","FrameRoundness",0,32,1,PhotoFrames.Radius,v=>$"{v:0} px",v=>s.FrameRoundness=v));
        var reset=new Button { Content=Strings.ResetFrame,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,4,10,4),Margin=new Thickness(0,5,0,0) };
        AutomationProperties.SetAutomationId(reset,"ResetFrame");
        reset.Click+=(_,_)=>{s.FrameWidth=null;s.FramePadding=null;s.FrameRoundness=null;Changed();Rebuild();};
        panel.Children.Add(reset);
        return panel;
    }
}
