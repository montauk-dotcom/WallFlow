using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Wallflow.Core;
using Wallflow.DockBar;

namespace Wallflow.Motion;

/// <summary>
/// Video wallpaper: a borderless window per monitor, re-parented behind the desktop icons
/// (the WorkerW trick used by Wallpaper Engine / Lively). Pauses when a window covers the desktop.
/// </summary>
public sealed class LiveEngine
{
    readonly List<LiveWindow> _windows = new();
    WindowTracker? _tracker;
    bool _locked;

    static LiveSettings L => SettingsStore.Current.Live;
    public bool IsPlaying => _windows.Count > 0;
    public event Action? StateChanged;

    public void Start()
    {
        SystemEvents.SessionSwitch += (_, e) =>
        {
            _locked = e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect;
            Application.Current.Dispatcher.BeginInvoke(UpdatePause);
        };
        if (L.Enabled && File.Exists(L.File)) Play(L.File);
    }

    /// <summary>Download (if needed) and start a wallpaper from the gallery.</summary>
    public async Task Apply(LiveWallpaper w, IProgress<double>? progress = null)
    {
        var file = await LiveApi.Download(w, progress);
        // the preview frame becomes the normal wallpaper: used by the glass widgets and when Wallflow is closed
        try
        {
            var thumb = await LiveApi.DownloadThumb(w);
            if (thumb != null) { Native.SetWallpaper(thumb, "fill"); Backdrop.Invalidate(); }
        }
        catch (Exception ex) { App.Log(ex); }
        L.Title = w.Title;
        PlayAndRemember(file);
    }

    public void PlayAndRemember(string file)
    {
        L.File = file;
        L.Enabled = true;
        SettingsStore.Save();
        Play(file);
    }

    public void Play(string file)
    {
        Stop(keepSetting: true, refreshDesktop: false);
        var target = Desktop.Find(L.AttachMode);
        Desktop.Log($"play {file} mode={L.AttachMode} -> parent={target.Parent:X} insertAfter={target.InsertAfter:X} layered={target.Layered}");
        if (target.Parent == IntPtr.Zero) { App.Log(new InvalidOperationException("Desktop window not found")); return; }
        foreach (var scr in System.Windows.Forms.Screen.AllScreens)
        {
            var w = new LiveWindow(file, scr.Bounds, target, L.Mute);
            w.Show();
            _windows.Add(w);
        }
        if (_tracker == null)
        {
            _tracker = new WindowTracker();
            _tracker.Changed += UpdatePause;
        }
        UpdatePause();
        StateChanged?.Invoke();
    }

    public void SetMute(bool mute)
    {
        L.Mute = mute;
        SettingsStore.Save();
        foreach (var w in _windows) w.Muted = mute;
    }

    void UpdatePause()
    {
        bool pause = _locked || (L.PauseWhenCovered && _tracker != null && WindowTracker.CoversDesktop(_tracker.Foreground));
        foreach (var w in _windows) w.Paused = pause;
    }

    public void Stop(bool keepSetting = false, bool refreshDesktop = true)
    {
        foreach (var w in _windows) w.Close();
        bool had = _windows.Count > 0;
        _windows.Clear();
        if (!keepSetting) { L.Enabled = false; SettingsStore.Save(); }
        if (had && refreshDesktop) Desktop.Repaint();
        if (!keepSetting) { _tracker?.Dispose(); _tracker = null; }
        StateChanged?.Invoke();
    }
}

/// <summary>One monitor's video.</summary>
sealed class LiveWindow : Window
{
    readonly MediaElement _media;
    readonly System.Drawing.Rectangle _bounds;
    readonly DesktopTarget _target;
    bool _paused;

