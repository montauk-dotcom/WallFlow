using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Wallflow.Core;
using Wallflow.Widgets;
using Wallflow.Views;

namespace Wallflow;

public partial class App : Application
{
    public static App Me => (App)Current;

    const string MutexName = "Wallflow.SingleInstance.montauk";
    const string ShowEventName = "Wallflow.Show.montauk";

    Mutex? _mutex;
    EventWaitHandle? _showEvent;
    public WidgetHost Widgets { get; } = new();
    public TrayIcon? Tray { get; private set; }
    readonly DispatcherTimer _slideshow = new(DispatcherPriority.Background);
    GalleryWindow? _gallery;
    SettingsWindow? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length >= 3 && e.Args[0] == "--live-test")
        {
            Motion.LiveTest.Run(e.Args[1], e.Args[2]);
            return;
        }
        _mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // already running → ask the other instance to show itself
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(() => OpenGallery()), null, -1, false);

        DispatcherUnhandledException += (_, ex) => { Log(ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log(ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Log(ex.Exception); ex.SetObserved(); };

        base.OnStartup(e);
        var s = SettingsStore.Load();
        try { Native.SetAutoStart(s.AutoStart); } catch (Exception ex) { Log(ex); }

        Tray = new TrayIcon();
        Backdrop.Start();
        Widgets.Start();
        Widgets.EditModeChanged += () => Tray?.Sync();
        ApplyDock();
        Live.Start();

        _slideshow.Tick += async (_, _) => await NextWallpaper();
        ApplySlideshow();

        Directory.CreateDirectory(SettingsStore.Dir);
        bool firstRun = !File.Exists(Path.Combine(SettingsStore.Dir, "settings.json.seen"));
        if (firstRun)
        {
            File.WriteAllText(Path.Combine(SettingsStore.Dir, "settings.json.seen"), DateTime.Now.ToString("O"));
            Tray.Balloon("Wallflow запущен", "Виджеты уже на рабочем столе. Значок в трее — обои, настройки, перестановка виджетов.");
            OpenSettings("widgets");
        }
        else if (!e.Args.Contains("--tray"))
        {
            OpenGallery();
        }

        // after startup settles, give unused memory back
        var trim = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(8) };
        trim.Tick += (_, _) => { trim.Stop(); Native.TrimMemory(); };
        trim.Start();
    }

    // ---------------- windows (created on demand, freed on close) ----------------
    public void OpenGallery()
    {
        if (_gallery is null)
        {
            _gallery = new GalleryWindow();
            _gallery.Closed += (_, _) => { _gallery = null; TrimSoon(); };
            _gallery.Show();
        }
        if (_gallery.WindowState == WindowState.Minimized) _gallery.WindowState = WindowState.Normal;
        _gallery.Activate();
    }

    public void OpenSettings(string tab = "widgets")
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => { _settings = null; TrimSoon(); };
            _settings.Show();
        }
        _settings.SelectTab(tab);
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    }

    void TrimSoon()
    {
        var t = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(2) };
        t.Tick += (_, _) => { t.Stop(); Native.TrimMemory(); };
        t.Start();
    }

    // ---------------- wallpapers ----------------
    public void ApplySlideshow()
    {
        _slideshow.Stop();
        var ss = SettingsStore.Current.Slideshow;
        if (ss.Enabled && ss.Minutes >= 1)
        {
            _slideshow.Interval = TimeSpan.FromMinutes(ss.Minutes);
            _slideshow.Start();
        }
        Tray?.Sync();
    }

    public async Task NextWallpaper()
    {
        try
        {
            var w = await WallpaperService.PickNext();
            if (w != null) { await WallpaperService.Apply(w); Backdrop.Invalidate(); }
        }
        catch (Exception ex)
        {
            Tray?.Balloon("Не удалось сменить обои", ex.Message);
        }
    }

    public void SetDesktopIconsHidden(bool hide)
    {
        SettingsStore.Current.HideDesktopIcons = hide;
        SettingsStore.Save();
        Native.SetDesktopIconsVisible(!hide);
        Tray?.Sync();
    }

    // ---------------- dock & taskbar ----------------
    DockBar.DockWindow? _dock;

    public void ApplyDock()
    {
        var d = SettingsStore.Current.Dock;
        if (d.Enabled)
        {
            DockBar.DockWindow.ImportPinnedOnce();
            if (d.AutoHideTaskbar)
            {
                try
                {
                    d.PrevTaskbarAutoHide ??= Native.TaskbarAutoHide;
                    Native.TaskbarAutoHide = true;
                }
                catch (Exception ex) { Log(ex); }
            }
            else RestoreTaskbar();
            if (_dock is null) { _dock = new DockBar.DockWindow(); _dock.Show(); }
            else _dock.ApplyLook();
        }
        else
        {
            _dock?.Close();
            _dock = null;
            RestoreTaskbar();
        }
        SettingsStore.Save();
    }

    public void RefreshDockLook() => _dock?.ApplyLook();

    void RestoreTaskbar()
    {
        var d = SettingsStore.Current.Dock;
        if (d.PrevTaskbarAutoHide is bool prev)
        {
            try { Native.TaskbarAutoHide = prev; } catch (Exception ex) { Log(ex); }
            d.PrevTaskbarAutoHide = null;
        }
    }

    public Motion.LiveEngine Live { get; } = new();

    public void Quit()
    {
        Live.Stop(keepSetting: true);
        _dock?.Close();
        _dock = null;
        RestoreTaskbar();
        Widgets.CloseAll();
        // never leave the user with an empty desktop
        if (SettingsStore.Current.HideDesktopIcons) Native.SetDesktopIconsVisible(true);
        Tray?.Dispose();
        SettingsStore.Save();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    public static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            Directory.CreateDirectory(SettingsStore.Dir);
            File.AppendAllText(Path.Combine(SettingsStore.Dir, "error.log"), $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch { }
    }
}
