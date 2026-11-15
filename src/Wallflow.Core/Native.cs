using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Wallflow.Core;

/// <summary>All Win32 interop in one place.</summary>
public static class Native
{
    // ---------------- wallpaper ----------------
    const int SPI_SETDESKWALLPAPER = 20, SPIF_UPDATEINIFILE = 1, SPIF_SENDCHANGE = 2;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SystemParametersInfo(int action, int param, string vparam, int winIni);

    static readonly Dictionary<string, (string style, string tile)> Styles = new()
    {
        ["fill"] = ("10", "0"), ["fit"] = ("6", "0"), ["stretch"] = ("2", "0"),
        ["center"] = ("0", "0"), ["tile"] = ("0", "1"), ["span"] = ("22", "0")
    };

    public static void SetWallpaper(string file, string style)
    {
        var (ws, tw) = Styles.TryGetValue(style, out var s) ? s : Styles["fill"];
        using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
        {
            key?.SetValue("WallpaperStyle", ws);
            key?.SetValue("TileWallpaper", tw);
        }
        if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, file, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
            throw new InvalidOperationException($"Windows не принял обои (код {Marshal.GetLastWin32Error()})");
    }

    // ---------------- autostart ----------------
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetAutoStart(bool on)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) key.SetValue("Wallflow", $"\"{Environment.ProcessPath}\" --tray");
        else key.DeleteValue("Wallflow", false);
    }

    public static bool IsAutoStart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("Wallflow") is string v && v.Contains(Environment.ProcessPath ?? "\u0000", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- desktop icons ----------------
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);

    public static IntPtr Progman => FindWindow("Progman", null);

    static IntPtr FindDefView()
    {
        var dv = FindWindowEx(Progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (dv != IntPtr.Zero) return dv;
        EnumWindows((h, _) =>
        {
            var d = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (d != IntPtr.Zero) { dv = d; return false; }
            return true;
        }, IntPtr.Zero);
        return dv;
    }

    public static bool DesktopIconsVisible()
    {
        var lv = FindWindowEx(FindDefView(), IntPtr.Zero, "SysListView32", "FolderView");
        return lv == IntPtr.Zero || IsWindowVisible(lv);
    }

    /// <summary>Same as right-click desktop → View → Show desktop icons.</summary>
    public static void SetDesktopIconsVisible(bool visible)
    {
        if (DesktopIconsVisible() == visible) return;
        var dv = FindDefView();
        if (dv != IntPtr.Zero) SendMessage(dv, 0x0111 /*WM_COMMAND*/, (IntPtr)0x7402, IntPtr.Zero);
    }

    // ---------------- widget windows ----------------
    const int GWL_EXSTYLE = -20, GWLP_HWNDPARENT = -8;
    const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_APPWINDOW = 0x40000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    /// <summary>
    /// Makes a window behave like a desktop widget: no taskbar/Alt-Tab entry, owned by the
    /// desktop (so it survives Win+D / "Show desktop"), kept at the bottom of the Z-order.
    /// </summary>
    public static void PinToDesktop(IntPtr hwnd, bool noActivate, IntPtr owner = default)
    {
        var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        if (noActivate) ex |= WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)ex);
        var pm = owner != IntPtr.Zero ? owner : Progman;
        if (pm != IntPtr.Zero) SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, pm);
        SendToBottom(hwnd);
    }

    public static void SendToBottom(IntPtr hwnd) =>
        SetWindowPos(hwnd, (IntPtr)1 /*HWND_BOTTOM*/, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /*NOSIZE|NOMOVE|NOACTIVATE*/);

    // ---- glass (DWM blur behind) ----
    [StructLayout(LayoutKind.Sequential)]
    struct AccentPolicy { public int State, Flags, GradientColor, AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    struct WinCompAttrData { public int Attribute; public IntPtr Data; public int Size; }
    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h, ref WinCompAttrData d);

    /// <summary>state: 0 = off, 3 = blur behind, 4 = acrylic</summary>
    public static void SetBlur(IntPtr hwnd, bool on, uint abgr = 0x01000000)
    {
        var accent = new AccentPolicy { State = on ? 3 : 0, Flags = 2, GradientColor = unchecked((int)abgr) };
        var size = Marshal.SizeOf(accent);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WinCompAttrData { Attribute = 19 /*WCA_ACCENT_POLICY*/, Data = ptr, Size = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);

    /// <summary>Clips the window (and therefore the blur) to a rounded rectangle. Pixel units.</summary>
    public static void SetRoundRegion(IntPtr hwnd, int width, int height, int radius)
    {
        var rgn = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
        SetWindowRgn(hwnd, rgn, true); // system owns rgn afterwards
    }

    // ---------------- Windows taskbar ----------------
    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);

    /// <summary>Auto-hide of the Windows taskbar (the same switch as in taskbar settings).</summary>
    public static bool TaskbarAutoHide
    {
        get
        {
            var d = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = FindWindow("Shell_TrayWnd", null) };
            return ((ulong)SHAppBarMessage(4 /*ABM_GETSTATE*/, ref d) & 1) != 0;
        }
        set
        {
            var d = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = FindWindow("Shell_TrayWnd", null), lParam = (IntPtr)(value ? 1 : 2 /*ALWAYSONTOP*/) };
            SHAppBarMessage(10 /*ABM_SETSTATE*/, ref d);
        }
    }

    // ---------------- memory ----------------
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    /// <summary>Full GC + hand unused pages back to Windows. Call after closing heavy windows.</summary>
    public static void TrimMemory()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        try { SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1); } catch { }
    }

    public static void OpenUrl(string url)
    {
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static void Launch(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) ?? "" });
    }
}
