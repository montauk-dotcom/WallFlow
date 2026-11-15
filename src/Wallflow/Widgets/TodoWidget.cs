using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>Simple to-do list: Enter adds, click toggles, ✕ deletes.</summary>
public sealed class TodoWidget : GlassWindow
{
    readonly StackPanel _list = new();
    readonly TextBlock _count;
    readonly TextBox _input;

    protected override bool Activatable => true;
    static WidgetSettings W => SettingsStore.Current.Widgets;

    public TodoWidget() : base(SettingsStore.Current.Widgets.TodoPos)
    {
        Glass.Padding = new Thickness(18, 14, 18, 14);
        var f = UiKit.SysFont("Segoe UI Variable Text, Segoe UI");
        var root = new StackPanel { Width = 290 };

        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        _count = UiKit.Text("", f, 12.5, UiKit.WhiteDim);
        _count.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_count, Dock.Right);
        head.Children.Add(_count);
        head.Children.Add(UiKit.Text(W.Language == "ru" ? "Список дел" : "To-do", UiKit.SysFont("Segoe UI Variable Display, Segoe UI"), 18, UiKit.White, FontWeights.SemiBold, HorizontalAlignment.Left));
        root.Children.Add(head);

        root.Children.Add(new ScrollViewer { Content = _list, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        _input = new TextBox
        {
            Background = UiKit.Brush(28, 255, 255, 255), BorderThickness = new Thickness(0), Foreground = UiKit.White, CaretBrush = UiKit.White,
            Padding = new Thickness(10, 7, 10, 7), FontFamily = f, FontSize = 14, Margin = new Thickness(0, 8, 0, 0)
        };
        _input.Style = null;
        var hint = UiKit.Text(W.Language == "ru" ? "Новая задача… (Enter)" : "New task… (Enter)", f, 14, UiKit.WhiteDim, null, HorizontalAlignment.Left);
        hint.IsHitTestVisible = false;
        hint.Margin = new Thickness(11, 15, 0, 0);
        var inputGrid = new Grid();
        inputGrid.Children.Add(_input);
        inputGrid.Children.Add(hint);
        _input.TextChanged += (_, _) => hint.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _input.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(_input.Text)) return;
            W.TodoItems.Add(new TodoItem { Text = _input.Text.Trim() });
            _input.Clear();
            Changed();
        };
        root.Children.Add(inputGrid);

        Glass.Child = root;
        Render();
    }

    // bottom centre, above the dock area
    protected override Point DefaultPosition(Rect wa, Size s) => new(wa.Left + (wa.Width - s.Width) / 2, wa.Bottom - s.Height - 110);

    void Changed() { SettingsStore.Save(); Render(); }

    void Render()
    {
        _list.Children.Clear();
        var f = UiKit.SysFont("Segoe UI Variable Text, Segoe UI");
        // undone first, done at the bottom
        foreach (var item in W.TodoItems.OrderBy(i => i.Done).ToList())
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var del = new TextBlock { Text = "✕", FontSize = 11, Foreground = UiKit.WhiteDim, Visibility = Visibility.Hidden, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            del.MouseLeftButtonUp += (_, e) => { e.Handled = true; W.TodoItems.Remove(item); Changed(); };
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);

            var box = new Border
            {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Margin = new Thickness(0, 1, 10, 0), VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(1.5), BorderBrush = item.Done ? UiKit.Hex("#7C5CFF") : UiKit.WhiteSoft,
                Background = item.Done ? UiKit.Hex("#7C5CFF") : Brushes.Transparent,
                Child = item.Done ? new TextBlock { Text = "✓", FontSize = 11, Foreground = UiKit.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : null
            };
            DockPanel.SetDock(box, Dock.Left);
            row.Children.Add(box);

            var text = UiKit.Text(item.Text, f, 14.5, item.Done ? UiKit.WhiteDim : UiKit.White, null, HorizontalAlignment.Left);
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Left;
            if (item.Done) text.TextDecorations = TextDecorations.Strikethrough;
            row.Children.Add(text);

            row.MouseEnter += (_, _) => del.Visibility = Visibility.Visible;
            row.MouseLeave += (_, _) => del.Visibility = Visibility.Hidden;
            row.MouseLeftButtonUp += (_, _) => { item.Done = !item.Done; Changed(); };
            _list.Children.Add(row);
        }
        int left = W.TodoItems.Count(i => !i.Done);
        _count.Text = W.TodoItems.Count == 0 ? "" : W.Language == "ru" ? $"осталось {left}" : $"{left} left";
    }
}
