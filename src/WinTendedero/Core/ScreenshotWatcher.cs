using System.Collections.Concurrent;
using System.IO;
using System.Windows.Threading;

namespace WinTendedero.Core;

/// <summary>One startup snapshot, then metadata reads only for paths named by native notifications.</summary>
public sealed class ScreenshotWatcher : IDisposable
{
    public string Folder { get; }
    private readonly Action<string> _onNew, _onModified;
    private readonly Action _onChange;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DateTime _launch = DateTime.Now;
    private readonly Dictionary<string, Stamp> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _debounce, _retry;
    private FileSystemWatcher? _watcher;
    private bool _busy, _disposed, _fullScan, _initialized;
    private int _dispatchQueued;
    internal int FullScanCount { get; private set; }
    internal bool IsBusy => _busy;
    private readonly record struct Stamp(DateTime Written, long Length, DateTime Modified);

    public ScreenshotWatcher(string folder, Action<string> onNew, Action<string> onModified, Action onChange)
    {
        Folder = folder; _onNew = onNew; _onModified = onModified; _onChange = onChange;
        _debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(220) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Process(); };
        // Runs only after a monitoring failure, never as an ordinary polling loop.
        _retry = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromSeconds(15) };
        _retry.Tick += (_, _) => Recover();
    }

    public void Start() => Recover();

    private void Recover()
    {
        if (_disposed) return;
        try
        {
            Directory.CreateDirectory(Folder);
            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(Folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false, InternalBufferSize = 16 * 1024,
            };
            _watcher.Created += (_, e) => Touched(e.FullPath);
            _watcher.Changed += (_, e) => Touched(e.FullPath);
            _watcher.Deleted += (_, e) => Touched(e.FullPath);
            _watcher.Renamed += (_, e) => { Touched(e.OldFullPath); Touched(e.FullPath); };
            _watcher.Error += (_, e) => _dispatcher.BeginInvoke(() =>
            {
                if (_disposed) return;
                Log.Error($"Watcher recovery for {Folder}: {e.GetException().Message}");
                Recover();
            });
            _watcher.EnableRaisingEvents = true;
            _retry.Stop();
            Reconcile();
        }
        catch (Exception e)
        {
            Log.Error($"Cannot watch {Folder}: {e.Message}");
            _retry.Start();
        }
    }

    internal void Reconcile()
    {
        if (_disposed) return;
        _fullScan = true;
        Process();
    }

    private void Touched(string path)
    {
        if (!Thumbnails.IsImage(path) || _disposed) return;
        _pending[path] = 0;
        if (Interlocked.Exchange(ref _dispatchQueued, 1) != 0) return;
        _dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _dispatchQueued, 0);
            if (_disposed) return;
            _debounce.Stop(); _debounce.Start();
        });
    }

    private async void Process()
    {
        if (_disposed || _busy) return;
        _busy = true;
        bool full = _fullScan; _fullScan = false;
        var dirty = _pending.Keys.ToArray();
        foreach (var path in dirty) _pending.TryRemove(path, out _);
        try
        {
            var entries = await Task.Run(() => full ? Listing() : dirty.Select(path => (path, stamp: Read(path))).ToList());
            if (_disposed) return;
            var seen = full ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;
            bool changed = false;
            foreach (var (path, stamp) in entries)
            {
                if (stamp is not { } current) { changed |= _known.Remove(path); continue; }
                seen?.Add(path);
                if (!_known.TryGetValue(path, out var before))
                {
                    _known[path] = current;
                    if (_initialized || !full || current.Written >= _launch) _onNew(path);
                    changed = true;
                }
                else if (current != before)
                {
                    _known[path] = current;
                    _onModified(path);
                    changed = true;
                }
            }
            if (seen is not null)
                foreach (var gone in _known.Keys.Where(path => !seen.Contains(path)).ToArray()) changed |= _known.Remove(gone);
            _initialized = true;
            if (changed) _onChange();
        }
        catch (Exception e)
        {
            Log.Error($"Cannot inspect {Folder}: {e.Message}");
            _retry.Start();
        }
        finally
        {
            _busy = false;
            if (!_disposed && (_fullScan || !_pending.IsEmpty)) { _debounce.Stop(); _debounce.Start(); }
        }
    }

    private List<(string path, Stamp? stamp)> Listing()
    {
        FullScanCount++;
        return Directory.EnumerateFiles(Folder).Where(Thumbnails.IsImage).Select(path => (path, Read(path))).ToList();
    }

    private static Stamp? Read(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Name.StartsWith('~') || (file.Attributes & (FileAttributes.Hidden | FileAttributes.Temporary)) != 0) return null;
            return new Stamp(file.CreationTime > file.LastWriteTime ? file.CreationTime : file.LastWriteTime, file.Length, file.LastWriteTime);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void Dispose()
    {
        _disposed = true;
        _debounce.Stop(); _retry.Stop();
        _watcher?.Dispose(); _watcher = null;
        _pending.Clear();
    }
}
