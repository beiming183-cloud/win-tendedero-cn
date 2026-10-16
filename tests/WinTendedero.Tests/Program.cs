using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTendedero.Core;
using WinTendedero.Interop;
using WinTendedero.UI;

internal static class Tests
{
    private static int _checks;
    [STAThread]
    static int Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "win-tendedero-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var data = Path.Combine(root, "profile");
        Directory.CreateDirectory(data);
        Environment.SetEnvironmentVariable("WIN_TENDEDERO_DATA_DIR", data);
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        File.WriteAllText(Path.Combine(data, "settings.json"), """{"revealDelay":0.5,"language":"zh","soundOn":false,"welcomed":true,"hotKey":"Ctrl+Alt+T"}""");
        try
        {
            Run(root);
            LibraryChecks.Run(root, Check);
            WatcherChecks.Run(root, Check);
            LooksChecks.Run(root, Check);
            Console.WriteLine($"PASS: {_checks} activation, native shortcut, library, session, pagination and watcher checks.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL: " + name);
        _checks++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string root)
    {
        Check(Settings.Folder == Path.Combine(root, "profile"), "isolated data folder");
        Check(Settings.Current.RevealDelay == 0.2 && Settings.Current.RevealBand == 8, "upgrade hidden legacy defaults");
        Check(!Settings.Current.SoundOn && Settings.Current.Language == "zh", "preserve unrelated preferences");
        var trigger = new EdgeActivation();
        IntPtr first = new(1), second = new(2);
        bool At(int ms, bool eligible = true, IntPtr? screen = null, double delay = 0.2) => trigger.Update(eligible, screen ?? first, TimeSpan.FromMilliseconds(ms), delay);
        Check(!At(0) && !At(199) && At(200), "continuous band dwell reaches the configured threshold");
        trigger.Reset();
        Check(!At(0) && !At(150, false) && !At(200) && !At(399) && At(400), "leaving or blocking resets the dwell");
        trigger.Reset();
        Check(!At(0) && !At(150, screen: second) && !At(349, screen: second) && At(350, screen: second), "changing monitors restarts dwell");
        trigger.Reset();
        Check(At(0, delay: 0), "zero delay reveals immediately");
        trigger.Reset();
        Check(!At(0, delay: 1.2) && !At(1199, delay: 1.2) && At(1200, delay: 1.2), "slow setting is honored");
        Check(EdgeActivation.Delay(double.NaN) == 0.2 && EdgeActivation.Delay(-1) == 0 && EdgeActivation.Delay(9) == 1.5, "invalid delays are bounded");
        Check(EdgeActivation.PhysicalBand(8, 1.5) == 12 && EdgeActivation.PhysicalBand(24, 2) == 48, "activation range follows screen DPI");
        var display = new Display(first, new Native.RECT(-1920, -1080, 0, 0), new Native.RECT(-1920, -1040, 0, 0), 144, false);
        Check(display.HotBand.Contains(new Native.POINT(-500, -1074)) && !display.HotBand.Contains(new Native.POINT(-500, -1067)), "real band handles negative monitor coordinates and top taskbars");
        foreach (string invalid in new[] { "T", "Shift+T", "Ctrl+Shift", "Ctrl+A+B", "Ctrl++T", "Ctrl+999", "Ctrl+LButton", "Ctrl+ControlKey", "Ctrl+F25" })
            Check(!HotKey.Parse(invalid, out _, out _), "reject invalid global shortcut " + invalid);
        Check(HotKey.TryNormalize("alt+control+1", out var digits) && digits == "Ctrl+Alt+D1", "digit keys use real keyboard key codes");
        Check(HotKey.TryNormalize("Win+Shift+F9", out var win) && win == "Shift+Win+F9", "Windows and function keys normalize");

        var app = new WinTendedero.App();
        app.InitializeComponent();
        Strings.Refresh();
        Theme.ApplySetting();
        Theme.Apply();
        using var messages = new MessageWindow();
        using var competingMessages = new MessageWindow();
        var free = new List<string>();
        for (int n = 13; n <= 24 && free.Count < 3; n++)
        {
            string candidate = $"Ctrl+Alt+Shift+F{n}";
            using var probe = new HotKey(competingMessages, candidate, () => { });
            if (probe.Registered) free.Add(candidate);
        }
        Check(free.Count == 3, "three temporary test shortcuts are available");
        using var binding = new ShortcutBinding(messages, () => { });
        Check(binding.TrySet(free[0]), "initial native registration");
        using (var occupied = new HotKey(competingMessages, free[1], () => { }))
        {
            Check(occupied.Registered && !binding.TrySet(free[1]), "native conflict rejects replacement");
            using var oldProbe = new HotKey(competingMessages, free[0], () => { });
            Check(binding.Text == free[0] && !oldProbe.Registered, "conflict keeps the old shortcut registered");
        }
        Check(!binding.TrySet("Ctrl+A+B") && binding.Text == free[0], "invalid replacement preserves the working shortcut");
        Check(binding.TrySet(free[2]), "valid replacement is registered");
        using (var oldProbe = new HotKey(competingMessages, free[0], () => { }))
            Check(oldProbe.Registered, "successful replacement releases the old key");
        Settings.Current.HotKey = binding.Text!;
        var page = SettingsWindow.ForDemo(() => { }, candidate =>
        {
            if (!binding.TrySet(candidate)) return false;
            Settings.Current.HotKey = binding.Text!;
            Settings.Current.Save();
            return true;
        }, () => binding.Registered);
        Layout(page);
        var delay = Find<Slider>(page, "RevealDelay");
        var band = Find<Slider>(page, "RevealBand");
        delay.Value = 650;
        band.Value = 14;
        using (var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(Settings.Folder, "settings.json"))))
            Check(saved.RootElement.GetProperty("revealDelay").GetDouble() == 0.65 && saved.RootElement.GetProperty("revealBand").GetDouble() == 14, "both real slider controls persist settings");
        var change = Find<Button>(page, "ChangeShortcut");
        var input = Find<TextBox>(page, "ShortcutInput");
        var status = Find<TextBlock>(page, "ShortcutStatus");
        change.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check((string)change.Content == Strings.CancelShortcut && input.Text == Strings.PressShortcut, "explicit shortcut recording state");
        input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, HwndSource.FromHwnd(messages.Handle), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Check((string)change.Content == Strings.ChangeShortcut && input.Text == binding.Text, "Escape cancels without changing the shortcut");
        change.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        change.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check((string)change.Content == Strings.ChangeShortcut, "cancel button exits recording");
        delay.Value = 200; band.Value = 8;
        Render(page, Path.Combine(root, "settings-light.png"));
        Theme.Override(true);
        var dark = SettingsWindow.ForDemo(() => { }, _ => false, () => false);
        Render(dark, Path.Combine(root, "settings-dark-conflict.png"));
        Check(Find<TextBlock>(dark, "ShortcutStatus").Text == Strings.ShortcutUnavailable, "startup conflicts are visible in settings");
        binding.Dispose();
        using var released = new HotKey(competingMessages, free[2], () => { });
        Check(released.Registered, "disposing releases the active global shortcut");
    }

    private static void Layout(FrameworkElement page)
    {
        page.Measure(new Size(600, double.PositiveInfinity));
        page.Arrange(new Rect(new Point(0, 0), page.DesiredSize));
        page.UpdateLayout();
    }

    private static T Find<T>(DependencyObject root, string id) where T : DependencyObject
    {
        if (root is T result && AutomationProperties.GetAutomationId(root) == id) return result;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            try { return Find<T>(VisualTreeHelper.GetChild(root, i), id); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("Control not found: " + id);
    }

    private static void Render(FrameworkElement page, string path)
    {
        Layout(page);
        var bitmap = new RenderTargetBitmap(900, (int)Math.Ceiling(page.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(page);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        png.Save(stream);
    }
}
