using System.IO;
using System.Windows.Threading;
using WinTendedero.Core;

internal static class WatcherChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var folder=Path.Combine(root,"watcher"); Directory.CreateDirectory(folder);
        for(int i=0;i<2000;i++) File.WriteAllText(Path.Combine(folder,$"old-{i}.png"),"fixture");
        var caught=new List<string>(); var modified=new List<string>(); int changed=0;
        using var watcher=new ScreenshotWatcher(folder,caught.Add,modified.Add,()=>changed++);
        watcher.Start();
        PumpUntil(()=>watcher.FullScanCount>0 && !watcher.IsBusy);
        caught.Clear();
        int scans=watcher.FullScanCount;
        var path=Path.Combine(folder,"new.png"); File.WriteAllText(path,"new image");
        PumpUntil(()=>caught.Contains(path));
        check(watcher.FullScanCount==scans,"single capture uses its notification without enumerating 2000 old files");
        check(caught.Count(p=>p==path)==1,"duplicate create/write events produce one capture");
        File.AppendAllText(path," updated"); PumpUntil(()=>modified.Contains(path));
        check(watcher.FullScanCount==scans,"editing a capture does not enumerate the directory");
        var renamed=Path.Combine(folder,"renamed.png"); File.Move(path,renamed); PumpUntil(()=>caught.Contains(renamed));
        check(watcher.FullScanCount==scans,"renaming a capture is handled without a full scan");
        int before=changed; File.Delete(renamed); PumpUntil(()=>changed>before);
        check(watcher.FullScanCount==scans,"deletion is handled without a full scan");
        var temp=Path.Combine(folder,"capture.tmp"); File.WriteAllText(temp,"temporary image");
        var final=Path.Combine(folder,"from-temp.png"); File.Move(temp,final); PumpUntil(()=>caught.Contains(final));
        check(caught.Count(p=>p==final)==1,"temporary file renamed into a screenshot is caught once");
        var until=DateTime.UtcNow.AddSeconds(3.4);
        PumpUntil(()=>DateTime.UtcNow>=until);
        check(watcher.FullScanCount==scans,"idle watcher no longer polls every three seconds");
        var native=(FileSystemWatcher)typeof(ScreenshotWatcher).GetField("_watcher",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(watcher)!;
        native.EnableRaisingEvents=false;
        var missed=Path.Combine(folder,"missed.png"); File.WriteAllText(missed,"missed notification");
        watcher.Reconcile(); PumpUntil(()=>caught.Contains(missed));
        check(watcher.FullScanCount==scans+1,"recovery reconciliation finds a missed capture");
        native.EnableRaisingEvents=true;
    }
    internal static void PumpUntil(Func<bool> condition)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(!condition())
        {
            if(timer.Elapsed.TotalSeconds>5) throw new Exception("Timed out waiting for filesystem event");
            var frame=new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,()=>frame.Continue=false);
            Dispatcher.PushFrame(frame); Thread.Sleep(10);
        }
    }
}
