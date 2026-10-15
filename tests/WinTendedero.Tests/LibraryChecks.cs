using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTendedero.Core;
using WinTendedero.UI;
using Line = WinTendedero.Core.Line;

internal static class LibraryChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        Settings.Current.InboxFolder = Path.Combine(root, "library");
        var sourceFolder = Path.Combine(root, "external"); Directory.CreateDirectory(sourceFolder);
        var source = Path.Combine(sourceFolder, "capture.png");
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 960, 600));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(26, 74, 101)), null, new Rect(0, 0, 960, 120));
            dc.DrawText(new FormattedText("win-tendedero", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 48, Brushes.White, 1), new Point(40, 26));
            for (int n = 0; n < 5; n++) dc.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(220-n*16), (byte)(235-n*10), 239)), null, new Rect(40, 170+n*64, 880-n*70, 25));
        }
        var image = new RenderTargetBitmap(960, 600, 96, 96, PixelFormats.Pbgra32); image.Render(drawing);
        Thumbnails.SavePng(image, source);
        var saved = CaptureLibrary.Store(source);
        check(File.Exists(source) && File.Exists(saved) && source != saved && File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(saved)), "external captures archived byte-for-byte without moving originals");
        check(CaptureLibrary.Store(saved) == saved && CaptureLibrary.Store(source) == saved, "watcher and source duplicates share one archived file");
        var another = Path.Combine(root, "another"); Directory.CreateDirectory(another);
        var collision = Path.Combine(another, "capture.png"); File.Copy(source, collision);
        var savedCollision = CaptureLibrary.Store(collision);
        check(savedCollision != saved && File.Exists(saved), "same-name captures never overwrite an existing screenshot");
        var line = new Line(false);
        var id = line.Hang(saved, true)!.Value; line.Discard(id);
        check(File.Exists(saved) && line.LiveCount == 0, "taking down a capture preserves the library file");
        Settings.Current.Pegged = new() { saved };
        var restarted = new Line();
        check(restarted.LiveCount == 0 && File.Exists(saved), "restart empties rope and retains archived images");
        var target = Path.Combine(root, "moved-library");
        CaptureLibrary.ChangeFolder(target);
        check(Inbox.Folder == target && File.Exists(saved) && File.ReadAllBytes(CaptureLibrary.Resolve(saved)).SequenceEqual(File.ReadAllBytes(saved)), "changing location copies history and preserves originals");
        var blocked = Path.Combine(root, "not-a-folder"); File.WriteAllText(blocked, "occupied");
        bool failed = false; try { CaptureLibrary.ChangeFolder(blocked); } catch (IOException) { failed = true; }
        check(failed && Inbox.Folder == target, "failed location change retains working folder");
        Settings.Current.CardScale = 1.4; Settings.Current.AutoArrange = true;
        var paged = new Line(false) { MaxItems = 24 };
        for (int n=0; n<24; n++)
        {
            string path=Path.Combine(target,$"card-{n}.png"); File.Copy(source,path,true); paged.Hang(path,true);
        }
        var canvas = new LineCanvas(paged) { Width = 1440 };
        Arrange(canvas); canvas.RelayoutNow(); canvas.Revealed=true; canvas.Advance(2);
        check(paged.LiveCount == 24 && canvas.PageCount > 1 && canvas.Cards.Count > 1 && canvas.Cards.Count <= WinTendedero.UI.Layout.PageSize(1440), "24 captures retained with bounded visible controls");
        var visited = new HashSet<Guid>();
        canvas.ChangePage(-100);
        for(int n=0; n<canvas.PageCount; n++)
        {
            foreach (var card in canvas.Cards) visited.Add(card.Item.Id);
            canvas.ChangePage(1);
        }
        check(visited.Count == 24, "every screenshot is reachable through pagination");
        canvas.ChangePage(100); check(canvas.PageIndex == canvas.PageCount-1, "last page boundary clamps");
        canvas.ChangePage(-100); check(canvas.PageIndex == 0, "first page boundary clamps");
        canvas.Advance(2);
        var rects=canvas.HitRects().Select(h=>h.rect).OrderBy(r=>r.Left).ToArray();
        check(rects.All(r=>r.Left>=60 && r.Right<=1380) && rects.Zip(rects.Skip(1)).All(pair=>pair.First.Right < pair.Second.Left), "large cards fit without covering folder button or overlapping");
        check(canvas.LibraryRect is { } folder && !canvas.IsOverRope(new Point(folder.Left+8,folder.Top+8)), "folder has its own mouse hit area");
        foreach(bool dark in new[]{false,true})
        {
            Theme.Override(dark); Arrange(canvas); canvas.RelayoutNow(); canvas.Advance(2); Arrange(canvas);
            var bitmap=new RenderTargetBitmap(2160,(int)(WinTendedero.UI.Layout.PanelHeight*1.5),144,144,PixelFormats.Pbgra32); bitmap.Render(canvas);
            Thumbnails.SavePng(bitmap,Path.Combine(root,dark?"rope-dark.png":"rope-light.png"));
        }
        Settings.Current.Save();
        var reloaded=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Settings.Folder,"settings.json")));
        check(reloaded.RootElement.GetProperty("inboxFolder").GetString()==target, "capture location persists across launches");
    }
    private static void Arrange(FrameworkElement canvas)
    {
        canvas.Measure(new Size(1440, WinTendedero.UI.Layout.PanelHeight));
        canvas.Arrange(new Rect(0,0,1440,WinTendedero.UI.Layout.PanelHeight));
        canvas.UpdateLayout();
    }
}
