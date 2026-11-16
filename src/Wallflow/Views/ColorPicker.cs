using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Wallflow.Views;

/// <summary>Small HSV color picker dialog: saturation/brightness square, hue strip, hex field.</summary>
public sealed class ColorPicker : Window
{
    double _h, _s, _v;
    readonly Rectangle _hueLayer = new() { Width = 260, Height = 200 };
    readonly Ellipse _svThumb = new() { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
    readonly Rectangle _hueThumb = new() { Width = 26, Height = 6, Stroke = Brushes.White, StrokeThickness = 2, RadiusX = 3, RadiusY = 3, IsHitTestVisible = false };
    readonly Border _preview = new() { Width = 70, Height = 40, CornerRadius = new CornerRadius(8) };
    readonly TextBox _hex = new() { Width = 110 };
    readonly Canvas _sv = new() { Width = 260, Height = 200, ClipToBounds = true, Cursor = Cursors.Cross };
    readonly Canvas _hue = new() { Width = 22, Height = 200, Cursor = Cursors.Hand };
    bool _updating;

    public Color Result { get; private set; }

    public static Color? Pick(Color initial, Window? owner = null, string? title = null)
    {
        var p = new ColorPicker(initial) { Title = title ?? Loc.T("Выбери цвет") };
        if (owner != null && owner.IsVisible) p.Owner = owner; else p.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return p.ShowDialog() == true ? p.Result : null;
    }

    ColorPicker(Color initial)
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = (Brush)Application.Current.Resources["Bg"];
        Foreground = (Brush)Application.Current.Resources["Fg"];
        UiKit.DarkTitleBar(this);

        // SV square: hue color, white→transparent left to right, transparent→black top to bottom
        var white = new Rectangle { Width = 260, Height = 200, Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) };
        var black = new Rectangle { Width = 260, Height = 200, Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) };
        _sv.Children.Add(_hueLayer); _sv.Children.Add(white); _sv.Children.Add(black); _sv.Children.Add(_svThumb);
        _sv.MouseLeftButtonDown += (_, e) => { _sv.CaptureMouse(); SvAt(e.GetPosition(_sv)); };
        _sv.MouseMove += (_, e) => { if (_sv.IsMouseCaptured) SvAt(e.GetPosition(_sv)); };
        _sv.MouseLeftButtonUp += (_, _) => _sv.ReleaseMouseCapture();

        var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        for (int i = 0; i <= 6; i++) rainbow.GradientStops.Add(new GradientStop(FromHsv(i * 60 % 360, 1, 1), i / 6.0));
        _hue.Children.Add(new Rectangle { Width = 22, Height = 200, Fill = rainbow, RadiusX = 4, RadiusY = 4 });
        _hue.Children.Add(_hueThumb);
        _hue.MouseLeftButtonDown += (_, e) => { _hue.CaptureMouse(); HueAt(e.GetPosition(_hue).Y); };
        _hue.MouseMove += (_, e) => { if (_hue.IsMouseCaptured) HueAt(e.GetPosition(_hue).Y); };
        _hue.MouseLeftButtonUp += (_, _) => _hue.ReleaseMouseCapture();

        _hex.TextChanged += (_, _) =>
        {
            if (_updating) return;
            var t = _hex.Text.Trim();
            if (t.Length is 7 or 6)
            {
                try { SetColor((Color)ColorConverter.ConvertFromString(t.StartsWith('#') ? t : "#" + t), updateHex: false); }
                catch { }
            }
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = _sv });
        row.Children.Add(new Border { Margin = new Thickness(12, 0, 0, 0), Child = _hue });

        var bottom = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
        bottom.Children.Add(_preview);
        var hexRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        hexRow.Children.Add(new TextBlock { Text = "HEX", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Foreground = (Brush)Application.Current.Resources["Muted"] });
        hexRow.Children.Add(_hex);
        bottom.Children.Add(hexRow);
        var ok = new Button { Content = Loc.T("Готово"), IsDefault = true, Style = (Style)Application.Current.Resources["Primary"], MinWidth = 90 };
        ok.Click += (_, _) => { Result = FromHsv(_h, _s, _v); DialogResult = true; };
        DockPanel.SetDock(ok, Dock.Right);
        bottom.Children.Add(ok);

        // quick swatches
        var swatches = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), Width = 294 };
        foreach (var hx in new[] { "#FFFFFF", "#FF5CA8", "#FF2D55", "#FF9500", "#FFD60A", "#34C759", "#5AE6FF", "#0A84FF", "#7C5CFF", "#BF5AF2", "#A2845E", "#8E8E93" })
        {
            var c = (Color)ColorConverter.ConvertFromString(hx);
            var b = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 4, 0), Background = new SolidColorBrush(c), Cursor = Cursors.Hand };
            b.MouseLeftButtonUp += (_, _) => SetColor(c);
            swatches.Children.Add(b);
        }

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(row);
        root.Children.Add(swatches);
        root.Children.Add(bottom);
        Content = root;
        SetColor(initial);
    }

    void SvAt(Point p)
    {
        _s = Math.Clamp(p.X / _sv.Width, 0, 1);
        _v = 1 - Math.Clamp(p.Y / _sv.Height, 0, 1);
        Update();
    }

    void HueAt(double y)
    {
        _h = Math.Clamp(y / _hue.Height, 0, 0.9999) * 360;
        Update();
    }

    void SetColor(Color c, bool updateHex = true)
    {
        ToHsv(c, out _h, out _s, out _v);
        Update(updateHex);
    }

    void Update(bool updateHex = true)
    {
        _hueLayer.Fill = new SolidColorBrush(FromHsv(_h, 1, 1));
        Canvas.SetLeft(_svThumb, _s * _sv.Width - 7);
        Canvas.SetTop(_svThumb, (1 - _v) * _sv.Height - 7);
        Canvas.SetLeft(_hueThumb, -2);
        Canvas.SetTop(_hueThumb, _h / 360 * _hue.Height - 3);
        var c = FromHsv(_h, _s, _v);
        _preview.Background = new SolidColorBrush(c);
        if (updateHex) { _updating = true; _hex.Text = UiKit.ToHex(c); _updating = false; }
    }

    public static Color FromHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    static void ToHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        h = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        s = max == 0 ? 0 : d / max;
        v = max;
    }
}
