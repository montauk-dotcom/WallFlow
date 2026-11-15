using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

public sealed class WeatherWidget : GlassWindow
{
    readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(30) };
    readonly ContentControl _host = new();
    WeatherNow? _last;

    public WeatherWidget() : base(SettingsStore.Current.Widgets.WeatherPos)
    {
        Glass.Padding = new Thickness(22, 18, 22, 18);
        Glass.Child = _host;
        _host.Content = UiKit.Text("…", UiKit.SysFont("Segoe UI Variable Display, Segoe UI"), 20, UiKit.WhiteSoft);
        _timer.Tick += async (_, _) => await Load();
        _timer.Start();
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPower;
        _ = Load();
    }

    void OnPower(object? s, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume) Dispatcher.BeginInvoke(async () => { await Task.Delay(5000); await Load(); });
    }

    protected override Point DefaultPosition(Rect wa, Size s) => new(wa.Right - s.Width - 70, wa.Top + 60);

    public override void Refresh() => _ = Load();

    async Task Load()
    {
        try
        {
            _last = await WeatherApi.Get(SettingsStore.Current.Widgets);
            Render(_last);
        }
        catch (Exception ex)
        {
            if (_last is null)
                _host.Content = UiKit.Text("Погода недоступна\n" + ex.Message, UiKit.SysFont("Segoe UI"), 13, UiKit.WhiteSoft);
        }
    }

    void Render(WeatherNow w)
    {
        var lang = SettingsStore.Current.Widgets.Language;
        var f = UiKit.SysFont("Segoe UI Variable Display, Segoe UI");
        var root = new StackPanel { Width = 300 };

        // header: city + temp + icon
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel();
        left.Children.Add(UiKit.Text(w.City, f, 18, UiKit.White, FontWeights.SemiBold, HorizontalAlignment.Left));
        var temp = UiKit.Text($"{Math.Round(w.Temp)}°", f, 64, UiKit.White, FontWeights.Light, HorizontalAlignment.Left);
        temp.Margin = new Thickness(-2, -8, 0, -8);
        left.Children.Add(temp);
        top.Children.Add(left);
        var icon = WeatherIcons.Create(w.Sky, w.IsDay, 72);
        icon.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(icon, 1);
        top.Children.Add(icon);
        root.Children.Add(top);

        root.Children.Add(UiKit.Text(w.Description, f, 15, UiKit.White, FontWeights.SemiBold, HorizontalAlignment.Left));
        var feels = lang == "ru"
            ? $"Ощущается {Math.Round(w.Feels)}° · Влажн. {w.Humidity}% · {w.Wind.ToString("0.#", CultureInfo.InvariantCulture)} м/с"
            : $"Feels {Math.Round(w.Feels)}° · Hum {w.Humidity}% · {w.Wind.ToString("0.#", CultureInfo.InvariantCulture)} m/s";
        root.Children.Add(UiKit.Text(feels, f, 12.5, UiKit.WhiteDim, null, HorizontalAlignment.Left));

        root.Children.Add(new Rectangle { Height = 1, Fill = UiKit.Brush(40, 255, 255, 255), Margin = new Thickness(0, 12, 0, 10) });

        var days = new UniformGrid5();
        foreach (var d in w.Days)
        {
            var col = new StackPanel();
            col.Children.Add(UiKit.Text(d.Date.Date == DateTime.Today ? (lang == "ru" ? "Сег." : "Today") : Lang.ShortDay(d.Date, lang), f, 12.5, UiKit.WhiteSoft));
            var ic = WeatherIcons.Create(d.Sky, true, 28);
            ic.Margin = new Thickness(0, 4, 0, 4);
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            col.Children.Add(ic);
            col.Children.Add(UiKit.Text($"{Math.Round(d.Max)}°", f, 13, UiKit.White, FontWeights.SemiBold));
            col.Children.Add(UiKit.Text($"{Math.Round(d.Min)}°", f, 12, UiKit.WhiteDim));
            days.Children.Add(col);
        }
        root.Children.Add(days);
        _host.Content = root;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPower;
        base.OnClosed(e);
    }

    sealed class UniformGrid5 : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGrid5() { Rows = 1; }
    }
}

