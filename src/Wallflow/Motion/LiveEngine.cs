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
        var parent = Desktop.WorkerW();
        if (parent == IntPtr.Zero) { App.Log(new InvalidOperationException("WorkerW not found")); return; }
        foreach (var scr in System.Windows.Forms.Screen.AllScreens)
        {
            var w = new LiveWindow(file, scr.Bounds, parent, L.Mute);
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
    readonly IntPtr _parent;
    bool _paused;

    public LiveWindow(string file, System.Drawing.Rectangle bounds, IntPtr parent, bool mute)
    {
        _bounds = bounds;
        _parent = parent;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = Brushes.Black;
        Title = "Wallflow live wallpaper";
        _media = new MediaElement
        {
            Source = new Uri(file), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close,
            Stretch = Stretch.UniformToFill, IsMuted = mute, Volume = 0.5, ScrubbingEnabled = false
        };
        _media.MediaEnded += (_, _) => { _media.Position = TimeSpan.Zero; if (!_paused) _media.Play(); };
        _media.MediaFailed += (_, e) => App.Log(e.ErrorException);
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
        // become a child of the desktop's WorkerW so we sit behind the icons
        long style = (long)GetWindowLongPtr(h, -16);
        style = (style & ~0x80000000L /*WS_POPUP*/) | 0x40000000L /*WS_CHILD*/;
        SetWindowLongPtr(h, -16, (IntPtr)style);
        SetParent(h, _parent);
        var pts = new[] { new POINT { X = _bounds.Left, Y = _bounds.Top } };
        MapWindowPoints(IntPtr.Zero, _parent, pts, 1);
        SetWindowPos(h, IntPtr.Zero, pts[0].X, pts[0].Y, _bounds.Width, _bounds.Height, 0x0004 /*NOZORDER*/ | 0x0010 /*NOACTIVATE*/ | 0x0040 /*SHOWWINDOW*/);
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern int MapWindowPoints(IntPtr from, IntPtr to, [In, Out] POINT[] pts, int n);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}

/// <summary>Finds the window that lives between the wallpaper and the desktop icons.</summary>
static class Desktop
{
    public static IntPtr WorkerW()
    {
        var progman = FindWindow("Progman", null);
        // ask Explorer to create the WorkerW behind the icons
        SendMessageTimeout(progman, 0x052C, (IntPtr)0xD, (IntPtr)0x1, 0, 1000, out _);
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        // Windows 11 24H2+: the WorkerW is a child of Progman
        var child = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (child != IntPtr.Zero) return child;

        // older Windows: the top-level WorkerW right after the one that hosts the icons
        IntPtr worker = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return worker == IntPtr.Zero;
        }, IntPtr.Zero);
        return worker != IntPtr.Zero ? worker : progman;
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
