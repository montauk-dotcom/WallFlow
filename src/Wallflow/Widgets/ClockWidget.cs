using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>Day name + time + date in one of many typographic styles. Just text on the wallpaper, no panel.</summary>
public sealed class ClockWidget : GlassWindow
{
    public static readonly (string Key, string Title)[] Faces =
    {
        ("poster", "Постер"),
        ("outline", "Контур"),
        ("script", "Каллиграфия"),
        ("neon", "Неон"),
        ("gradient", "Градиент"),
        ("stack", "Стопка"),
        ("ios", "Как на iPhone"),
        ("analog", "Стрелочные"),
        ("words", "Словами"),
        ("bold", "Жирный"),
        ("retro", "Ретро"),
        ("mono", "Моно"),
        ("soft", "Мягкий"),
        ("thin", "Тонкий"),
        ("minimal", "Минимум")
    };

    public static readonly (string Key, string Title, string Hex)[] Palette =
    {
        ("white", "Белый", "#FFFFFF"), ("pink", "Розовый", "#FF7AC6"), ("cyan", "Голубой", "#7DF3FF"),
        ("violet", "Фиолетовый", "#B69CFF"), ("gold", "Золотой", "#FFD27A"), ("mint", "Мятный", "#7CFFC4"), ("red", "Красный", "#FF6B6B")
    };

    readonly DispatcherTimer _timer = new(DispatcherPriority.Background);
    readonly ContentControl _host = new();

    protected override bool HasGlass => false;

    public ClockWidget() : base(SettingsStore.Current.Widgets.ClockPos)
    {
        Glass.Child = _host;
        Glass.Padding = new Thickness(10);
        _timer.Tick += (_, _) => { Render(); Schedule(); };
        Refresh();
    }

    protected override Point DefaultPosition(Rect wa, Size s) => new(wa.Left + 70, wa.Top + 50);

    public override void Refresh() { Render(); Schedule(); }

