using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Wallflow;

/// <summary>Small UI helpers shared by widgets and windows.</summary>
public static class UiKit
{
    static readonly Dictionary<string, FontFamily> FontCache = new();

    /// <summary>Bundled font (Fonts/*.ttf) with a system fallback for missing glyphs (e.g. Cyrillic).</summary>
    public static FontFamily Font(string family, string fallback = "Segoe UI")
    {
        var key = family + "|" + fallback;
        if (FontCache.TryGetValue(key, out var f)) return f;
        f = new FontFamily(new Uri("pack://application:,,,/"), $"./Fonts/#{family}, {fallback}");
        FontCache[key] = f;
        return f;
    }

    public static FontFamily SysFont(string families) => new(families);

    public static SolidColorBrush Brush(byte a, byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }

    public static SolidColorBrush Hex(string hex, byte alpha = 255)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex.StartsWith('#') ? hex : "#" + hex);
        c.A = alpha;
        var br = new SolidColorBrush(c);
        br.Freeze();
        return br;
    }

    public static readonly SolidColorBrush White = Brush(255, 255, 255, 255);
    public static readonly SolidColorBrush WhiteSoft = Brush(200, 255, 255, 255);
    public static readonly SolidColorBrush WhiteDim = Brush(140, 255, 255, 255);

    public static TextBlock Text(string s, FontFamily ff, double size, Brush? fg = null,
        FontWeight? weight = null, HorizontalAlignment align = HorizontalAlignment.Center)
        => new()
        {
            Text = s, FontFamily = ff, FontSize = size, Foreground = fg ?? White,
            FontWeight = weight ?? FontWeights.Normal, HorizontalAlignment = align,
            TextAlignment = TextAlignment.Center
        };

    /// <summary>Text with letter-spacing (WPF has no tracking property, so one TextBlock per character).</summary>
    public static FrameworkElement Spaced(string s, FontFamily ff, double size, double spacing, Brush? fg = null,
        FontWeight? weight = null, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        if (spacing <= 0) return Text(s, ff, size, fg, weight, align);
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = align };
        for (int i = 0; i < s.Length; i++)
        {
            var t = Text(s[i].ToString(), ff, size, fg, weight);
            if (i < s.Length - 1) t.Margin = new Thickness(0, 0, spacing, 0);
            sp.Children.Add(t);
        }
        return sp;
    }

    /// <summary>Hollow (stroke-only) text.</summary>
    public static FrameworkElement Outline(string s, FontFamily ff, double size, Brush stroke, double thickness = 2,
        FontWeight? weight = null)
    {
        var ft = new FormattedText(s, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(ff, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, Brushes.White, 1.0);
        var geo = ft.BuildGeometry(new Point(0, 0));
        geo.Freeze();
        return new System.Windows.Shapes.Path
        {
            Data = geo, Stroke = stroke, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round,
            Width = ft.WidthIncludingTrailingWhitespace, Height = ft.Height, HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    public static DropShadowEffect Glow(Color c, double radius = 24, double opacity = 0.9) =>
        new() { Color = c, BlurRadius = radius, ShadowDepth = 0, Opacity = opacity, RenderingBias = RenderingBias.Performance };

    public static DropShadowEffect SoftShadow() =>
        new() { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.45, RenderingBias = RenderingBias.Performance };

    // ---------------- dark title bar for normal windows ----------------
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

    public static void DarkTitleBar(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(w).Handle;
            int on = 1;
            DwmSetWindowAttribute(h, 20, ref on, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
            int round = 2;
            DwmSetWindowAttribute(h, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE = round (Win11)
        };
    }

    // ---------------- thumbnails from the web ----------------
    public static BitmapImage WebImage(string url, int decodeWidth)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource = new Uri(url);
        bi.DecodePixelWidth = decodeWidth;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bi.EndInit();
        return bi;
    }
}

/// <summary>High quality icons for files/shortcuts (the same ones Explorer shows).</summary>
public static class ShellIcon
{
    static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static void ClearCache() => Cache.Clear();

    public static ImageSource? Get(string path, int size = 96)
    {
        var key = path + "|" + size;
        if (Cache.TryGetValue(key, out var img)) return img;
        img = FromFactory(path, size) ?? FromFileInfo(path);
        img?.Freeze();
        Cache[key] = img;
        return img;
    }

    // --- IShellItemImageFactory: crisp, any size, keeps alpha ---
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, ref BITMAP bm);
    [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight; public ushort biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    static BitmapSource? FromFactory(string path, int size)
    {
        IntPtr hbm = IntPtr.Zero;
        try
        {
            SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var f);
            // SIIGBF_BIGGERSIZEOK (0x1) | SIIGBF_ICONONLY (0x4)
            if (f.GetImage(new SIZE { cx = size, cy = size }, 0x1 | 0x4, out hbm) != 0 || hbm == IntPtr.Zero) return null;
            var bm = new BITMAP();
            GetObject(hbm, Marshal.SizeOf<BITMAP>(), ref bm);
            int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
            var bi = new BITMAPINFOHEADER { biSize = Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            var px = new byte[w * h * 4];
            var dc = GetDC(IntPtr.Zero);
            try { GetDIBits(dc, hbm, 0, (uint)h, px, ref bi, 0); }
            finally { ReleaseDC(IntPtr.Zero, dc); }
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, px, w * 4);
        }
        catch { return null; }
        finally { if (hbm != IntPtr.Zero) DeleteObject(hbm); }
    }

    // --- fallback: classic 32px icon ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    static BitmapSource? FromFileInfo(string path)
    {
        var info = new SHFILEINFO();
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 /*ICON*/ | 0x0 /*LARGE*/) == IntPtr.Zero
            || info.hIcon == IntPtr.Zero) return null;
        try { return Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); }
        finally { DestroyIcon(info.hIcon); }
    }
}
