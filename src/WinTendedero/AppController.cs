using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinTendedero.Core;
using WinTendedero.Interop;
using WinTendedero.UI;
using static WinTendedero.Interop.Native;

namespace WinTendedero;

/// <summary>
/// Runs the show: the tray icon, the shortcut, revealing and tucking away the
/// line, and the captures flying to it.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly string[] _previousSession = Settings.Current.Pegged.Concat(Settings.Current.PendingArchive).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private readonly Line _line = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private string? _visualSettings;
    private readonly LineWindow _panel;
    private readonly MessageWindow _messages = new();
    private readonly TrayIcon _tray;
    private readonly List<ScreenshotWatcher> _watchers = new();
    private readonly ClipboardWatcher _clipboard;
    private readonly ShortcutBinding _shortcut;
    private string? _requestedShortcut;
    private readonly DispatcherTimer _mouseTimer;
    private readonly DispatcherTimer _themeTimer;

    /// <summary>Whether the panel is shown. It can be shown and still tucked away above the top edge.</summary>
    private bool _isPresent;
    /// <summary>Whether the line has slid down into view.</summary>
    private bool _isRevealed;
    /// <summary>Opened on purpose with the shortcut or the menu: it stays down until the pointer has visited it and left.</summary>
    private bool _pinned;
    /// <summary>A new screenshot shows itself for a moment, then tucks away.</summary>
    private DateTime _peekUntil = DateTime.MinValue;
    private readonly EdgeActivation _edgeActivation = new();
    private DateTime? _awaySince;
    /// <summary>Whether the line should be up, if nothing prevents it. A full screen app on that screen does.</summary>
    private bool _wanted;
    /// <summary>Set when you open the line on purpose, so it stays up while empty.</summary>
    private bool _keepOpen;
    private int _lastLiveCount;
    /// <summary>The screen a new capture was taken on: the line goes there.</summary>
    private Display? _pendingMonitor;
    /// <summary>Captures that just hung from a file, so the clipboard copy of the same one is skipped.</summary>
    private readonly List<(DateTime when, int w, int h)> _recentFileCaptures = new();

    private static readonly TimeSpan RetractDelay = TimeSpan.FromSeconds(0.5);
    /// <summary>A click near the top edge means work in the window there, not a wish for the line. Stays set until the pointer leaves the edge.</summary>
    private bool _edgeSuppressed;
    private bool _buttonWasDown;

    public AppController()
    {
        OleInitialize(IntPtr.Zero);
        Shell.MigrateStartup();
        Originals.Sweep(_line.Items.Select(i => i.Path));
        Theme.ApplySetting();
        Theme.Apply();
        Strings.Refresh();

        _panel = new LineWindow(_line);
        _visualSettings = VisualSettings();
        _panel.PlaceOn();
        _panel.Show();
        _panel.OrderOut();
        _panel.PlaceOn();
        UpdateCapacity();

        _tray = new TrayIcon { MenuProvider = BuildMenu };
        _tray.LeftClick += Toggle;
        _panel.Canvas.MenuRequested += () => Menus.Show(BuildMenu());

        _clipboard = new ClipboardWatcher(_messages, OnClipboardCapture, RecentlyHungFromFile)
        {
            Enabled = Settings.Current.CatchClipboard,
        };
        _clipboard.Start();
        StartWatchers();
        _shortcut = new ShortcutBinding(_messages, Toggle);
        RegisterHotKey();
        ArchivePreviousSession();

        _line.Fall += Fall;
        _line.ItemsChanged += ItemsChanged;
        _lastLiveCount = _line.LiveCount;

        _mouseTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1.0 / 30) };
        _mouseTimer.Tick += (_, _) => Tick();

        _themeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _themeTimer.Tick += (_, _) => Theme.Refresh();
        _themeTimer.Start();

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => _panel.Dispatcher.BeginInvoke(() =>
        {
            _panel.PlaceOn(_panel.Display is { } m && Displays.All().Contains(m) ? m : null);
            UpdateCapacity();
        });
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) => _panel.Dispatcher.BeginInvoke(Theme.Refresh);

        if (_line.LiveCount > 0)
        {
            _wanted = true;
            Refresh();
        }
        else if (Settings.Current.RevealAtTopEdge) _mouseTimer.Start();

        if (!Settings.Current.Welcomed)
        {
            Settings.Current.Welcomed = true;
            Settings.Current.Save();
            Welcome();
        }
    }

    // MARK: Watching for captures

    private void StartWatchers()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        var folders = new List<string> { Shell.ScreenshotsFolder(), Inbox.Folder };
        folders.AddRange(Settings.Current.WatchFolders.Select(Environment.ExpandEnvironmentVariables));
        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var watcher = new ScreenshotWatcher(folder,
                onNew: path => HangCapture(path, fromFile: true),
                onModified: path => _line.ReloadThumbnail(path),
                onChange: () => _line.Prune());
            watcher.Start();
            _watchers.Add(watcher);
            Log.Notice($"Watching {folder}");
        }
    }

    private void RegisterHotKey()
    {
        _requestedShortcut = Settings.Current.HotKey;
        if (!_shortcut.TrySet(Settings.Current.HotKey)) Log.Error($"Could not register the shortcut {Settings.Current.HotKey}");
    }

    public bool TrySetShortcut(string text)
    {
        if (!_shortcut.TrySet(text)) return false;
        Settings.Current.HotKey = _shortcut.Text!;
        _requestedShortcut = _shortcut.Text;
        Settings.Current.Save();
        return true;
    }

    private bool RecentlyHungFromFile(int w, int h)
    {
        var cutoff = DateTime.Now.AddSeconds(-4);
        _recentFileCaptures.RemoveAll(c => c.when < cutoff);
        return _recentFileCaptures.Any(c => c.w == w && c.h == h);
    }

    private void OnClipboardCapture(string path, int w, int h) => HangCapture(path, fromFile: false);

    // MARK: The capture flying to the line

    /// <summary>A new screenshot lifts off from where it was taken and flies to its place on the line.</summary>
    private async void HangCapture(string path, bool fromFile)
    {
        if (_disposed || CaptureLibrary.IsInternalWrite(path) || !_loading.Add(path)) return;
        await _captureGate.WaitAsync();
        try
        {
            if (_disposed) return;
            var result = await Task.Run(() =>
            {
                var thumb = Thumbnails.Load(path);
                return (thumb, saved: thumb is null ? path : CaptureLibrary.Store(path));
            });
            if (_disposed || result.thumb is not { } thumb) return;
            if (fromFile) _recentFileCaptures.Add((DateTime.Now, thumb.PixelWidth, thumb.PixelHeight));
            var from = CaptureRect.Infer(thumb.PixelWidth, thumb.PixelHeight);
            if (from is not null) _pendingMonitor = from.Value.monitor;
            // Reuse the already decoded thumbnail for the arrival, keeping full-size images off the UI thread.
            var id = _line.Hang(result.saved, flying: from is not null && SystemParameters.ClientAreaAnimation, preloaded: thumb);
            if (id is null || from is null || !SystemParameters.ClientAreaAnimation) return;
            Later(0.03, () => Fly(id.Value, from.Value.rect, from.Value.monitor));
        }
        catch (Exception e) { Log.Error($"Could not archive capture: {e.Message}"); _tray.Balloon(Strings.AppName, "截图保存失败，请检查保存位置和磁盘空间。原文件仍在原位置。"); }
        finally { _loading.Remove(path); _captureGate.Release(); }
    }

    private async void ArchivePreviousSession()
    {
        var completed = new List<string>();
        await Task.Run(() =>
        {
            foreach (var path in _previousSession)
            {
                try { if (File.Exists(path) && !Notes.IsNote(path)) CaptureLibrary.Store(path); completed.Add(path); }
                catch (Exception e) { Log.Error($"Could not archive previous session: {e.Message}"); }
            }
        });
        Settings.Current.PendingArchive.RemoveAll(completed.Contains);
        Settings.Current.Save();
    }

    private void Fly(Guid id, RECT from, Display monitor)
    {
        var item = _line.Find(id);
        if (item is null) return;
        if (!_isPresent || !_isRevealed || _panel.Display is null || !_panel.Display.Equals(monitor) || CardFrame(id) is not { } to)
        {
            _line.Land(id);
            return;
        }
        FlightWindow.Fly(item.Thumb, from, to, item.Tilt, monitor, Seed(item), () => _line.Land(id));
    }

    /// <summary>A discarded card falls over the whole screen, from where it hangs.</summary>
    private void Fall(Pegged item)
    {
        if (!_isPresent || !_isRevealed || item.Flying || _panel.Display is null) return;
        if (CardFrame(item.Id) is not { } card) return;
        FlightWindow.Fall(item.Thumb, card, item.Tilt, _panel.Display, Seed(item));
    }

    private static int Seed(Pegged item) => item.Path.Aggregate(17, (h, c) => h * 31 + c);

    /// <summary>Where a card hangs, in screen pixels, using the same layout as the canvas.</summary>
    private RECT? CardFrame(Guid id)
    {
        var card = _panel.Canvas.Cards.FirstOrDefault(c => c.Item.Id == id);
        if (card is null || _panel.Display is null) return null;
        var item = card.Item;
        var live = _line.Items.Where(i => !i.Falling).ToList();
        int index = live.IndexOf(item);
        if (index < 0) return null;
        double width = _panel.Canvas.ActualWidth;
        double x = card.TargetX > 0 ? card.TargetX : Layout.X(index, live.Count, width);
        double viewTop = Layout.RopeY(x, width) - Layout.PinAbove;
        double cardTop = viewTop + Layout.CardOffsetBelowTop;
        var size = Layout.CardSize(item.PixelWidth, item.PixelHeight);
        var frame = _panel.Frame;
        double s = _panel.Display.Scale;
        int l = frame.Left + (int)Math.Round((x - size.Width / 2) * s);
        int t = frame.Top + (int)Math.Round(cardTop * s);
        return new RECT(l, t, l + (int)Math.Round(size.Width * s), t + (int)Math.Round(size.Height * s));
    }

    // MARK: Showing and hiding

    private void ItemsChanged()
    {
        int live = _line.LiveCount;
        if (live > _lastLiveCount)
        {
            _panel.PlaceOn(_pendingMonitor);
            _pendingMonitor = null;
            UpdateCapacity();
            _wanted = true;
            Refresh();
            Reveal(peekFor: 2.5, reason: "new capture");
        }
        else if (live == 0 && !_keepOpen)
        {
            Later(0.7, () =>
            {
                if (_line.LiveCount != 0 || _keepOpen) return;
                _wanted = false;
                Refresh();
            });
        }
        _lastLiveCount = live;
    }

    /// <summary>Decides whether the panel is shown at all: something to show, and no full screen app on that screen.</summary>
    private void Refresh()
    {
        var monitor = _panel.Display ?? Displays.UnderPointer();
        bool blocked = FullScreen.IsActive(monitor);
        if (_wanted && !blocked) Present(); else Dismiss();
        // The pointer is watched while there is a line, even tucked away, to notice it pushing against the top edge.
        if (_wanted || Settings.Current.RevealAtTopEdge) _mouseTimer.Start(); else StopMouseTracking();
    }

    private void Present()
    {
        if (_isPresent) return;
        _isPresent = true;
        _panel.OrderFront();
    }

    private void Dismiss()
    {
        if (!_isPresent) return;
        _isPresent = false;
        SetRevealed(false);
        Later(0.4, () => { if (!_isPresent) _panel.OrderOut(); });
    }

    private void Reveal(bool pinned = false, double peekFor = 0, string reason = "edge")
    {
        if (!_isPresent) return;
        if (!_isRevealed) Log.Notice($"Reveal: {reason}");
        if (pinned) _pinned = true;
        if (peekFor > 0) _peekUntil = DateTime.Now.AddSeconds(peekFor);
        _awaySince = null;
        SetRevealed(true);
    }

    private void SetRevealed(bool on)
    {
        if (on == _isRevealed) return;
        _isRevealed = on;
        _panel.Canvas.Revealed = on;
        if (!on)
        {
            _pinned = false;
            _peekUntil = DateTime.MinValue;
            _panel.ClickThrough = true;
            _panel.Canvas.UpdateHover(null);
        }
    }

    public void Toggle()
    {
        if (_isRevealed)
        {
            SetRevealed(false);
            if (_line.LiveCount == 0)
            {
                _keepOpen = false;
                _wanted = false;
                Refresh();
            }
        }
        else
        {
            _keepOpen = true;
            _wanted = true;
            _panel.PlaceOn();
            UpdateCapacity();
            Refresh();
            Reveal(pinned: true, reason: "toggle");
        }
    }

    private void StopMouseTracking()
    {
        _mouseTimer.Stop();
        _edgeActivation.Reset();
        _panel.Canvas.UpdateHover(null);
    }

    private static bool ButtonDown() => (GetAsyncKeyState(0x01) < 0) || (GetAsyncKeyState(0x02) < 0) || (GetAsyncKeyState(0x04) < 0);

    private void Tick()
    {
        var mouse = Displays.Cursor();
        var now = DateTime.Now;
        var displayUnderPointer = Displays.Containing(mouse);
        bool inHotBand = displayUnderPointer is not null && displayUnderPointer.HotBand.Contains(mouse);
        bool buttonDown = ButtonDown();
        bool justPressed = buttonDown && !_buttonWasDown;
        _buttonWasDown = buttonDown;
        if (!inHotBand) _edgeSuppressed = false;

        if (_isPresent && _panel.Display is { } current && FullScreen.IsActive(current))
        {
            _edgeActivation.Reset();
            Dismiss();
            return;
        }
        if (!_isPresent && _wanted) Refresh();

        if (!_isRevealed)
        {
            _panel.ClickThrough = true;
            // Clicks and drags in the band still belong to the application underneath.
            if (buttonDown && inHotBand) _edgeSuppressed = true;
            bool eligible = Settings.Current.RevealAtTopEdge && displayUnderPointer is not null && inHotBand && !buttonDown
                           && !_edgeSuppressed && !FullScreen.IsActive(displayUnderPointer);
            if (_edgeActivation.Update(eligible, displayUnderPointer?.Handle ?? IntPtr.Zero,
                    System.Diagnostics.Stopwatch.GetElapsedTime(0), Settings.Current.RevealDelay))
            {
                _edgeActivation.Reset();
                if (_panel.Display is null || !_panel.Display.Equals(displayUnderPointer))
                {
                    _panel.PlaceOn(displayUnderPointer);
                    UpdateCapacity();
                }
                _wanted = true;
                Refresh();
                Reveal();
            }
            return;
        }

        bool dragging = DragSource.IsDragging || _line.DraggingId is not null;
        bool holding = _line.PressedId is not null || _panel.Canvas.RopeDragging || _panel.Canvas.Cards.Any(c => c.Reordering);
        _panel.HoldMouse = dragging || holding;
        _panel.ClickThrough = !(dragging || holding || _line.MenuOpen || _panel.IsOverPhoto(mouse));
        var local = _panel.ToCanvas(mouse);
        _panel.Canvas.UpdateHover(dragging ? null : local);

        // A click that goes through the strip into the window underneath puts
        // the line away at once: you are working there.
        if (justPressed && !dragging && !holding && !_line.MenuOpen && !_panel.IsOverPhoto(mouse))
        {
            _edgeSuppressed = inHotBand;
            SetRevealed(false);
            return;
        }

        // The line's zone runs from its lowest point up to the top of the screen, so moving up never hides it.
        var frame = _panel.Frame;
        var zone = frame;
        if (_panel.Display is { } m) zone.Top = m.Bounds.Top;
        bool inside = zone.Contains(mouse);
        if (inside && _pinned) _pinned = false;

        bool pendingHold = Settings.Current.StayDownWhileUnused && _line.HasUnused;
        bool busy = _pinned || dragging || holding || _line.MenuOpen || now < _peekUntil || pendingHold;
        if (inside || busy)
        {
            _awaySince = null;
        }
        else
        {
            _awaySince ??= now;
            if (now - _awaySince.Value >= RetractDelay)
            {
                _awaySince = null;
                SetRevealed(false);
            }
        }
    }

    private void UpdateCapacity()
    {
        _line.MaxItems = Math.Clamp(Settings.Current.MaxItems ?? 24, 4, 64);
    }

    // MARK: Welcome

    private void Welcome()
    {
        _keepOpen = true;
        _wanted = true;
        Refresh();
        Reveal(pinned: true, reason: "welcome");
        _tray.Balloon(Strings.WelcomeTitle, string.Format(Strings.WelcomeBody, Settings.Current.HotKey));
        Later(6, () =>
        {
            if (_line.LiveCount != 0) return;
            _keepOpen = false;
            _wanted = false;
            Refresh();
        });
    }

    /// <summary>Settings changed: the shortcut, the folders and the clipboard watcher follow.</summary>
    public void ApplySettings()
    {
        _line.FollowLibraryPaths();
        UpdateCapacity();
        Theme.ApplySetting();
        Strings.Refresh();
        string visual = VisualSettings();
        if (_visualSettings != visual) { _visualSettings = visual; Pegs.RaiseLookChanged(); }
        _tray.Repaint();
        if (_panel.Display is { } d) _panel.PlaceOn(d);
        if (_requestedShortcut != Settings.Current.HotKey) RegisterHotKey();
        _edgeActivation.Reset();
        Refresh();
        _clipboard.Enabled = Settings.Current.CatchClipboard;
        var wanted = new List<string> { Shell.ScreenshotsFolder(), Inbox.Folder };
        wanted.AddRange(Settings.Current.WatchFolders.Select(Environment.ExpandEnvironmentVariables));
        var current = _watchers.Select(w => w.Folder).ToList();
        if (!wanted.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).SequenceEqual(current.OrderBy(x => x), StringComparer.OrdinalIgnoreCase)) StartWatchers();
    }

    // MARK: Menu

    private static string VisualSettings() => $"{Settings.Current.RopeColor}|{Settings.Current.PegStyle}|{Settings.Current.Bows}|{Settings.Current.CardScale}|{Settings.Current.AutoArrange}";

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu { FlowDirection = Strings.Flow };
        menu.Opened += (_, _) => _line.MenuOpen = true;
        menu.Closed += (_, _) => _line.MenuOpen = false;
        menu.Items.Add(PeggedControl.MenuItemFor(_isRevealed ? Strings.HideLine : Strings.ShowLine, Toggle, Settings.Current.HotKey));
        var clear = PeggedControl.MenuItemFor(Strings.TakeEverythingDown, () => _line.Clear());
        clear.IsEnabled = _line.LiveCount > 0;
        menu.Items.Add(clear);
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.NewCapture, Shell.Snip, "Win+Shift+S"));
        var note = new MenuItem { Header = Strings.NewNote };
        foreach (var key in Notes.Colors)
        {
            var k = key;
            var item = new MenuItem { Header = Strings.NoteName(k), Icon = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Notes.Paper(k)) } };
            item.Click += (_, _) => { _keepOpen = true; _wanted = true; _panel.PlaceOn(); UpdateCapacity(); Refresh(); _line.NewNote(k); Reveal(pinned: true, reason: "note"); };
            note.Items.Add(item);
        }
        menu.Items.Add(note);
        menu.Items.Add(new Separator());

        var catchItem = PeggedControl.MenuItemFor(Strings.CatchClipboard, () =>
        {
            Settings.Current.CatchClipboard = !Settings.Current.CatchClipboard;
            Settings.Current.Save();
            _clipboard.Enabled = Settings.Current.CatchClipboard;
        });
        catchItem.IsChecked = Settings.Current.CatchClipboard;
        catchItem.ToolTip = Strings.CatchClipboardTip;
        menu.Items.Add(catchItem);
        var stay = PeggedControl.MenuItemFor(Strings.StayDownWhileUnused, () =>
        {
            Settings.Current.StayDownWhileUnused = !Settings.Current.StayDownWhileUnused;
            Settings.Current.Save();
        });
        stay.IsChecked = Settings.Current.StayDownWhileUnused;
        stay.ToolTip = Strings.StayDownWhileUnusedTip;
        menu.Items.Add(stay);
        var afterDrag = PeggedControl.MenuItemFor(Strings.TakeDownAfterDrag, () =>
        {
            Settings.Current.TakeDownAfterDrag = !Settings.Current.TakeDownAfterDrag;
            Settings.Current.Save();
        });
        afterDrag.IsChecked = Settings.Current.TakeDownAfterDrag;
        afterDrag.ToolTip = Strings.TakeDownAfterDragTip;
        menu.Items.Add(afterDrag);
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.OpenScreenshotsFolder, () => Shell.OpenFolder(Shell.ScreenshotsFolder())));
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.OpenInboxFolder, () => Shell.OpenFolder(Inbox.Folder)));
        menu.Items.Add(new Separator());

        var sound = PeggedControl.MenuItemFor(Strings.Sounds, () => _line.SoundOn = !_line.SoundOn);
        sound.IsChecked = _line.SoundOn;
        menu.Items.Add(sound);
        var login = PeggedControl.MenuItemFor(Strings.StartWithWindows, () =>
        {
            try { Shell.StartsWithWindows = !Shell.StartsWithWindows; }
            catch (Exception e) { Log.Error($"Could not change the startup setting: {e.Message}"); }
        });
        login.IsChecked = Shell.StartsWithWindows;
        menu.Items.Add(login);
        menu.Items.Add(new Separator());
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.Settings, () => SettingsWindow.Open(ApplySettings, TrySetShortcut, () => _shortcut.Registered)));
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.About, ShowAbout));
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.Quit, () => Application.Current.Shutdown()));
        return menu;
    }

    private void ShowAbout()
    {
        var version = typeof(AppController).Assembly.GetName().Version?.ToString(3) ?? "1.0";
        MessageBox.Show(
            $"{Strings.AppName} {version}\n{Strings.Tagline}\n\n" +
            string.Format(Strings.AboutBody, Settings.Current.HotKey) + "\n\n" +
            $"{Strings.AboutCaptures}\n{Inbox.Folder}\n\n{Strings.AboutSettings}: {Path.Combine(Settings.Folder, "settings.json")}",
            Strings.About, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK,
            Strings.IsRtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
    }

    private static void Later(double seconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(0.001, seconds)) };
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        timer.Start();
    }

    public void Dispose()
    {
        _disposed = true;
        _mouseTimer.Stop();
        _themeTimer.Stop();
        foreach (var w in _watchers) w.Dispose();
        _clipboard.Dispose();
        _shortcut.Dispose();
        _tray.Dispose();
        _messages.Dispose();
    }
}
