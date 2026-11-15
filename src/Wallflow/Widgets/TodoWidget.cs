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
    readonly TextBlock _header;

    static string ListTitle() => !string.IsNullOrWhiteSpace(W.TodoTitle) ? W.TodoTitle : W.Language == "ru" ? "Список дел" : "To-do";

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
        var more = new TextBlock { Text = "⋯", FontSize = 18, Foreground = UiKit.WhiteSoft, Cursor = Cursors.Hand, Margin = new Thickness(10, -4, 0, 0), ToolTip = "Настройки списка", VerticalAlignment = VerticalAlignment.Center };
        more.MouseLeftButtonUp += (_, e) => { e.Handled = true; ContextMenu.PlacementTarget = more; ContextMenu.IsOpen = true; };
        DockPanel.SetDock(more, Dock.Right);
        head.Children.Insert(0, more);
        _header = UiKit.Text(ListTitle(), UiKit.SysFont("Segoe UI Variable Display, Segoe UI"), 18, UiKit.White, FontWeights.SemiBold, HorizontalAlignment.Left);
        head.Children.Add(_header);
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
        ContextMenu = BuildMenu();
        Render();
    }

    ContextMenu BuildMenu()
    {
        var m = new ContextMenu();
        var size = new MenuItem { Header = "Размер текста" };
        foreach (var (label, px) in new[] { ("Мелкий", 12.5), ("Обычный", 14.5), ("Крупный", 17.0), ("Очень крупный", 20.0), ("Огромный", 24.0) })
        {
            var mi = new MenuItem { Header = label, Tag = px };
            mi.Click += (_, _) => { W.TodoFontSize = px; Changed(); };
            size.Items.Add(mi);
        }
        var space = new MenuItem { Header = "Расстояние между пунктами" };
        foreach (var (label, px) in new[] { ("Плотно", 0.0), ("Обычно", 3.0), ("Свободно", 7.0), ("Просторно", 12.0) })
        {
            var mi = new MenuItem { Header = label, Tag = px };
            mi.Click += (_, _) => { W.TodoSpacing = px; Changed(); };
            space.Items.Add(mi);
        }
        var bottom = new MenuItem { Header = "Выполненные опускать вниз", IsCheckable = true };
        bottom.Click += (_, _) => { W.TodoDoneToBottom = bottom.IsChecked; Changed(); };
        var clear = new MenuItem { Header = "Удалить выполненные" };
        clear.Click += (_, _) => { W.TodoItems.RemoveAll(i => i.Done); Changed(); };
        var rename = new MenuItem { Header = "Переименовать список…" };
        rename.Click += (_, _) =>
        {
            var t = Prompt.Ask("Название списка", ListTitle());
            if (t is null) return;
            W.TodoTitle = t.Trim();
            _header.Text = ListTitle();
            SettingsStore.Save();
        };
        m.Opened += (_, _) =>
        {
            foreach (MenuItem i in size.Items) i.IsChecked = Math.Abs((double)i.Tag - W.TodoFontSize) < 0.1;
            foreach (MenuItem i in space.Items) i.IsChecked = Math.Abs((double)i.Tag - W.TodoSpacing) < 0.1;
            bottom.IsChecked = W.TodoDoneToBottom;
        };
        m.Items.Add(size); m.Items.Add(space); m.Items.Add(bottom);
        m.Items.Add(new Separator()); m.Items.Add(rename); m.Items.Add(clear);
        return m;
    }

    // bottom centre, above the dock area
    protected override Point DefaultPosition(Rect wa, Size s) => new(wa.Left + (wa.Width - s.Width) / 2, wa.Bottom - s.Height - 110);

    void Changed() { SettingsStore.Save(); Render(); }

    void Render()
    {
        _list.Children.Clear();
        var f = UiKit.SysFont("Segoe UI Variable Text, Segoe UI");
        // undone first, done at the bottom
        double fs = Math.Clamp(W.TodoFontSize, 10, 36), gap = Math.Clamp(W.TodoSpacing, 0, 30);
        var items = W.TodoDoneToBottom ? W.TodoItems.OrderBy(i => i.Done).ToList() : W.TodoItems.ToList();
        foreach (var item in items)
        {
            var row = new DockPanel { Margin = new Thickness(0, gap, 0, gap), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var del = new TextBlock { Text = "✕", FontSize = fs * 0.75, Foreground = UiKit.WhiteDim, Visibility = Visibility.Hidden, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            del.MouseLeftButtonUp += (_, e) => { e.Handled = true; W.TodoItems.Remove(item); Changed(); };
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);

            var box = new Border
            {
                Width = fs * 1.25, Height = fs * 1.25, CornerRadius = new CornerRadius(fs * 0.625), Margin = new Thickness(0, fs * 0.1, fs * 0.65, 0), VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(1.5), BorderBrush = item.Done ? UiKit.Hex("#7C5CFF") : UiKit.WhiteSoft,
                Background = item.Done ? UiKit.Hex("#7C5CFF") : Brushes.Transparent,
                Child = item.Done ? new TextBlock { Text = "✓", FontSize = fs * 0.75, Foreground = UiKit.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : null
            };
            DockPanel.SetDock(box, Dock.Left);
            row.Children.Add(box);

            var text = UiKit.Text(item.Text, f, fs, item.Done ? UiKit.WhiteDim : UiKit.White, null, HorizontalAlignment.Left);
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