    /// <summary>Wake up exactly on the next minute (or second) — zero work in between.</summary>
    void Schedule()
    {
        var now = DateTime.Now;
        var next = SettingsStore.Current.Widgets.ShowSeconds
            ? now.AddSeconds(1).AddMilliseconds(-now.Millisecond)
            : now.AddMinutes(1).AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond);
        _timer.Interval = next - now + TimeSpan.FromMilliseconds(30);
        _timer.Start();
    }

    void Render() => _host.Content = BuildFace(SettingsStore.Current.Widgets.ClockStyle, DateTime.Now, SettingsStore.Current.Widgets);

    protected override void OnClosed(EventArgs e) { _timer.Stop(); base.OnClosed(e); }

    static Color MainColor(WidgetSettings w)
    {
        foreach (var p in Palette) if (p.Key == w.ClockColor) return (Color)ColorConverter.ConvertFromString(p.Hex);
        return Colors.White;
    }

    static SolidColorBrush B(Color c, byte a = 255) { var b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); b.Freeze(); return b; }

    // ------------------------------------------------------------------
    public static FrameworkElement BuildFace(string style, DateTime now, WidgetSettings w)
    {
        var lang = w.Language;
        string day = Lang.DayName(now, lang);
        string date = Lang.DayMonth(now, lang);
        string hh = w.Use24h ? now.ToString("HH") : now.ToString("%h");
        string mm = now.ToString("mm");
        string time = $"{hh}:{mm}" + (w.ShowSeconds ? now.ToString(":ss") : "");

        var c = MainColor(w);
        var main = B(c);
        var soft = B(c, 205);
        var dim = B(c, 150);
        bool white = w.ClockColor == "white";
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var shadow = w.TextShadow ? UiKit.SoftShadow() : null;

        switch (style)
        {
            case "script":
            {
                var d = UiKit.Text(day, UiKit.Font("Great Vibes"), 130, main);
                d.Margin = new Thickness(0, -20, 0, -18);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Spaced($"{date.ToUpper()}  ·  {time}", UiKit.Font("Poiret One"), 24, 3, soft));
                sp.Effect = shadow;
                break;
            }
            case "outline":
            {
                var d = UiKit.Outline(day.ToUpper(), UiKit.Font("Bebas Neue", "Segoe UI Black"), 140, main, 2.2);
                d.Margin = new Thickness(0, 0, 0, -6);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Spaced($"{time}   {date.ToUpper()}", UiKit.Font("Bebas Neue", "Segoe UI Black"), 38, 6, main));
                sp.Effect = shadow;
                break;
            }
            case "neon":
            {
                var glow = white ? Color.FromRgb(0, 229, 255) : c;
                var light = white ? UiKit.Hex("#7DF3FF") : main;
                sp.Children.Add(UiKit.Spaced(day.ToUpper(), UiKit.Font("Orbitron"), 26, 12, light));
                var t = UiKit.Text(time, UiKit.Font("Orbitron"), 96, UiKit.White, FontWeights.Bold);
                t.Effect = UiKit.Glow(glow, 28, 1);
                t.Margin = new Thickness(0, -6, 0, -4);
                sp.Children.Add(t);
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), UiKit.Font("Orbitron"), 15, 7, light));
                break;
            }
            case "gradient":
            {
                var grad = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(255, 92, 168), 0), new GradientStop(Color.FromRgb(160, 120, 255), 0.5), new GradientStop(Color.FromRgb(90, 230, 255), 1)
                }, 0);
                grad.Freeze();
                var d = UiKit.Text(day.ToUpper(), UiKit.Font("Unbounded"), 72, grad, FontWeights.Black);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Spaced($"{time}  ·  {date.ToUpper()}", UiKit.Font("Unbounded"), 22, 3, UiKit.WhiteSoft, FontWeights.Light));
                sp.Effect = shadow;
                break;
            }
            case "stack":
            {
                var f = UiKit.Font("Bebas Neue", "Segoe UI Black");
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var nums = new StackPanel();
                var h = UiKit.Text(hh.PadLeft(2, '0'), f, 170, main);
                h.Margin = new Thickness(0, -30, 0, -48);
                nums.Children.Add(h);
                var m = UiKit.Outline(mm, f, 170, main, 2.5);
                nums.Children.Add(m);
                row.Children.Add(nums);
                var side = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
                side.Children.Add(UiKit.Text(day.ToUpper(), UiKit.Font("Poiret One"), 26, main, FontWeights.Bold, HorizontalAlignment.Left));
                side.Children.Add(UiKit.Text(date, UiKit.Font("Poiret One"), 20, soft, null, HorizontalAlignment.Left));
                row.Children.Add(side);
                sp.Children.Add(row);
                sp.Effect = shadow;
                break;
            }
            case "ios":
            {
                var f = UiKit.SysFont("Segoe UI Variable Display, Segoe UI");
                sp.Children.Add(UiKit.Text($"{day}, {date}", f, 24, soft, FontWeights.SemiBold));
                var t = UiKit.Text(time, f, 128, main, FontWeights.SemiBold);
                t.Margin = new Thickness(0, -18, 0, -10);
                sp.Children.Add(t);
                sp.Effect = shadow;
                break;
            }
            case "analog":
            {
                sp.Children.Add(Analog(now, w.ShowSeconds, main, dim));
                var d = UiKit.Spaced(day.ToUpper(), UiKit.Font("Poiret One"), 22, 8, main);
                d.Margin = new Thickness(0, 12, 0, 0);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), UiKit.Font("Poiret One"), 14, 5, dim));
                sp.Effect = shadow;
                break;
            }
            case "words":
            {
                sp.HorizontalAlignment = HorizontalAlignment.Left;
                var lines = TimeInWords(now, lang);
                var f = UiKit.Font("Comfortaa");
                for (int i = 0; i < lines.Length; i++)
                {
                    bool strong = i == lines.Length - 1 || (lines.Length == 2 && i == 0);
                    var t = UiKit.Text(lines[i], f, strong ? 58 : 40, strong ? main : soft, strong ? FontWeights.Bold : FontWeights.Normal, HorizontalAlignment.Left);
                    t.Margin = new Thickness(0, -4, 0, -4);
                    sp.Children.Add(t);
                }
                var d = UiKit.Text($"{day.ToLower()}, {date}", f, 20, dim, null, HorizontalAlignment.Left);
                d.Margin = new Thickness(2, 10, 0, 0);
                sp.Children.Add(d);
                sp.Effect = shadow;
                break;
            }
            case "bold":
            {
                sp.HorizontalAlignment = HorizontalAlignment.Left;
                sp.Children.Add(UiKit.Text(day.ToUpper(), UiKit.Font("Unbounded"), 72, main, FontWeights.Black, HorizontalAlignment.Left));
                sp.Children.Add(UiKit.Text($"{date} — {time}", UiKit.Font("Unbounded"), 26, soft, FontWeights.Light, HorizontalAlignment.Left));
                sp.Effect = shadow;
                break;
            }
            case "retro":
            {
                var glow = white ? Color.FromRgb(255, 92, 168) : c;
                var t = UiKit.Text(time, UiKit.Font("Monoton"), 104, white ? UiKit.Hex("#FFD1E8") : main);
                t.Effect = UiKit.Glow(glow, 26, 1);
                sp.Children.Add(t);
                sp.Children.Add(UiKit.Spaced(day.ToUpper(), UiKit.Font("Monoton"), 30, 4, white ? UiKit.Hex("#FF8CC6") : main));
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), UiKit.Font("Poiret One"), 18, 5, UiKit.WhiteSoft));
                break;
            }
            case "mono":
            {
                sp.HorizontalAlignment = HorizontalAlignment.Left;
                var f = UiKit.Font("Major Mono Display", "Consolas");
                sp.Children.Add(UiKit.Text(day.ToLower(), f, 66, main, null, HorizontalAlignment.Left));
                sp.Children.Add(UiKit.Text($"{time}  {date.ToLower()}", f, 28, soft, null, HorizontalAlignment.Left));
                sp.Effect = shadow;
                break;
            }
            case "soft":
            {
                var f = UiKit.Font("Comfortaa");
                var t = UiKit.Text(time, f, 112, main, FontWeights.Bold);
                t.Margin = new Thickness(0, -10, 0, -6);
                sp.Children.Add(t);
                sp.Children.Add(UiKit.Text($"{day.ToLower()}, {date}", f, 26, soft));
                sp.Effect = shadow;
                break;
            }
            case "thin":
            {
                var f = UiKit.Font("Poiret One");
                sp.Children.Add(UiKit.Spaced(day.ToUpper(), f, 54, 14, main));
                sp.Children.Add(UiKit.Spaced(time, f, 40, 8, soft));
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), f, 16, 6, dim));
                sp.Effect = shadow;
                break;
            }
            case "minimal":
            {
                var f = UiKit.SysFont("Segoe UI Variable Display Light, Segoe UI Light");
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(UiKit.Text(time, f, 54, main));
                var line = new Rectangle { Width = 1.5, Height = 46, Fill = dim, Margin = new Thickness(18, 4, 18, 0) };
                row.Children.Add(line);
                var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                right.Children.Add(UiKit.Text(day, f, 22, main, null, HorizontalAlignment.Left));
                right.Children.Add(UiKit.Text(date, f, 18, dim, null, HorizontalAlignment.Left));
                row.Children.Add(right);
                sp.Children.Add(row);
                sp.Effect = shadow;
                break;
            }
            default: // poster
            {
                var d = UiKit.Spaced(day.ToUpper(), UiKit.Font("Bebas Neue", "Segoe UI Black"), 124, 7, main);
                d.Margin = new Thickness(0, -10, 0, -14);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), UiKit.Font("Poiret One"), 20, 9, soft));
                var t = UiKit.Spaced(time, UiKit.Font("Bebas Neue", "Segoe UI Black"), 46, 5, main);
                t.Margin = new Thickness(0, 6, 0, 0);
                sp.Children.Add(t);
                sp.Effect = shadow;
                break;
            }
        }
        return sp;
    }

    static FrameworkElement Analog(DateTime now, bool seconds, Brush main, Brush dim)
    {
        const double S = 200, R = 96, C = S / 2;
        var cv = new Canvas { Width = S, Height = S };
        var face = new Ellipse { Width = R * 2, Height = R * 2, Stroke = dim, StrokeThickness = 1.5 };
        Canvas.SetLeft(face, C - R); Canvas.SetTop(face, C - R);
        cv.Children.Add(face);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            double r1 = i % 3 == 0 ? R - 16 : R - 9;
            cv.Children.Add(Hand(C + Math.Sin(a) * r1, C - Math.Cos(a) * r1, C + Math.Sin(a) * (R - 3), C - Math.Cos(a) * (R - 3),
                main, i % 3 == 0 ? 3 : 1.5));
        }
        double hA = ((now.Hour % 12) + now.Minute / 60.0) * Math.PI / 6;
        double mA = (now.Minute + (seconds ? now.Second / 60.0 : 0)) * Math.PI / 30;
        cv.Children.Add(Hand(C, C, C + Math.Sin(hA) * (R * 0.5), C - Math.Cos(hA) * (R * 0.5), main, 6));
        cv.Children.Add(Hand(C, C, C + Math.Sin(mA) * (R * 0.78), C - Math.Cos(mA) * (R * 0.78), main, 3.5));
        if (seconds)
        {
            double sA = now.Second * Math.PI / 30;
            cv.Children.Add(Hand(C, C, C + Math.Sin(sA) * (R * 0.86), C - Math.Cos(sA) * (R * 0.86), UiKit.Hex("#FF6B6B"), 1.5));
        }
        var dot = new Ellipse { Width = 10, Height = 10, Fill = main };
        Canvas.SetLeft(dot, C - 5); Canvas.SetTop(dot, C - 5);
        cv.Children.Add(dot);
        return cv;
    }

    static Line Hand(double x1, double y1, double x2, double y2, Brush b, double t) => new()
    {
        X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = t,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
    };

    // ---------------- time in words ----------------
    static readonly string[] EnNum =
    {
        "twelve", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"
    };

    static readonly string[] RuUnits =
    {
        "ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять",
        "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"
    };
    static readonly string[] RuTens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят" };

    static string Ru(int n) => n < 20 ? RuUnits[n] : RuTens[n / 10] + (n % 10 == 0 ? "" : " " + RuUnits[n % 10]);

    public static string[] TimeInWords(DateTime t, string lang)
    {
        if (lang == "ru")
        {
            var m = t.Minute;
            var mins = m == 0 ? "ровно" : m < 10 ? "ноль " + RuUnits[m] : Ru(m);
            return new[] { Ru(t.Hour), mins };
        }
        int rounded = (int)Math.Round(t.Minute / 5.0) * 5;
        int h = t.Hour % 12;
        if (rounded == 60) { rounded = 0; h = (h + 1) % 12; }
        string hour = EnNum[h];
        return rounded switch
        {
            0 => new[] { "it's", hour, "o'clock" },
            15 => new[] { "quarter", "past " + hour },
            30 => new[] { "half", "past " + hour },
            45 => new[] { "quarter", "to " + EnNum[(h + 1) % 12] },
            < 30 => new[] { rounded switch { 5 => "five", 10 => "ten", 20 => "twenty", _ => "twenty-five" }, "past " + hour },
            _ => new[] { (60 - rounded) switch { 5 => "five", 10 => "ten", 20 => "twenty", _ => "twenty-five" }, "to " + EnNum[(h + 1) % 12] }
        };
    }
}
