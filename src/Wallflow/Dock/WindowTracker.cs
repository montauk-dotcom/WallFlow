using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace Wallflow.Dock;

public sealed record AppWindow(IntPtr Hwnd, string Title, string ExePath, int Pid);

/// <summary>
/// Lists the same windows the Windows taskbar shows, and raises <see cref="Changed"/> when they change.
/// Driven by WinEvent hooks (no polling), debounced.
/// </summary>
public sealed class WindowTracker : IDisposable
{
    public event Action? Changed;
    public IntPtr Foreground { get; private set; }

    readonly WinEventProc _proc;   // keep the delegate alive
    readonly List<IntPtr> _hooks = new();
    readonly DispatcherTimer _debounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
    readonly int _ownPid = Environment.ProcessId;

    public WindowTracker()
    {
        _proc = OnEvent;
        _debounce.Tick += (_, _) => { _debounce.Stop(); Foreground = GetForegroundWindow(); Changed?.Invoke(); };
        const uint flags = 0 /*OUTOFCONTEXT*/ | 2 /*SKIPOWNPROCESS*/;
        _hooks.Add(SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, _proc, 0, 0, flags));   // foreground
        _hooks.Add(SetWinEventHook(0x0016, 0x0017, IntPtr.Zero, _proc, 0, 0, flags));   // minimize start/end
        _hooks.Add(SetWinEventHook(0x8001, 0x8003, IntPtr.Zero, _proc, 0, 0, flags));   // destroy/show/hide
        _hooks.Add(SetWinEventHook(0x800C, 0x800C, IntPtr.Zero, _proc, 0, 0, flags));   // name change
        Foreground = GetForegroundWindow();
    }

    void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero) return;   // windows only, not controls
        if (ev != 0x0003 && GetAncestor(hwnd, 2 /*GA_ROOT*/) != hwnd) return;
        _debounce.Stop(); _debounce.Start();
    }

    public List<AppWindow> List()
    {
        var res = new List<AppWindow>();
        EnumWindows((h, _) =>
        {
            if (IsTaskbarWindow(h))
            {
                var exe = ExeFor(h, out int pid);
                if (pid != _ownPid)
                    res.Add(new AppWindow(h, Title(h), exe, pid));
            }
            return true;
        }, IntPtr.Zero);
        return res;
    }

    static bool IsTaskbarWindow(IntPtr h)
    {
        if (!IsWindowVisible(h)) return false;
        long ex = (long)GetWindowLongPtr(h, -20);
        bool appWindow = (ex & 0x40000) != 0;
        if ((ex & 0x80) != 0 && !appWindow) return false;               // tool windows
        if (GetWindow(h, 4 /*GW_OWNER*/) != IntPtr.Zero && !appWindow) return false;
        if ((ex & 0x08000000) != 0 && !appWindow) return false;         // no-activate
        if (DwmGetWindowAttribute(h, 14 /*DWMWA_CLOAKED*/, out int cloaked, 4) == 0 && cloaked != 0) return false;
        if (GetWindowTextLength(h) == 0) return false;
        var cls = ClassName(h);
        return cls is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow");
    }

    public static string Title(IntPtr h)
    {
        var sb = new StringBuilder(GetWindowTextLength(h) + 1);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    static string ClassName(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>Path of the program that owns the window (looks inside ApplicationFrameHost for Store apps).</summary>
    public static string ExeFor(IntPtr h, out int pid)
    {
        GetWindowThreadProcessId(h, out pid);
        var path = ProcessPath(pid);
        if (path.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            int framePid = pid, inner = 0;
            EnumChildWindows(h, (c, _) =>
            {
                GetWindowThreadProcessId(c, out int p);
                if (p != framePid) { inner = p; return false; }
                return true;
            }, IntPtr.Zero);
            if (inner != 0) { pid = inner; path = ProcessPath(inner); }
        }
        return path;
    }

    static string ProcessPath(int pid)
    {
        var hp = OpenProcess(0x1000 /*QUERY_LIMITED_INFORMATION*/, false, pid);
        if (hp == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(hp, 0, sb, ref size) ? sb.ToString() : "";
        }
        finally { CloseHandle(hp); }
    }

    // ---------------- window actions ----------------
    public static void ActivateOrMinimize(IntPtr h, IntPtr foreground)
    {
        if (h == foreground && !IsIconic(h)) { ShowWindow(h, 6 /*SW_MINIMIZE*/); return; }
        Activate(h);
    }

    public static void Activate(IntPtr h)
    {
        if (IsIconic(h)) ShowWindow(h, 9 /*SW_RESTORE*/);
        // tap Alt so Windows lets us change the foreground window
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        SetForegroundWindow(h);
    }

    public static void Close(IntPtr h) => PostMessage(h, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);

    /// <summary>True if the window covers its whole monitor (fullscreen game/video).</summary>
    public static bool IsFullscreen(IntPtr h)
    {
        if (h == IntPtr.Zero || !IsWindowVisible(h)) return false;
        var cls = ClassName(h);
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!GetWindowRect(h, out var r)) return false;
        var mon = MonitorFromWindow(h, 2);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi)) return false;
        return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
    }

    /// <summary>Fullscreen or maximized on that monitor — the desktop can't be seen.</summary>
    public static bool CoversDesktop(IntPtr h) => IsFullscreen(h) || (h != IntPtr.Zero && IsZoomed(h) && IsWindowVisible(h));

    public static void SendKeys(params byte[] vk)
    {
        foreach (var k in vk) keybd_event(k, 0, 0, UIntPtr.Zero);
        for (int i = vk.Length - 1; i >= 0; i--) keybd_event(vk[i], 0, 2, UIntPtr.Zero);
    }

    public void Dispose()
    {
        foreach (var h in _hooks) if (h != IntPtr.Zero) UnhookWinEvent(h);
        _hooks.Clear();
        _debounce.Stop();
    }

    // ---------------- interop ----------------
    delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
}
