using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>Day name + time + date in one of several typographic styles. No panel by default — just text on the wallpaper.</summary>
public sealed class ClockWidget : GlassWindow
{
    public static readonly (string Key, string Title)[] Faces =
    {
        ("poster", "Постер — Bebas Neue"),
        ("script", "Каллиграфия — Great Vibes"),
        ("neon", "Неон — Orbitron"),
        ("ios", "Как на iPhone — Segoe UI"),
        ("bold", "Жирный — Unbounded"),
        ("retro", "Ретро — Monoton"),
        ("mono", "Моно — Major Mono"),
        ("soft", "Мягкий — Comfortaa"),
        ("thin", "Тонкий — Poiret One")
    };

    readonly DispatcherTimer _timer = new(DispatcherPriority.Background);
    readonly ContentControl _host = new();

    protected override bool HasGlass => false;

    public ClockWidget() : base(SettingsStore.Current.Widgets.ClockPos)
    {
        Glass.Child = _host;
        Glass.Padding = new Thickness(10);
        _timer.Tick += (_, _) => Tick();
        Refresh();
    }

    protected override Point DefaultPosition(Rect wa, Size s) => new(wa.Left + 70, wa.Top + 50);

    public override void Refresh()
    {
        Render();
        Schedule();
    }

    void Tick() { Render(); Schedule(); }

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

    void Render()
    {
        _host.Content = BuildFace(SettingsStore.Current.Widgets.ClockStyle, DateTime.Now, SettingsStore.Current.Widgets);
    }

    protected override void OnClosed(EventArgs e) { _timer.Stop(); base.OnClosed(e); }

    // ------------------------------------------------------------------
    public static FrameworkElement BuildFace(string style, DateTime now, WidgetSettings w)
    {
        var lang = w.Language;
        string day = Lang.DayName(now, lang);
        string date = Lang.DayMonth(now, lang);
        string time = w.Use24h
            ? now.ToString(w.ShowSeconds ? "HH:mm:ss" : "HH:mm")
            : now.ToString(w.ShowSeconds ? "h:mm:ss" : "h:mm", Lang.Culture("en"));
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var shadow = UiKit.SoftShadow();

        switch (style)
        {
            case "script":
            {
                var d = UiKit.Text(day, UiKit.Font("Great Vibes"), 130);
                d.Margin = new Thickness(0, -20, 0, -18);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Spaced($"{date.ToUpper()}  ·  {time}", UiKit.Font("Poiret One"), 24, 3, UiKit.WhiteSoft));
                sp.Effect = shadow;
                break;
            }
            case "neon":
            {
                var cyan = Color.FromRgb(0, 229, 255);
                sp.Children.Add(UiKit.Spaced(day.ToUpper(), UiKit.Font("Orbitron"), 26, 12, UiKit.Hex("#7DF3FF")));
                var t = UiKit.Text(time, UiKit.Font("Orbitron"), 96, UiKit.White, FontWeights.Bold);
                t.Effect = UiKit.Glow(cyan, 28, 1);
                t.Margin = new Thickness(0, -6, 0, -4);
                sp.Children.Add(t);
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), UiKit.Font("Orbitron"), 15, 7, UiKit.Hex("#7DF3FF", 200)));
                break;
            }
            case "ios":
            {
                var f = UiKit.SysFont("Segoe UI Variable Display, Segoe UI");
                sp.Children.Add(UiKit.Text($"{day}, {date}", f, 24, UiKit.WhiteSoft, FontWeights.SemiBold));
                var t = UiKit.Text(time, f, 128, UiKit.White, FontWeights.SemiBold);
                t.Margin = new Thickness(0, -18, 0, -10);
                sp.Children.Add(t);
                sp.Effect = shadow;
                break;
            }
            case "bold":
            {
                sp.HorizontalAlignment = HorizontalAlignment.Left;
                var d = UiKit.Text(day.ToUpper(), UiKit.Font("Unbounded"), 72, UiKit.White, FontWeights.Black, HorizontalAlignment.Left);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Text($"{date} — {time}", UiKit.Font("Unbounded"), 26, UiKit.WhiteSoft, FontWeights.Light, HorizontalAlignment.Left));
                sp.Effect = shadow;
                break;
            }
            case "retro":
            {
                var pink = Color.FromRgb(255, 92, 168);
                var t = UiKit.Text(time, UiKit.Font("Monoton"), 104, UiKit.Hex("#FFD1E8"));
                t.Effect = UiKit.Glow(pink, 26, 1);
                sp.Children.Add(t);
                sp.Children.Add(UiKit.Spaced(day.ToUpper(), UiKit.Font("Monoton"), 30, 4, UiKit.Hex("#FF8CC6")));
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), UiKit.Font("Poiret One"), 18, 5, UiKit.WhiteSoft));
                break;
            }
            case "mono":
            {
                sp.HorizontalAlignment = HorizontalAlignment.Left;
                var f = UiKit.Font("Major Mono Display", "Consolas");
                sp.Children.Add(UiKit.Text(day.ToLower(), f, 66, UiKit.White, null, HorizontalAlignment.Left));
                sp.Children.Add(UiKit.Text($"{time}  {date.ToLower()}", f, 28, UiKit.WhiteSoft, null, HorizontalAlignment.Left));
                sp.Effect = shadow;
                break;
            }
            case "soft":
            {
                var f = UiKit.Font("Comfortaa");
                var t = UiKit.Text(time, f, 112, UiKit.White, FontWeights.Bold);
                t.Margin = new Thickness(0, -10, 0, -6);
                sp.Children.Add(t);
                sp.Children.Add(UiKit.Text($"{day.ToLower()}, {date}", f, 26, UiKit.WhiteSoft));
                sp.Effect = shadow;
                break;
            }
            case "thin":
            {
                var f = UiKit.Font("Poiret One");
                sp.Children.Add(UiKit.Spaced(day.ToUpper(), f, 54, 14, UiKit.White));
                sp.Children.Add(UiKit.Spaced(time, f, 40, 8, UiKit.WhiteSoft));
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), f, 16, 6, UiKit.WhiteDim));
                sp.Effect = shadow;
                break;
            }
            default: // poster
            {
                var d = UiKit.Spaced(day.ToUpper(), UiKit.Font("Bebas Neue", "Segoe UI Black"), 124, 7, UiKit.White);
                d.Margin = new Thickness(0, -10, 0, -14);
                sp.Children.Add(d);
                sp.Children.Add(UiKit.Spaced(date.ToUpper(), UiKit.Font("Poiret One"), 20, 9, UiKit.WhiteSoft));
                var t = UiKit.Spaced(time, UiKit.Font("Bebas Neue", "Segoe UI Black"), 46, 5, UiKit.White);
                t.Margin = new Thickness(0, 6, 0, 0);
                sp.Children.Add(t);
                sp.Effect = shadow;
                break;
            }
        }
        return sp;
    }
}
