using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Automation;
using WinTendedero.Core;
using WinTendedero.Interop;

namespace WinTendedero.UI;

/// <summary>Everything you can change, on one page. Changes apply as you make them.</summary>
public sealed class SettingsWindow : Window
{
    private static SettingsWindow? _open;
    private readonly Action _apply;
    private readonly Func<string, bool>? _tryShortcut;
    private readonly Func<bool>? _shortcutRegistered;
    private TextBox _hotKey = null!;
    private TextBlock _shortcutStatus = null!;
    private Button _shortcutButton = null!;
    private bool _recordingShortcut;
    private ShortcutRecorder? _recorder;
    private ListBox _folders = null!;
    private TextBlock _inbox = null!;
    private TextBox _offset = null!;
    private bool _building;

    public static void Open(Action apply, Func<string, bool>? tryShortcut = null, Func<bool>? shortcutRegistered = null)
    {
        if (_open is not null) { _open.Activate(); return; }
        _open = new SettingsWindow(apply, tryShortcut, shortcutRegistered);
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    /// <summary>After a language change the page is rebuilt in place.</summary>
    public static void Rebuild()
    {
        if (_open is null) return;
        var apply = _open._apply;
        var tryShortcut = _open._tryShortcut;
        var shortcutRegistered = _open._shortcutRegistered;
        var left = _open.Left; var top = _open.Top;
        _open.Close();
        Open(apply, tryShortcut, shortcutRegistered);
        if (_open is not null) { _open.Left = left; _open.Top = top; }
    }

    /// <summary>The settings page as a plain element, for the film.</summary>
    public static FrameworkElement ForDemo(Action apply, Func<string, bool>? tryShortcut = null, Func<bool>? shortcutRegistered = null)
    {
        var w = new SettingsWindow(apply, tryShortcut, shortcutRegistered);
        var content = (FrameworkElement)w.Content;
        w.Content = null;
        if (content is ScrollViewer sv) { sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; sv.MaxHeight = double.PositiveInfinity; }
        var border = new Border { Width = 600, Background = w.Background, Child = content };
        System.Windows.Documents.TextElement.SetForeground(border, w.Foreground);
        return border;
    }

    private SettingsWindow(Action apply, Func<string, bool>? tryShortcut, Func<bool>? shortcutRegistered)
    {
        _apply = apply;
        _tryShortcut = tryShortcut;
        _shortcutRegistered = shortcutRegistered;
        Title = $"{Strings.AppName} · {Strings.SettingsTitle}";
        Width = Math.Min(600, SystemParameters.WorkArea.Width - 40);
        MinWidth = Math.Min(520, Width);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Icon = Brand.Icon;
        FlowDirection = Strings.Flow;
        FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        UseLayoutRounding = true;
        Paint();
        Theme.Changed += Paint;
        Closed += (_, _) => { Theme.Changed -= Paint; _recorder?.Dispose(); };
        Deactivated += (_, _) => { if (_recordingShortcut) EndShortcutCapture(); };
        Content = Build();
    }

    private void Paint()
    {
        Background = (Brush)Application.Current.Resources["MenuBg"];
        Foreground = (Brush)Application.Current.Resources["MenuFg"];
    }

    private UIElement Build()
    {
        _building = true;
        var s = Settings.Current;
        var stack = new StackPanel { Margin = new Thickness(24, 16, 24, 18) };
        var header = Brand.Lockup(26, Theme.AppsDark);
        header.Margin = new Thickness(0, 0, 0, 6);
        stack.Children.Add(header);

        stack.Children.Add(Heading(Strings.SectionActivation));
        var activationControls = new StackPanel { Margin = new Thickness(0, 8, 0, 4), IsEnabled = s.RevealAtTopEdge };
        stack.Children.Add(Check(Strings.RevealAtTopEdge, Strings.RevealAtTopEdgeTip, s.RevealAtTopEdge, v => { s.RevealAtTopEdge = v; activationControls.IsEnabled = v; }));
        activationControls.Children.Add(ActivationSlider(Strings.RevealDelayLabel, Strings.RevealDelayHelp, "RevealDelay", 0, 1500, 50, EdgeActivation.Delay(s.RevealDelay) * 1000, Strings.DelayValue, v => s.RevealDelay = v / 1000));
        activationControls.Children.Add(ActivationSlider(Strings.RevealBandLabel, Strings.RevealBandHelp, "RevealBand", 2, 24, 1, EdgeActivation.Band(s.RevealBand), Strings.BandValue, v => s.RevealBand = v));
        stack.Children.Add(activationControls);

        stack.Children.Add(Heading(Strings.SectionShortcut));
        var shortcutControls = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 6, 0, 0) };
        _hotKey = new TextBox { Text = s.HotKey, IsReadOnly = true, IsTabStop = false, Padding = new Thickness(8, 5, 8, 5), VerticalContentAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
        _hotKey.SetResourceReference(Control.ForegroundProperty, "MenuFg");
        _hotKey.SetResourceReference(Control.BackgroundProperty, "MenuBg");
        _hotKey.SetResourceReference(Control.BorderBrushProperty, "MenuBorder");
        AutomationProperties.SetAutomationId(_hotKey, "ShortcutInput");
        AutomationProperties.SetName(_hotKey, Strings.SectionShortcut);
        _hotKey.PreviewKeyDown += OnHotKeyPressed;
        _hotKey.PreviewMouseLeftButtonDown += (_, _) => { if (!_recordingShortcut) BeginShortcutCapture(); };
        _hotKey.LostKeyboardFocus += (_, e) => { if (_recordingShortcut && !ReferenceEquals(e.NewFocus, _shortcutButton)) EndShortcutCapture(); };
        _shortcutButton = new Button { Content = Strings.ChangeShortcut, MinWidth = 64, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetAutomationId(_shortcutButton, "ChangeShortcut");
        _shortcutButton.Click += (_, _) => { if (_recordingShortcut) EndShortcutCapture(); else BeginShortcutCapture(); };
        DockPanel.SetDock(_shortcutButton, Dock.Right);
        shortcutControls.Children.Add(_shortcutButton);
        shortcutControls.Children.Add(_hotKey);
        stack.Children.Add(new TextBlock { Text = Strings.Shortcut, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(shortcutControls);
        _shortcutStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8), FontSize = 12 };
        AutomationProperties.SetAutomationId(_shortcutStatus, "ShortcutStatus");
        AutomationProperties.SetLiveSetting(_shortcutStatus, AutomationLiveSetting.Polite);
        stack.Children.Add(_shortcutStatus);
        RefreshShortcutStatus();

        stack.Children.Add(Heading(Strings.SectionLine));
        stack.Children.Add(ActivationSlider(Strings.CaptureSize, Strings.CaptureSizeHint, "CardScale", 100, 200, 10, s.CardScale * 100, v => $"{v:0}%", v => s.CardScale = v / 100));
        stack.Children.Add(ActivationSlider(Strings.CaptureLimit, Strings.CaptureLimitHint, "MaxItems", 4, 64, 4, s.MaxItems ?? 24, v => $"{v:0}", v => s.MaxItems = (int)v));
        stack.Children.Add(Check(Strings.StayDownWhileUnused, Strings.StayDownWhileUnusedTip, s.StayDownWhileUnused, v => s.StayDownWhileUnused = v));
        stack.Children.Add(Check(Strings.TakeDownAfterDrag, Strings.TakeDownAfterDragTip, s.TakeDownAfterDrag, v => s.TakeDownAfterDrag = v));

        _offset = new TextBox { Text = ((int)s.LineOffset).ToString(), Width = 70, Padding = new Thickness(8, 4, 8, 4), ToolTip = Strings.LineDistanceTip, HorizontalAlignment = HorizontalAlignment.Left };
        _offset.LostFocus += (_, _) => { if (double.TryParse(_offset.Text, out var v)) { s.LineOffset = Math.Max(0, v); Changed(); } };
        _offset.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Keyboard.ClearFocus(); if (double.TryParse(_offset.Text, out var v)) { s.LineOffset = Math.Max(0, v); Changed(); } } };
        stack.Children.Add(Row(Strings.LineDistance, _offset, Strings.LineDistanceTip));