    public LiveWindow(string file, System.Drawing.Rectangle bounds, DesktopTarget target, bool mute)
    {
        _bounds = bounds;
        _target = target;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = Brushes.Black;
        Title = "Wallflow live wallpaper";
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = bounds.Left; Top = bounds.Top; Width = bounds.Width; Height = bounds.Height;
        _media = new MediaElement
        {
            Source = new Uri(file), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close,
            Stretch = Stretch.UniformToFill, IsMuted = mute, Volume = 0.5, ScrubbingEnabled = false
        };
        _media.MediaEnded += (_, _) => { _media.Position = TimeSpan.Zero; if (!_paused) _media.Play(); };
        _media.MediaFailed += (_, e) => { Desktop.Log("media failed: " + e.ErrorException?.Message); App.Log(e.ErrorException); };
        _media.MediaOpened += (_, _) => Desktop.Log($"media opened {_media.NaturalVideoWidth}x{_media.NaturalVideoHeight}");
        Content = _media;
        SourceInitialized += (_, _) => Attach();
        Loaded += (_, _) => { if (!_paused) _media.Play(); };
        Closed += (_, _) => { _media.Stop(); _media.Close(); };
    }

    public bool Muted { set => _media.IsMuted = value; }

    public bool Paused
    {
        set
        {
            if (_paused == value) return;
            _paused = value;
            if (value) _media.Pause(); else _media.Play();
        }
    }

