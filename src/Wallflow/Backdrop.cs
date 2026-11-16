using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Wallflow.Core;

namespace Wallflow;

/// <summary>
/// "Frosted glass" without DWM: renders the current wallpaper exactly as Windows lays it out on each
/// monitor, blurs it once at low resolution and hands out brushes that show the part behind a widget.
/// Costs ~1 MB per monitor and a few ms only when the wallpaper changes.
/// </summary>
public static class Backdrop
{
    sealed record Monitor(Int32Rect Bounds, BitmapSource Blurred, double K);

    static readonly List<Monitor> Monitors = new();
    static readonly DispatcherTimer Debounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(600) };
    static bool _started;

    /// <summary>Raised after the blurred wallpaper was rebuilt (wallpaper/monitors/blur strength changed).</summary>
    public static event Action? Changed;

    /// <summary>
    /// The blur runs on a reduced copy (cheap). Strong blur → smaller copy; weak blur → bigger copy,
    /// so "almost no blur" really looks sharp instead of soft from up-scaling.
    /// </summary>
    static int DownscaleFor(double strength) => strength < 12 ? 1 : strength < 35 ? 2 : 4;

    public static void Start()
    {
        if (_started) return;
        _started = true;
        Debounce.Tick += (_, _) => { Debounce.Stop(); Debounce.Interval = TimeSpan.FromMilliseconds(600); Rebuild(); };
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
                Application.Current.Dispatcher.BeginInvoke(Invalidate);
        };
        SystemEvents.DisplaySettingsChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(Invalidate);
        Rebuild();
    }

    public static void Invalidate() { Debounce.Stop(); Debounce.Start(); }

    /// <summary>Quicker re-blur while a slider is being dragged.</summary>
    public static void InvalidateSoon() { Debounce.Stop(); Debounce.Interval = TimeSpan.FromMilliseconds(180); Debounce.Start(); }

    // ------------------------------------------------------------------
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool SystemParametersInfo(int action, int param, StringBuilder buf, int winIni);

    static string? WallpaperPath()
    {
        var sb = new StringBuilder(1024);
        SystemParametersInfo(0x0073 /*SPI_GETDESKWALLPAPER*/, sb.Capacity, sb, 0);
        var p = sb.ToString();
        if (File.Exists(p)) return p;
        var t = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
        return File.Exists(t) ? t : null;
    }

    static (string style, bool tile) WallpaperStyle()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var ws = k?.GetValue("WallpaperStyle") as string ?? "10";
        var tw = k?.GetValue("TileWallpaper") as string ?? "0";
        if (tw == "1") return ("tile", true);
        return (ws switch { "10" => "fill", "6" => "fit", "2" => "stretch", "22" => "span", "0" => "center", _ => "fill" }, false);
    }

    static Color BackgroundColor()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors");
            var parts = (k?.GetValue("Background") as string ?? "0 0 0").Split(' ');
            return Color.FromRgb(byte.Parse(parts[0]), byte.Parse(parts[1]), byte.Parse(parts[2]));
        }
        catch { return Colors.Black; }
    }

    static void Rebuild()
    {
        Monitors.Clear();
        try
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            var virt = System.Windows.Forms.SystemInformation.VirtualScreen;
            var path = WallpaperPath();
            var (style, _) = WallpaperStyle();
            var bg = BackgroundColor();

            BitmapSource? img = null;
            int nativeW = 0, nativeH = 0;
            if (path != null)
            {
                // read native size from metadata, then decode small
                using (var fs = File.OpenRead(path))
                {
                    var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    nativeW = frame.PixelWidth; nativeH = frame.PixelHeight;
                }
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
                bi.DecodePixelWidth = Math.Min(nativeW, Math.Max(640, virt.Width / DownscaleFor(SettingsStore.Current.Widgets.BlurStrength) * 2));
                bi.EndInit();
                bi.Freeze();
                img = bi;
            }

            double strength = Math.Clamp(SettingsStore.Current.Widgets.BlurStrength, 0, 100);
            int Downscale = DownscaleFor(strength);
            foreach (var s in screens)
            {
                var b = s.Bounds;
                int w = Math.Max(1, b.Width / Downscale), h = Math.Max(1, b.Height / Downscale);
                double k = 1.0 / Downscale;
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, w, h));
                    if (img != null)
                    {
                        Rect r;
                        switch (style)
                        {
                            case "fit":
                            {
                                double sc = Math.Min((double)b.Width / nativeW, (double)b.Height / nativeH);
                                double iw = nativeW * sc * k, ih = nativeH * sc * k;
                                r = new Rect((w - iw) / 2, (h - ih) / 2, iw, ih); break;
                            }
                            case "stretch": r = new Rect(0, 0, w, h); break;
                            case "center":
                            {
                                double iw = nativeW * k, ih = nativeH * k;
                                r = new Rect((w - iw) / 2, (h - ih) / 2, iw, ih); break;
                            }
                            case "span":
                            {
                                double sc = Math.Max((double)virt.Width / nativeW, (double)virt.Height / nativeH);
                                double iw = nativeW * sc, ih = nativeH * sc;
                                double x = virt.X + (virt.Width - iw) / 2 - b.X, y = virt.Y + (virt.Height - ih) / 2 - b.Y;
                                r = new Rect(x * k, y * k, iw * k, ih * k); break;
                            }
                            case "tile":
                            {
                                var tb = new ImageBrush(img)
                                {
                                    TileMode = TileMode.Tile, Stretch = Stretch.Fill, ViewportUnits = BrushMappingMode.Absolute,
                                    Viewport = new Rect((virt.X - b.X) * k, (virt.Y - b.Y) * k, nativeW * k, nativeH * k)
                                };
                                dc.DrawRectangle(tb, null, new Rect(0, 0, w, h));
                                r = Rect.Empty; break;
                            }
                            default: // fill
                            {
                                double sc = Math.Max((double)b.Width / nativeW, (double)b.Height / nativeH);
                                double iw = nativeW * sc * k, ih = nativeH * sc * k;
                                r = new Rect((w - iw) / 2, (h - ih) / 2, iw, ih); break;
                            }
                        }
                        if (!r.IsEmpty) dc.DrawImage(img, r);
                    }
                }
                var flat = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                flat.Render(dv);
                flat.Freeze();

                Monitors.Add(new Monitor(new Int32Rect(b.X, b.Y, b.Width, b.Height), Blur(flat, strength * 1.2 / Downscale), k)); // up to ~120 px of real blur
            }
        }
        catch (Exception ex) { App.Log(ex); }
        Changed?.Invoke();
    }

    /// <summary>Gaussian blur; the image is drawn a bit larger so edges don't fade to transparent.</summary>
    static BitmapSource Blur(BitmapSource src, double radius)
    {
        if (radius < 0.5) return src;
        int w = src.PixelWidth, h = src.PixelHeight;
        double pad = radius * 2;
        var image = new Image { Source = src, Stretch = Stretch.Fill, Width = w + pad * 2, Height = h + pad * 2 };
        image.Effect = new BlurEffect { Radius = radius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        Canvas.SetLeft(image, -pad); Canvas.SetTop(image, -pad);
        canvas.Children.Add(image);
        canvas.Measure(new Size(w, h));
        canvas.Arrange(new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(canvas);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>Brush showing the blurred wallpaper behind <paramref name="el"/> (physical screen coords).</summary>
    public static Brush? BrushFor(FrameworkElement el)
    {
        if (Monitors.Count == 0 || el.ActualWidth < 1 || PresentationSource.FromVisual(el) is null) return null;
        Point a, b;
        try
        {
            a = el.PointToScreen(new Point(0, 0));
            b = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
        }
        catch { return null; }
        double cx = (a.X + b.X) / 2, cy = (a.Y + b.Y) / 2;
        Monitor? m = null;
        foreach (var mon in Monitors)
            if (cx >= mon.Bounds.X && cx < mon.Bounds.X + mon.Bounds.Width && cy >= mon.Bounds.Y && cy < mon.Bounds.Y + mon.Bounds.Height) { m = mon; break; }
        m ??= Monitors[0];
        var vb = new Rect((a.X - m.Bounds.X) * m.K, (a.Y - m.Bounds.Y) * m.K, (b.X - a.X) * m.K, (b.Y - a.Y) * m.K);
        var brush = new ImageBrush(m.Blurred)
        {
            ViewboxUnits = BrushMappingMode.Absolute, Viewbox = vb, Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
        brush.Freeze();
        return brush;
    }
}