/// <summary>Tiny vector weather icons — no image files, no emoji fonts.</summary>
public static class WeatherIcons
{
    static readonly Brush Sun = UiKit.Hex("#FFC857");
    static readonly Brush Cloud = UiKit.Hex("#F2F4FA");
    static readonly Brush CloudDark = UiKit.Hex("#B9BFD3");
    static readonly Brush Drop = UiKit.Hex("#6CB8FF");
    static readonly Brush Bolt = UiKit.Hex("#FFD84D");

    public static FrameworkElement Create(Sky sky, bool day, double size)
    {
        var c = new Canvas { Width = 64, Height = 64 };
        switch (sky)
        {
            case Sky.Clear: if (day) DrawSun(c, 32, 32, 14); else DrawMoon(c, 32, 30, 16); break;
            case Sky.PartlyCloudy:
                if (day) DrawSun(c, 40, 22, 11); else DrawMoon(c, 42, 20, 11);
                DrawCloud(c, 6, 26, 46, Cloud); break;
            case Sky.Cloudy: DrawCloud(c, 14, 14, 40, CloudDark); DrawCloud(c, 4, 24, 48, Cloud); break;
            case Sky.Fog:
                DrawCloud(c, 8, 12, 46, CloudDark);
                for (int i = 0; i < 3; i++) Line(c, 10 + i * 3, 46 + i * 6, 54 - i * 3, 46 + i * 6, Cloud, 3);
                break;
            case Sky.Drizzle: DrawCloud(c, 6, 10, 50, Cloud); for (int i = 0; i < 3; i++) Line(c, 20 + i * 12, 46, 18 + i * 12, 52, Drop, 3); break;
            case Sky.Rain: DrawCloud(c, 6, 8, 50, Cloud); for (int i = 0; i < 4; i++) Line(c, 16 + i * 10, 44, 11 + i * 10, 58, Drop, 3); break;
            case Sky.Snow:
                DrawCloud(c, 6, 8, 50, Cloud);
                for (int i = 0; i < 4; i++) Dot(c, 16 + i * 10, 50 + (i % 2) * 7, 3.2, Cloud);
                break;
            case Sky.Thunder:
                DrawCloud(c, 6, 6, 50, CloudDark);
                var bolt = new Polygon
                {
                    Fill = Bolt,
                    Points = new PointCollection { new(34, 36), new(24, 50), new(32, 50), new(27, 62), new(42, 45), new(34, 45), new(39, 36) }
                };
                c.Children.Add(bolt);
                break;
        }
        return new Viewbox { Width = size, Height = size, Child = c };
    }

    static void DrawSun(Canvas c, double cx, double cy, double r)
    {
        for (int i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            Line(c, cx + Math.Cos(a) * (r + 4), cy + Math.Sin(a) * (r + 4), cx + Math.Cos(a) * (r + 10), cy + Math.Sin(a) * (r + 10), Sun, 3.2);
        }
        Dot(c, cx, cy, r, Sun);
    }

    static void DrawMoon(Canvas c, double cx, double cy, double r)
    {
        var g = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(new Point(cx, cy), r, r),
            new EllipseGeometry(new Point(cx + r * 0.55, cy - r * 0.35), r * 0.85, r * 0.85));
        c.Children.Add(new Path { Data = g, Fill = UiKit.Hex("#E8ECFF") });
    }

    static void DrawCloud(Canvas c, double x, double y, double w, Brush fill)
    {
        double h = w * 0.55;
        var g = new GeometryGroup { FillRule = FillRule.Nonzero };
        g.Children.Add(new EllipseGeometry(new Point(x + w * 0.30, y + h * 0.62), w * 0.22, h * 0.38));
        g.Children.Add(new EllipseGeometry(new Point(x + w * 0.55, y + h * 0.45), w * 0.27, h * 0.48));
        g.Children.Add(new EllipseGeometry(new Point(x + w * 0.78, y + h * 0.66), w * 0.19, h * 0.33));
        g.Children.Add(new RectangleGeometry(new Rect(x + w * 0.12, y + h * 0.62, w * 0.78, h * 0.38), h * 0.19, h * 0.19));
        c.Children.Add(new Path { Data = g, Fill = fill });
    }

    static void Line(Canvas c, double x1, double y1, double x2, double y2, Brush b, double t) =>
        c.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = t, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });

    static void Dot(Canvas c, double cx, double cy, double r, Brush b)
    {
        var e = new Ellipse { Width = r * 2, Height = r * 2, Fill = b };
        Canvas.SetLeft(e, cx - r); Canvas.SetTop(e, cy - r);
        c.Children.Add(e);
    }
}
