using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

public sealed class CalendarWidget : GlassWindow
{
    readonly DispatcherTimer _midnight = new(DispatcherPriority.Background);
    readonly ContentControl _host = new();

    public CalendarWidget() : base(SettingsStore.Current.Widgets.CalendarPos)
    {
        Glass.Padding = new Thickness(20, 16, 20, 16);
        Glass.Child = _host;
        _midnight.Tick += (_, _) => Refresh();
        Microsoft.Win32.SystemEvents.TimeChanged += OnTimeChanged;
        Refresh();
    }

    void OnTimeChanged(object? s, EventArgs e) => Dispatcher.BeginInvoke(Refresh);

    protected override Point DefaultPosition(Rect wa, Size s) => new(wa.Right - s.Width - 70, wa.Top + 330);

    public override void Refresh()
    {
        Render(DateTime.Today);
        _midnight.Interval = DateTime.Today.AddDays(1) - DateTime.Now + TimeSpan.FromSeconds(2);
        _midnight.Start();
    }

    void Render(DateTime today)
    {
        var lang = SettingsStore.Current.Widgets.Language;
        var f = UiKit.SysFont("Segoe UI Variable Display, Segoe UI");
        var root = new StackPanel { Width = 300 };

        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var month = UiKit.Text(Lang.MonthYear(today, lang), f, 18, UiKit.White, FontWeights.SemiBold, HorizontalAlignment.Left);
        head.Children.Add(month);
        var dayName = UiKit.Text(Lang.DayName(today, lang), f, 14, UiKit.Hex("#FF6B6B"), FontWeights.SemiBold, HorizontalAlignment.Right);
        dayName.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(dayName, Dock.Right);
        head.Children.Insert(0, dayName);
        root.Children.Add(head);

        var grid = new UniformGrid { Columns = 7 };
        // Monday first
        var names = lang == "ru"
            ? new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }
            : new[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
        for (int i = 0; i < 7; i++)
            grid.Children.Add(UiKit.Text(names[i], f, 11.5, i >= 5 ? UiKit.Hex("#FF8A8A", 200) : UiKit.WhiteDim, FontWeights.SemiBold));

        var first = new DateTime(today.Year, today.Month, 1);
        int offset = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-offset);
        int weeks = (int)Math.Ceiling((offset + DateTime.DaysInMonth(today.Year, today.Month)) / 7.0);
        for (int i = 0; i < weeks * 7; i++)
        {
            var d = start.AddDays(i);
            bool inMonth = d.Month == today.Month;
            bool isToday = d == today;
            bool weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var cell = new Grid { Height = 34 };
            if (isToday)
                cell.Children.Add(new Ellipse { Width = 30, Height = 30, Fill = UiKit.Hex("#FF453A") });
            var t = UiKit.Text(d.Day.ToString(), f, 14,
                isToday ? UiKit.White : !inMonth ? UiKit.Brush(60, 255, 255, 255) : weekend ? UiKit.Hex("#FF9A9A") : UiKit.White,
                isToday ? FontWeights.Bold : FontWeights.Normal);
            t.VerticalAlignment = VerticalAlignment.Center;
            cell.Children.Add(t);
            grid.Children.Add(cell);
        }
        root.Children.Add(grid);
        _host.Content = root;
    }

    protected override void OnClosed(EventArgs e)
    {
        _midnight.Stop();
        Microsoft.Win32.SystemEvents.TimeChanged -= OnTimeChanged;
        base.OnClosed(e);
    }
}