        // Look
        stack.Children.Add(Heading(Strings.SectionLook));
        var rope = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var key in Pegs.RopeColors) rope.Items.Add(ColorItem(key));
        rope.SelectedIndex = Math.Max(0, Array.IndexOf(Pegs.RopeColors, s.RopeColor));
        rope.SelectionChanged += (_, _) => { if (rope.SelectedIndex >= 0) { s.RopeColor = Pegs.RopeColors[rope.SelectedIndex]; Changed(); } };
        stack.Children.Add(Row(Strings.RopeColor, rope));

        var pegs = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var key in Pegs.Styles) pegs.Items.Add(PegItem(key));
        pegs.SelectedIndex = Math.Max(0, Array.IndexOf(Pegs.Styles, s.PegStyle));
        pegs.SelectionChanged += (_, _) => { if (pegs.SelectedIndex >= 0) { s.PegStyle = Pegs.Styles[pegs.SelectedIndex]; Changed(); } };
        stack.Children.Add(Row(Strings.PegStyle, pegs));

        stack.Children.Add(Check(Strings.Bows, null, s.Bows, v => s.Bows = v));
        stack.Children.Add(Check(Strings.AutoArrange, Strings.AutoArrangeTip, s.AutoArrange, v => s.AutoArrange = v));

        var appearance = Choice(new[] { ("auto", Strings.Auto), ("light", Strings.Light), ("dark", Strings.Dark) }, s.Appearance, v => s.Appearance = v);
        stack.Children.Add(Row(Strings.Appearance, appearance));

        var language = Choice(new[] { ("auto", Strings.Auto), ("zh", "简体中文"), ("en", "English"), ("ar", "العربية") }, s.Language, v => { s.Language = v; Dispatcher.BeginInvoke(Rebuild); });
        stack.Children.Add(Row(Strings.LanguageLabel, language));

        // Captures
        stack.Children.Add(Heading(Strings.SectionCaptures));
        stack.Children.Add(Check(Strings.CatchClipboard, Strings.CatchClipboardTip, s.CatchClipboard, v => s.CatchClipboard = v));

        var inboxRow = new DockPanel { Margin = new Thickness(0, 6, 0, 2) };
        var change = new Button { Content = Strings.Change, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(change, Dock.Right);
        change.Click += async (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.OpenInboxFolder, InitialDirectory = Inbox.Folder };
            if (dialog.ShowDialog(this) != true) return;
            change.IsEnabled = false;
            try
            {
                await Task.Run(() => CaptureLibrary.ChangeFolder(dialog.FolderName));
                Changed(); _inbox.Text = Inbox.Folder;
            }
            catch (Exception e) { Log.Error($"Library folder change failed: {e.Message}"); MessageBox.Show(this, Strings.FolderChangeFailed, Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { change.IsEnabled = true; }
        };
        inboxRow.Children.Add(change);
        _inbox = new TextBlock { Text = Inbox.Folder, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.7, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = Strings.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        var inboxLabel = new StackPanel();
        inboxLabel.Children.Add(new TextBlock { Text = Strings.CaughtCapturesFolder });
        inboxLabel.Children.Add(_inbox);
        inboxRow.Children.Add(inboxLabel);
        stack.Children.Add(inboxRow);
        stack.Children.Add(new TextBlock { Text = Strings.CaptureFolderHint, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 6, 0, 8) });
        var openLibrary = new Button { Content = Strings.LibraryOpen, Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Left };
        openLibrary.Click += (_, _) => Shell.OpenFolder(Inbox.Folder);
        stack.Children.Add(openLibrary);

        stack.Children.Add(new TextBlock { Text = Strings.WatchFoldersLabel, Margin = new Thickness(0, 12, 0, 4) });
        _folders = new ListBox { Height = 84, Margin = new Thickness(0, 0, 0, 6), FlowDirection = FlowDirection.LeftToRight };
        _folders.Items.Add(new ListBoxItem { Content = Shell.ScreenshotsFolder(), IsEnabled = false, Opacity = 0.6 });
        foreach (var f in s.WatchFolders) _folders.Items.Add(f);
        stack.Children.Add(_folders);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var add = new Button { Content = Strings.AddFolder, Padding = new Thickness(12, 4, 12, 4) };
        add.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.AddFolder };
            if (dialog.ShowDialog(this) == true && !s.WatchFolders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
            {
                s.WatchFolders.Add(dialog.FolderName);
                _folders.Items.Add(dialog.FolderName);
                Changed();
            }
        };
        var remove = new Button { Content = Strings.RemoveFolder, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) };
        remove.Click += (_, _) =>
        {
            if (_folders.SelectedItem is string f)
            {
                s.WatchFolders.RemoveAll(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase));
                _folders.Items.Remove(f);
                Changed();
            }
        };
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        stack.Children.Add(buttons);

        // General
        stack.Children.Add(Heading(Strings.SectionGeneral));
        stack.Children.Add(Check(Strings.Sounds, null, s.SoundOn, v => { s.SoundOn = v; Sounds.Enabled = v; }));
        stack.Children.Add(Check(Strings.StartWithWindows, null, Shell.StartsWithWindows, v =>
        {
            try { Shell.StartsWithWindows = v; } catch (Exception e) { Log.Error($"Could not change the startup setting: {e.Message}"); }
        }));

        stack.Children.Add(new TextBlock
        {
            Text = string.Format(Strings.SettingsFooter, System.IO.Path.Combine(Settings.Folder, "settings.json")),
            Margin = new Thickness(0, 16, 0, 0), Opacity = 0.6, FontSize = 11, TextWrapping = TextWrapping.Wrap,
        });
        _building = false;
        return new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = SystemParameters.WorkArea.Height - 80 };
    }

    private TextBlock Heading(string text) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 20, 0, 8),
    };

    private static DockPanel Row(string label, UIElement control, string? tip = null)
    {
        var row = new DockPanel { Margin = new Thickness(0, 6, 0, 2), LastChildFill = false };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 200, TextWrapping = TextWrapping.Wrap, ToolTip = tip };
        row.Children.Add(text);
        row.Children.Add(control);
        return row;
    }

    private CheckBox Check(string label, string? tip, bool value, Action<bool> set)
    {
        var box = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = value, Margin = new Thickness(0, 4, 0, 4), ToolTip = tip, Foreground = Foreground };
        box.Checked += (_, _) => { set(true); Changed(); };
        box.Unchecked += (_, _) => { set(false); Changed(); };
        return box;
    }

    private ComboBox Choice((string key, string label)[] options, string current, Action<string> set)
    {
        var box = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (_, label) in options) box.Items.Add(label);
        int index = Array.FindIndex(options, o => o.key == current);
        box.SelectedIndex = index < 0 ? 0 : index;
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) { set(options[box.SelectedIndex].key); Changed(); } };
        return box;
    }

    private static StackPanel ColorItem(string key)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Pegs.RopeColorOf(key)), Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)), StrokeThickness = 0.5, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = Strings.ColorName(key), VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static StackPanel PegItem(string key)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var peg = Pegs.Create(key, seed: 3);
        peg.Margin = new Thickness(0, 0, 8, 0);
        var box = new Grid { Width = 14, Height = 26, Margin = new Thickness(0, 0, 8, 0) };
        peg.Margin = new Thickness(1, -2, 0, 0);
        peg.LayoutTransform = new ScaleTransform(0.85, 0.85);
        box.Children.Add(peg);
        row.Children.Add(box);
        row.Children.Add(new TextBlock { Text = Strings.PegName(key), VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private UIElement ActivationSlider(string label, string hint, string id, double minimum, double maximum, double tick, double value, Func<double, string> format, Action<double> set)
    {
        var group = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var caption = new DockPanel();
        var readout = new TextBlock { Text = format(value), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(readout, Dock.Right);
        caption.Children.Add(readout);
        caption.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        group.Children.Add(caption);
        var slider = new Slider { Minimum = minimum, Maximum = maximum, TickFrequency = tick, IsSnapToTickEnabled = true, Value = value, SmallChange = tick, LargeChange = tick * 4, Margin = new Thickness(0, 7, 0, 4), MinHeight = 24 };
        AutomationProperties.SetAutomationId(slider, id);
        AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { readout.Text = format(slider.Value); set(slider.Value); Changed(); };
        group.Children.Add(slider);
        var help = new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        help.SetResourceReference(TextBlock.ForegroundProperty, "MenuFgSecondary");
        group.Children.Add(help);
        return group;
    }

    private void RefreshShortcutStatus(string? text = null, bool error = false)
    {
        bool available = _shortcutRegistered?.Invoke() ?? true;
        _shortcutStatus.Text = text ?? (available ? Strings.ShortcutReady : Strings.ShortcutUnavailable);
        _shortcutStatus.SetResourceReference(TextBlock.ForegroundProperty,
            error || (!available && text is null) ? "SettingsErrorFg" : "MenuFgSecondary");
    }

    private void BeginShortcutCapture()
    {
        _recordingShortcut = true;
        _shortcutButton.Content = Strings.CancelShortcut;
        _hotKey.Text = Strings.PressShortcut;
        _hotKey.Focus();
        RefreshShortcutStatus(Strings.ShortcutRecording);
        _recorder = new ShortcutRecorder(() => IsActive && _recordingShortcut && _hotKey.IsKeyboardFocusWithin, ApplyRecordedKey);
        if (!_recorder.Registered)
        {
            EndShortcutCapture();
            RefreshShortcutStatus(Strings.ShortcutRecorderUnavailable, error: true);
        }
    }

    private void EndShortcutCapture()
    {
        _recordingShortcut = false;
        _recorder?.Dispose();
        _recorder = null;
        _shortcutButton.Content = Strings.ChangeShortcut;
        _hotKey.Text = Settings.Current.HotKey;
        RefreshShortcutStatus();
    }

    private void OnHotKeyPressed(object sender, KeyEventArgs e)
    {
        if (!_recordingShortcut) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key == Key.Escape) { EndShortcutCapture(); return; }
        var mods = Keyboard.Modifiers;
        if (key == Key.Tab && mods == ModifierKeys.None) { EndShortcutCapture(); e.Handled = false; return; }
        uint modifiers = (mods.HasFlag(ModifierKeys.Control) ? Native.MOD_CONTROL : 0) |
                         (mods.HasFlag(ModifierKeys.Alt) ? Native.MOD_ALT : 0) |
                         (mods.HasFlag(ModifierKeys.Shift) ? Native.MOD_SHIFT : 0) |
                         (mods.HasFlag(ModifierKeys.Windows) ? Native.MOD_WIN : 0);
        ApplyRecordedKey((uint)KeyInterop.VirtualKeyFromKey(key), modifiers);
    }

    private void ApplyRecordedKey(uint key, uint modifiers)
    {
        if (!_recordingShortcut) return;
        if (key == 0x1B) { EndShortcutCapture(); return; }
        var parts = new List<string>();
        if ((modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Native.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Native.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & Native.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(((System.Windows.Forms.Keys)key).ToString());
        var text = string.Join("+", parts);
        if (!HotKey.TryNormalize(text, out var normalized)) { RefreshShortcutStatus(Strings.ShortcutInvalid, error: true); return; }
        if (_tryShortcut?.Invoke(normalized) != true)
        {
            _hotKey.Text = Settings.Current.HotKey;
            RefreshShortcutStatus(Strings.ShortcutConflict(normalized), error: true);
            return;
        }
        EndShortcutCapture();
    }

    private void Changed()
    {
        if (_building) return;
        Settings.Current.Save();
        _apply();
    }
}
