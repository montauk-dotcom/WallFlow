using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.DockBar;

/// <summary>
/// Glass dock at the bottom of the screen: Start, search, pinned apps (imported from the Windows taskbar),
/// running windows and a clock. Optionally reserves screen space like a real taskbar and hides in fullscreen.
/// </summary>
public sealed class DockWindow : Window
{
    readonly WindowTracker _tracker = new();
    readonly Border _frame = new();
    readonly Border _tint = new();
    readonly StackPanel _items = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Background);
    readonly TextBlock _time = new(), _date = new();
    IntPtr _hwnd;
    bool _appBar;
    uint _appBarMsg;
    bool _hiddenForFullscreen;

    static DockSettings D => SettingsStore.Current.Dock;
    static WidgetSettings W => SettingsStore.Current.Widgets;
    double IconSize => Math.Clamp(D.IconSize, 24, 72);
    double BarHeight => IconSize + 22;
    const double BottomGap = 8;

    public DockWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Width;
        UseLayoutRounding = true;
        Title = "Wallflow dock";

        _tint.Child = _items;
        _tint.Padding = new Thickness(10, 0, 10, 0);
        _frame.Child = _tint;
        Content = _frame;

        _time.FontFamily = _date.FontFamily = UiKit.SysFont("Segoe UI Variable Text, Segoe UI");
        _time.Foreground = UiKit.White; _time.FontSize = 13; _time.FontWeight = FontWeights.SemiBold;
        _date.Foreground = UiKit.WhiteSoft; _date.FontSize = 11.5;
        _time.HorizontalAlignment = _date.HorizontalAlignment = HorizontalAlignment.Right;

        _tracker.Changed += () => { Build(); CheckFullscreen(); };
        Backdrop.Changed += Frost;
        _clockTimer.Tick += (_, _) => UpdateClock();
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { Place(); Frost(); };
        SizeChanged += (_, _) => { Place(); Frost(); };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplay;

        ApplyLook();
        Build();
        UpdateClock();
    }

    void OnDisplay(object? s, EventArgs e) => Dispatcher.BeginInvoke(() => { Place(); RegisterAppBar(); Frost(); });

    // ---------------- window setup ----------------
    void OnSourceInitialized(object? s, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var ex = (long)GetWindowLongPtr(_hwnd, -20);
        SetWindowLongPtr(_hwnd, -20, (IntPtr)(ex | 0x80 /*TOOLWINDOW*/ | 0x08000000 /*NOACTIVATE*/));
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        RegisterAppBar();
    }

    IntPtr WndProc(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x0021 /*WM_MOUSEACTIVATE*/) { handled = true; return (IntPtr)3; }
        if (_appBar && msg == _appBarMsg && w.ToInt32() == 2 /*ABN_FULLSCREENAPP*/)
            Dispatcher.BeginInvoke(CheckFullscreen);
        return IntPtr.Zero;
    }

    Rect MonitorDip()
    {
        var b = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is null) return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        var m = src.CompositionTarget.TransformFromDevice;
        return new Rect(m.Transform(new Point(b.Left, b.Top)), m.Transform(new Point(b.Right, b.Bottom)));
    }

    void Place()
    {
        Height = BarHeight;
        var mon = MonitorDip();
        Left = mon.Left + (mon.Width - ActualWidth) / 2;
        Top = mon.Bottom - BarHeight - BottomGap;
    }

    // ---------------- reserve space (AppBar) ----------------
    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    void RegisterAppBar()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (!D.ReserveSpace) { UnregisterAppBar(); return; }
        var d = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd };
        if (!_appBar)
        {
            _appBarMsg = RegisterWindowMessage("WallflowDockAppBar");
            d.uCallbackMessage = _appBarMsg;
            SHAppBarMessage(0 /*ABM_NEW*/, ref d);
            _appBar = true;
        }
        var scr = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        var dpi = VisualTreeHelper.GetDpi(this);
        int h = (int)Math.Ceiling((BarHeight + BottomGap * 2) * dpi.DpiScaleY);
        d.uEdge = 3; // bottom
        d.rc = new RECT { Left = scr.Left, Right = scr.Right, Bottom = scr.Bottom, Top = scr.Bottom - h };
        SHAppBarMessage(2 /*ABM_QUERYPOS*/, ref d);
        d.rc.Top = d.rc.Bottom - h;
        SHAppBarMessage(3 /*ABM_SETPOS*/, ref d);
    }

    void UnregisterAppBar()
    {
        if (!_appBar) return;
        var d = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd };
        SHAppBarMessage(1 /*ABM_REMOVE*/, ref d);
        _appBar = false;
    }

    /// <summary>Hide while a game or video is fullscreen.</summary>
    void CheckFullscreen()
    {
        bool fs = WindowTracker.IsFullscreen(_tracker.Foreground);
        if (fs == _hiddenForFullscreen) return;
        _hiddenForFullscreen = fs;
        if (fs) Hide(); else { Show(); Place(); }
    }

    // ---------------- look ----------------
    public void ApplyLook()
    {
        double r = Math.Min(W.Radius, BarHeight / 2);
        _frame.CornerRadius = new CornerRadius(r);
        _tint.CornerRadius = new CornerRadius(Math.Max(0, r - 1));
        _frame.BorderThickness = new Thickness(1);
        _frame.BorderBrush = new LinearGradientBrush(Color.FromArgb(90, 255, 255, 255), Color.FromArgb(25, 255, 255, 255), 90);
        var a = (byte)Math.Round(Math.Clamp(W.Opacity + 0.15, 0, 0.95) * 255);
        _tint.Background = W.Tint == "light" ? UiKit.Brush(a, 255, 255, 255) : UiKit.Brush(a, 10, 10, 18);
        Height = BarHeight;
        Frost();
        Build();
        RegisterAppBar();
    }

    void Frost()
    {
        if (W.GlassMode == "frost") _frame.Background = Backdrop.BrushFor(_frame) ?? UiKit.Brush(150, 16, 16, 26);
        else if (W.GlassMode == "tint") _frame.Background = UiKit.Brush(90, 10, 10, 18);
        else _frame.Background = Brushes.Transparent;
    }

    // ---------------- content ----------------
    sealed record Slot(string Key, string Name, string? LaunchPath, string IconPath, List<AppWindow> Windows, bool Pinned);

    void Build()
    {
        _items.Children.Clear();
        if (D.ShowStart) _items.Children.Add(GlyphButton(StartGlyph(), "Пуск", () => WindowTracker.SendKeys(0x5B)));
        if (D.ShowSearch) _items.Children.Add(GlyphButton(Glyph(""), "Поиск", () => WindowTracker.SendKeys(0x5B, 0x53)));
        if (D.ShowStart || D.ShowSearch) _items.Children.Add(Separator());

        var windows = D.ShowRunning ? _tracker.List() : new List<AppWindow>();
        var slots = new List<Slot>();
        foreach (var pin in D.Pinned.Where(File.Exists))
        {
            var target = Shortcuts.ResolveTarget(pin) ?? "";
            var exe = File.Exists(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target : "";
            var wins = exe == "" ? new List<AppWindow>() : windows.Where(w => w.ExePath.Equals(exe, StringComparison.OrdinalIgnoreCase)).ToList();
            slots.Add(new Slot(exe != "" ? exe.ToLowerInvariant() : pin.ToLowerInvariant(), Shortcuts.DisplayName(pin), pin, pin, wins, true));
        }
        var pinnedExes = new HashSet<string>(slots.Select(s => s.Key));
        var running = windows.Where(w => w.ExePath != "" && !pinnedExes.Contains(w.ExePath.ToLowerInvariant()))
            .GroupBy(w => w.ExePath.ToLowerInvariant())
            .Select(g => new Slot(g.Key, AppName(g.First()), g.First().ExePath, g.First().ExePath, g.ToList(), false))
            .ToList();

        foreach (var s in slots) _items.Children.Add(AppButton(s));
        if (running.Count > 0)
        {
            if (slots.Count > 0) _items.Children.Add(Separator());
            foreach (var s in running) _items.Children.Add(AppButton(s));
        }
        if (D.ShowClock)
        {
            _items.Children.Add(Separator());
            var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            if (_time.Parent is Panel old) old.Children.Clear();
            clock.Children.Add(_time);
            clock.Children.Add(_date);
            clock.ToolTip = "Календарь и уведомления";
            clock.MouseLeftButtonUp += (_, _) => WindowTracker.SendKeys(0x5B, 0x4E); // Win+N
            _items.Children.Add(clock);
        }
    }

    static string AppName(AppWindow w)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(w.ExePath);
            if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription!;
        }
        catch { }
        return System.IO.Path.GetFileNameWithoutExtension(w.ExePath);
    }

    FrameworkElement AppButton(Slot s)
    {
        double size = IconSize;
        var img = new Image { Source = ShellIcon.Get(s.IconPath, size > 48 ? 96 : 64), Width = size, Height = size };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        var scale = new ScaleTransform(1, 1);
        img.RenderTransformOrigin = new Point(0.5, 1);
        img.RenderTransform = scale;

        bool active = s.Windows.Any(w => w.Hwnd == _tracker.Foreground);
        var dot = new Border
        {
            Height = 4, Width = active ? 14 : 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Center,
            Background = s.Windows.Count > 0 ? (active ? UiKit.Hex("#A08BFF") : UiKit.WhiteSoft) : Brushes.Transparent,
            Margin = new Thickness(0, 3, 0, 0)
        };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(img);
        stack.Children.Add(dot);
        var b = new Border
        {
            Child = stack, Padding = new Thickness(5, 7, 5, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand,
            ToolTip = s.Windows.Count > 1 ? $"{s.Name} — окон: {s.Windows.Count}" : s.Windows.Count == 1 ? s.Windows[0].Title : s.Name
        };
        b.MouseEnter += (_, _) => Bounce(scale, 1.18);
        b.MouseLeave += (_, _) => Bounce(scale, 1.0);
        int cycle = 0;
        b.MouseLeftButtonUp += (_, _) =>
        {
            if (s.Windows.Count == 0) { Launch(s.LaunchPath); return; }
            if (s.Windows.Count == 1) { WindowTracker.ActivateOrMinimize(s.Windows[0].Hwnd, _tracker.Foreground); return; }
            // several windows: cycle through them
            var i = s.Windows.FindIndex(w => w.Hwnd == _tracker.Foreground);
            cycle = i >= 0 ? (i + 1) % s.Windows.Count : cycle % s.Windows.Count;
            WindowTracker.Activate(s.Windows[cycle].Hwnd);
        };
        b.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) Launch(s.LaunchPath); };
        b.ContextMenu = Menu(s);
        return b;
    }

    ContextMenu Menu(Slot s)
    {
        var m = new ContextMenu();
        foreach (var w in s.Windows)
        {
            var title = w.Title.Length > 60 ? w.Title[..57] + "…" : w.Title;
            var mi = new MenuItem { Header = title };
            var hwnd = w.Hwnd;
            mi.Click += (_, _) => WindowTracker.Activate(hwnd);
            m.Items.Add(mi);
        }
        if (s.Windows.Count > 0) m.Items.Add(new Separator());
        if (s.LaunchPath != null)
        {
            var open = new MenuItem { Header = s.Windows.Count > 0 ? "Открыть ещё одно окно" : "Открыть" };
            open.Click += (_, _) => Launch(s.LaunchPath);
            m.Items.Add(open);
        }
        if (s.Pinned)
        {
            var un = new MenuItem { Header = "Открепить от дока" };
            un.Click += (_, _) => { D.Pinned.RemoveAll(p => p.Equals(s.LaunchPath, StringComparison.OrdinalIgnoreCase)); SettingsStore.Save(); Build(); };
            m.Items.Add(un);
        }
        else if (s.LaunchPath != null)
        {
            var pin = new MenuItem { Header = "Закрепить в доке" };
            pin.Click += (_, _) => { D.Pinned.Add(s.LaunchPath); SettingsStore.Save(); Build(); };
            m.Items.Add(pin);
        }
        if (s.Windows.Count > 0)
        {
            var close = new MenuItem { Header = s.Windows.Count > 1 ? "Закрыть все окна" : "Закрыть окно" };
            close.Click += (_, _) => { foreach (var w in s.Windows) WindowTracker.Close(w.Hwnd); };
            m.Items.Add(close);
        }
        m.Items.Add(new Separator());
        var settings = new MenuItem { Header = "Настройки дока…" };
        settings.Click += (_, _) => App.Me.OpenSettings("taskbar");
        m.Items.Add(settings);
        return m;
    }

    static void Launch(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { Native.Launch(path); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Не удалось запустить", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    static void Bounce(ScaleTransform t, double to)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new QuadraticEase() };
        t.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    FrameworkElement GlyphButton(FrameworkElement glyph, string tip, Action click)
    {
        var b = new Border
        {
            Width = IconSize + 6, Height = IconSize + 6, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent,
            Cursor = Cursors.Hand, ToolTip = tip, Child = glyph, Margin = new Thickness(2, 0, 2, 0)
        };
        b.MouseEnter += (_, _) => b.Background = UiKit.Brush(30, 255, 255, 255);
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (_, _) => click();
        return b;
    }

    FrameworkElement Glyph(string g) => new TextBlock
    {
        Text = g, FontFamily = UiKit.SysFont("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = IconSize * 0.5,
        Foreground = UiKit.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };

    FrameworkElement StartGlyph()
    {
        // four-pane Windows logo, drawn so it doesn't depend on fonts
        double s = IconSize * 0.24, gap = IconSize * 0.04;
        var g = new Grid { Width = s * 2 + gap, Height = s * 2 + gap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var blue = new LinearGradientBrush(Color.FromRgb(90, 200, 255), Color.FromRgb(0, 120, 215), 45);
        foreach (var (x, y) in new[] { (0d, 0d), (s + gap, 0d), (0d, s + gap), (s + gap, s + gap) })
            g.Children.Add(new Rectangle { Width = s, Height = s, Fill = blue, RadiusX = 1.5, RadiusY = 1.5, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(x, y, 0, 0) });
        return g;
    }

    FrameworkElement Separator() => new Rectangle
    {
        Width = 1, Height = IconSize * 0.7, Fill = UiKit.Brush(60, 255, 255, 255), Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
    };

    void UpdateClock()
    {
        var now = DateTime.Now;
        _time.Text = now.ToString(W.Use24h ? "HH:mm" : "h:mm tt", Lang.Culture(W.Language));
        _date.Text = now.ToString("d", Lang.Culture(W.Language));
        _clockTimer.Interval = now.AddMinutes(1).AddSeconds(-now.Second) - now + TimeSpan.FromMilliseconds(50);
        _clockTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterAppBar();
        _tracker.Dispose();
        _clockTimer.Stop();
        Backdrop.Changed -= Frost;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplay;
        base.OnClosed(e);
    }

    // ---------------- import pinned apps ----------------
    public static void ImportPinnedOnce()
    {
        if (D.PinnedImported) return;
        try
        {
            var dir = Native.TaskbarPinnedDir;
            if (Directory.Exists(dir))
                D.Pinned = Directory.EnumerateFiles(dir, "*.lnk")
                    .OrderBy(f => File.GetCreationTimeUtc(f)).ToList();
        }
        catch (Exception ex) { App.Log(ex); }
        D.PinnedImported = true;
        SettingsStore.Save();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string msg);
    [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
}
