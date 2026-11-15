using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>Sticky note on glass: title + text, own color, font size and width. Saved automatically.</summary>
public sealed class NoteWidget : GlassWindow
{
    static readonly string[] NoteColors = { "#FFD27A", "#FF7AC6", "#7DF3FF", "#7CFFC4", "#B69CFF", "#FF6B6B", "#FFFFFF" };
    static readonly (string Label, double Px)[] Sizes = { ("Мелкий", 12.5), ("Обычный", 15), ("Крупный", 18), ("Очень крупный", 22), ("Огромный", 28) };
    static readonly (string Label, double Px)[] Widths = { ("Узкая", 200), ("Обычная", 260), ("Широкая", 340), ("Очень широкая", 440) };

    readonly NoteDef _note;
    readonly int _index;
    readonly TextBox _title, _box;
    readonly Border _strip;
    readonly StackPanel _tools;
    readonly DockPanel _root;
    readonly DispatcherTimer _save = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(700) };

    protected override bool Activatable => true;
    public NoteDef Note => _note;

    public NoteWidget(NoteDef note, int index) : base(note.Pos)
    {
        _note = note;
        _index = index;
        Glass.Padding = new Thickness(16, 10, 16, 14);
        var font = UiKit.SysFont("Segoe UI Variable Text, Segoe UI");

        _root = new DockPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

        // tools appear on hover: smaller/bigger text, delete
        _tools = new StackPanel { Orientation = Orientation.Horizontal, Opacity = 0, VerticalAlignment = VerticalAlignment.Center };
        _tools.Children.Add(Tool("A−", "Текст меньше", () => Font(-1)));
        _tools.Children.Add(Tool("A+", "Текст больше", () => Font(+1)));
        _tools.Children.Add(Tool("✕", "Удалить заметку", Delete));
        DockPanel.SetDock(_tools, Dock.Right);
        head.Children.Add(_tools);

        _strip = new Border { Width = 30, Height = 6, CornerRadius = new CornerRadius(3), Cursor = Cursors.Hand, ToolTip = "Сменить цвет", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        _strip.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            int i = Array.IndexOf(NoteColors, _note.Color.ToUpperInvariant());
            _note.Color = NoteColors[(i + 1) % NoteColors.Length];
            Paint();
            SettingsStore.Save();
        };
        head.Children.Add(_strip);
        DockPanel.SetDock(head, Dock.Top);
        _root.Children.Add(head);

        _title = Field(note.Title, font, true);
        _box = Field(note.Text, font, false);
        _title.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _box.Focus(); _box.CaretIndex = _box.Text.Length; } };
        var titleGrid = WithHint(_title, "Заголовок", font, true);
        titleGrid.Margin = new Thickness(0, 0, 0, 4);
        DockPanel.SetDock(titleGrid, Dock.Top);
        _root.Children.Add(titleGrid);

        _box.AcceptsReturn = true; _box.AcceptsTab = true;
        _box.MinHeight = 90; _box.MaxHeight = 520;
        _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _root.Children.Add(WithHint(_box, "Текст заметки…", font, false));

        Glass.Child = _root;
        ContextMenu = Menu();
        MouseEnter += (_, _) => Fade(_tools, 1);
        MouseLeave += (_, _) => Fade(_tools, 0);
        _save.Tick += (_, _) => { _save.Stop(); Store(); };
        if (string.IsNullOrEmpty(note.Text) && string.IsNullOrEmpty(note.Title))
            Loaded += (_, _) => { Activate(); _title.Focus(); };
        Paint();
        ApplySize();
    }

    TextBox Field(string text, FontFamily font, bool title)
    {
        var t = new TextBox
        {
            Text = text, TextWrapping = TextWrapping.Wrap, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(0), Foreground = UiKit.White, CaretBrush = UiKit.White, SelectionBrush = UiKit.Hex("#7C5CFF"),
            FontFamily = font, FontWeight = title ? FontWeights.Bold : FontWeights.Normal
        };
        t.Style = null;
        t.TextChanged += (_, _) => { _save.Stop(); _save.Start(); };
        return t;
    }

    static Grid WithHint(TextBox box, string hint, FontFamily font, bool title)
    {
        var h = new TextBlock { Text = hint, Foreground = UiKit.WhiteDim, FontFamily = font, IsHitTestVisible = false, FontWeight = title ? FontWeights.Bold : FontWeights.Normal };
        h.SetBinding(TextBlock.FontSizeProperty, new System.Windows.Data.Binding("FontSize") { Source = box });
        void Upd() => h.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        box.TextChanged += (_, _) => Upd();
        Upd();
        var g = new Grid();
        g.Children.Add(box);
        g.Children.Add(h);
        return g;
    }

    static FrameworkElement Tool(string text, string tip, Action click)
    {
        var t = new TextBlock { Text = text, FontSize = 12, Foreground = UiKit.WhiteSoft, Margin = new Thickness(10, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = tip, FontWeight = FontWeights.SemiBold };
        t.MouseLeftButtonUp += (_, e) => { e.Handled = true; click(); };
        return t;
    }

    static void Fade(UIElement el, double to) =>
        el.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(to, TimeSpan.FromMilliseconds(150)));

    ContextMenu Menu()
    {
        var m = new ContextMenu();
        var size = new MenuItem { Header = "Размер текста" };
        foreach (var (label, px) in Sizes)
        {
            var mi = new MenuItem { Header = label, Tag = px };
            mi.Click += (_, _) => { _note.FontSize = px; ApplySize(); Store(); };
            size.Items.Add(mi);
        }
        var width = new MenuItem { Header = "Ширина" };
        foreach (var (label, px) in Widths)
        {
            var mi = new MenuItem { Header = label, Tag = px };
            mi.Click += (_, _) => { _note.Width = px; ApplySize(); Store(); };
            width.Items.Add(mi);
        }
        m.Opened += (_, _) =>
        {
            foreach (MenuItem i in size.Items) i.IsChecked = Math.Abs((double)i.Tag - _note.FontSize) < 0.1;
            foreach (MenuItem i in width.Items) i.IsChecked = Math.Abs((double)i.Tag - _note.Width) < 0.1;
        };
        var del = new MenuItem { Header = "Удалить заметку" };
        del.Click += (_, _) => Delete();
        m.Items.Add(size); m.Items.Add(width); m.Items.Add(new Separator()); m.Items.Add(del);
        return m;
    }

    void Font(int dir)
    {
        _note.FontSize = Math.Clamp(_note.FontSize + dir * 1.5, 10, 40);
        ApplySize();
        Store();
    }

    void ApplySize()
    {
        _box.FontSize = _note.FontSize;
        _title.FontSize = _note.FontSize + 3;
        _root.Width = Math.Clamp(_note.Width, 160, 700);
    }

    void Delete()
    {
        if ((!string.IsNullOrWhiteSpace(_box.Text) || !string.IsNullOrWhiteSpace(_title.Text)) &&
            MessageBox.Show("Удалить эту заметку?", "Wallflow", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        App.Me.Widgets.RemoveNote(_note);
    }

    void Store()
    {
        _note.Text = _box.Text;
        _note.Title = _title.Text;
        SettingsStore.Save();
    }

    void Paint() => _strip.Background = UiKit.Hex(_note.Color);

    protected override Point DefaultPosition(Rect wa, Size s) =>
        new(wa.Right - 420 - s.Width - 30 - _index * 30, wa.Top + 60 + _index * 40);

    protected override void OnClosed(EventArgs e)
    {
        if (_save.IsEnabled) { _save.Stop(); Store(); }
        base.OnClosed(e);
    }
}