    void Attach()
    {
        var h = new WindowInteropHelper(this).Handle;
        // become a child of the desktop so we sit behind the icons
        long style = (long)GetWindowLongPtr(h, -16);
        style = (style & ~0x80000000L /*WS_POPUP*/) | 0x40000000L /*WS_CHILD*/;
        SetWindowLongPtr(h, -16, (IntPtr)style);
        if (_target.Layered)
        {
            // Windows 11 24H2+: children of Progman must be layered to be composed
            long ex = (long)GetWindowLongPtr(h, -20);
            SetWindowLongPtr(h, -20, (IntPtr)(ex | 0x80000 /*WS_EX_LAYERED*/));
            SetLayeredWindowAttributes(h, 0, 255, 2 /*LWA_ALPHA*/);
        }
        SetParent(h, _target.Parent);
        var pts = new[] { new POINT { X = _bounds.Left, Y = _bounds.Top } };
        MapWindowPoints(IntPtr.Zero, _target.Parent, pts, 1);
        uint flags = 0x0010 /*NOACTIVATE*/ | 0x0040 /*SHOWWINDOW*/;
        if (_target.InsertAfter == IntPtr.Zero) flags |= 0x0004 /*NOZORDER*/;
        SetWindowPos(h, _target.InsertAfter, pts[0].X, pts[0].Y, _bounds.Width, _bounds.Height, flags);
        // the WorkerW that paints the static wallpaper must stay under the video
        if (_target.WorkerBelow != IntPtr.Zero)
            SetWindowPos(_target.WorkerBelow, h, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /*NOSIZE|NOMOVE|NOACTIVATE*/);
        if (_target.Software && HwndSource.FromHwnd(h)?.CompositionTarget is HwndTarget ht)
            ht.RenderMode = RenderMode.SoftwareOnly;
        Dispatcher.BeginInvoke(() =>
        {
            GetWindowRect(h, out var r);
            Desktop.Log($"after show: visible={IsWindowVisible(h)} rect={r.Left},{r.Top},{r.Right},{r.Bottom} layered={_target.Layered} soft={_target.Software}");
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Desktop.Log($"attached {h:X} parent={GetParent(h):X} at {pts[0].X},{pts[0].Y} {_bounds.Width}x{_bounds.Height}");
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern int MapWindowPoints(IntPtr from, IntPtr to, [In, Out] POINT[] pts, int n);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}

public readonly record struct DesktopTarget(IntPtr Parent, IntPtr InsertAfter, bool Layered, IntPtr WorkerBelow = default, bool Software = false);

/// <summary>Finds where a video window has to go to sit between the wallpaper and the desktop icons.</summary>
static class Desktop
{
    public static DesktopTarget Find(string mode)
    {
        var progman = FindWindow("Progman", null);
        // ask Explorer to create the WorkerW behind the icons
        SendMessageTimeout(progman, 0x052C, (IntPtr)0xD, (IntPtr)0x1, 0, 1000, out _);
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        var defViewInProgman = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        var childWorker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        Log($"progman={progman:X} defView(in progman)={defViewInProgman:X} workerw(child)={childWorker:X}");

        // Windows 11 24H2+: icons and wallpaper are both children of Progman.
        // The video goes into Progman right below the icons (and above the WorkerW that paints the static wallpaper).
        bool newDesktop = childWorker != IntPtr.Zero && defViewInProgman != IntPtr.Zero;
        if (mode == "progman" || (mode == "auto" && newDesktop))
            return new DesktopTarget(progman, defViewInProgman, Layered: true, WorkerBelow: childWorker);
        if (mode == "progman-plain")
            return new DesktopTarget(progman, defViewInProgman, Layered: false, WorkerBelow: childWorker);
        if (mode == "progman-soft")
            return new DesktopTarget(progman, defViewInProgman, Layered: true, WorkerBelow: childWorker, Software: true);
        if (mode == "workerw" && childWorker != IntPtr.Zero)
            return new DesktopTarget(childWorker, IntPtr.Zero, Layered: false);

        // classic Windows 10/11: the top-level WorkerW right after the one that hosts the icons
        IntPtr worker = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return worker == IntPtr.Zero;
        }, IntPtr.Zero);
        Log($"classic workerw={worker:X}");
        return new DesktopTarget(worker != IntPtr.Zero ? worker : childWorker != IntPtr.Zero ? childWorker : progman, IntPtr.Zero, Layered: false);
    }

    public static void Log(string msg)
    {
        try { File.AppendAllText(Path.Combine(SettingsStore.Dir, "live.log"), $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }

    /// <summary>Redraws the static wallpaper after the video window is gone.</summary>
    public static void Repaint()
    {
        var sb = new System.Text.StringBuilder(1024);
        SystemParametersInfo(0x0073, sb.Capacity, sb, 0);
        if (sb.Length > 0) SystemParametersInfo2(20, 0, sb.ToString(), 2);
    }

    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(int a, int b, System.Text.StringBuilder c, int d);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")] static extern bool SystemParametersInfo2(int a, int b, string c, int d);
}

/// <summary>CI diagnostics: puts a test window into the desktop in different ways (Wallflow.exe --live-test variant file).</summary>
static class LiveTest
{
    static object? _keep;
    public static void Run(string variant, string file)
    {
        var progman = FindWindow("Progman", null);
        SendMessageTimeout(progman, 0x052C, (IntPtr)0xD, (IntPtr)0x1, 0, 1000, out _);
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        var worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        IntPtr parent = variant.Contains("workerw") ? worker : progman;
        IntPtr after = variant.Contains("top") ? IntPtr.Zero : defView;
        bool layered = variant.Contains("layered");

        IntPtr h;
        if (variant.StartsWith("hs"))
        {
            // created as a layered child of Progman from the start (what Microsoft recommends for 24H2)
            var p = new HwndSourceParameters("WallflowLiveTest")
            {
                ParentWindow = progman, PositionX = 100, PositionY = 100, Width = 400, Height = 300,
                WindowStyle = 0x40000000 /*CHILD*/ | 0x10000000 /*VISIBLE*/ | 0x04000000 /*CLIPSIBLINGS*/,
                ExtendedWindowStyle = 0x80000 /*LAYERED*/
            };
            var src = new HwndSource(p);
            h = src.Handle;
            SetLayeredWindowAttributes(h, 0, 255, 2);
            if (variant.Contains("soft")) src.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            var g = new Grid { Background = Brushes.Red, Width = 400, Height = 300 };
            if (variant.Contains("media"))
                g.Children.Add(new MediaElement { Source = new Uri(file), LoadedBehavior = MediaState.Play, IsMuted = true, Stretch = Stretch.UniformToFill });
            src.RootVisual = g;
            SetWindowPos(h, defView, 100, 100, 400, 300, 0x0010 | 0x0040);
            if (worker != IntPtr.Zero) SetWindowPos(worker, h, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            _keep = src;
            Desktop.Log($"test {variant}: h={h:X} parent={GetParent(h):X} ex={(long)GetWindowLongPtr(h, -20):X}");
            Dump(progman);
            return;
        }
        if (variant.StartsWith("red"))
        {
            var f = new System.Windows.Forms.Form
            {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None, BackColor = System.Drawing.Color.Red, ShowInTaskbar = false,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual, Bounds = new System.Drawing.Rectangle(100, 100, 400, 300)
            };
            f.Show();
            h = f.Handle;
        }
        else
        {
            var w = new Window
            {
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = Brushes.Lime,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = 100, Top = 100, Width = 400, Height = 300,
                Content = new MediaElement { Source = new Uri(file), LoadedBehavior = MediaState.Play, IsMuted = true, Stretch = Stretch.UniformToFill }
            };
            w.Show();
            h = new WindowInteropHelper(w).Handle;
        }
        if (variant.Contains("owned"))
        {
            // control: a normal top-level window owned by the desktop, like the widgets
            SetWindowLongPtr(h, -8, progman);
            SetWindowPos(h, (IntPtr)1, 100, 100, 400, 300, 0x0010 | 0x0040);
            Dump(progman);
            return;
        }
        long style = (long)GetWindowLongPtr(h, -16);
        SetWindowLongPtr(h, -16, (IntPtr)((style & ~0x80000000L) | 0x40000000L));
        if (layered)
        {
            long ex = (long)GetWindowLongPtr(h, -20);
            SetWindowLongPtr(h, -20, (IntPtr)(ex | 0x80000));
            SetLayeredWindowAttributes(h, 0, 255, 2);
        }
        SetParent(h, parent);
        SetWindowPos(h, after, 100, 100, 400, 300, 0x0010 | 0x0040);
        if (!variant.Contains("top") && parent == progman && worker != IntPtr.Zero)
            SetWindowPos(worker, h, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        Desktop.Log($"test {variant}: progman={progman:X} defView={defView:X} worker={worker:X} h={h:X} parent={GetParent(h):X}");
        Dump(progman);
    }

    static void Dump(IntPtr progman)
    {
        var sb = new System.Text.StringBuilder("top-level z-order (visible, top→bottom):\n");
        int n = 0;
        EnumWindows((h, _) =>
        {
            if (IsWindowVisible(h) && n++ < 400)
            {
                GetWindowRect(h, out var r);
                var c = new System.Text.StringBuilder(128); GetClassName(h, c, 128);
                long ex = (long)GetWindowLongPtr(h, -20);
                if (r.Right - r.Left >= 300 || c.ToString() is "Progman" or "WorkerW")
                    sb.Append($"  {h:X} {c} [{r.Left},{r.Top},{r.Right},{r.Bottom}] ex={ex:X}\n");
            }
            return true;
        }, IntPtr.Zero);
        sb.Append("children of Progman (top→bottom):\n");
        for (var ch = GetWindow(GetWindow(progman, 5 /*GW_CHILD*/), 0 /*GW_HWNDFIRST*/); ch != IntPtr.Zero; ch = GetWindow(ch, 2 /*GW_HWNDNEXT*/))
        {
            GetWindowRect(ch, out var r);
            var c = new System.Text.StringBuilder(128); GetClassName(ch, c, 128);
            sb.Append($"  {ch:X} {c} vis={IsWindowVisible(ch)} [{r.Left},{r.Top},{r.Right},{r.Bottom}] style={(long)GetWindowLongPtr(ch, -16):X} ex={(long)GetWindowLongPtr(ch, -20):X}\n");
        }
        long pex = (long)GetWindowLongPtr(progman, -20);
        sb.Append($"progman ex={pex:X} style={(long)GetWindowLongPtr(progman, -16):X}");
        Desktop.Log(sb.ToString());
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int max);

    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
