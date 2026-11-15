using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>Sticky note on glass. Text is saved automatically.</summary>
public sealed class NoteWidget : GlassWindow
{
    static readonly string[] NoteColors = { "#FFD27A", "#FF7AC6", "#7DF3FF", "#7CFFC4", "#B69CFF", "#FF6B6B", "#FFFFFF" };

    readonly NoteDef _note;
    readonly int _index;
    readonly TextBox _box;
    readonly Border _strip;
    readonly DispatcherTimer _save = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(700) };

    protected override bool Activatable => true;

    public NoteDef Note => _note;

    public NoteWidget(NoteDef note, int index) : base(note.Pos)
    {
        _note = note;
        _index = index;
        Glass.Padding = new Thickness(16, 12, 16, 14);

        var root = new DockPanel { Width = 260 };
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        _strip = new Border { Width = 34, Height = 6, CornerRadius = new CornerRadius(3), Cursor = Cursors.Hand, ToolTip = "Сменить цвет", VerticalAlignment = VerticalAlignment.Center };
        _strip.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            int i = Array.IndexOf(NoteColors, _note.Color.ToUpperInvariant());
            _note.Color = NoteColors[(i + 1) % NoteColors.Length];
            Paint();
            SettingsStore.Save();
        };
        var close = new TextBlock
        {
            Text = "✕", FontSize = 12, Foreground = UiKit.WhiteDim, Cursor = Cursors.Hand, ToolTip = "Удалить заметку",
            VerticalAlignment = VerticalAlignment.Center
        };
        close.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (!string.IsNullOrWhiteSpace(_box?.Text) &&
                MessageBox.Show("Удалить эту заметку?", "Wallflow", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            App.Me.Widgets.RemoveNote(_note);
        };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(_strip);
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        _box = new TextBox
        {
            Text = note.Text, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            Foreground = UiKit.White, CaretBrush = UiKit.White, SelectionBrush = UiKit.Hex("#7C5CFF"),
            FontFamily = UiKit.SysFont("Segoe UI Variable Text, Segoe UI"), FontSize = 15,
            MinHeight = 120, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _box.Style = null;
        _box.TextChanged += (_, _) => { _save.Stop(); _save.Start(); };
        _save.Tick += (_, _) => { _save.Stop(); _note.Text = _box.Text; SettingsStore.Save(); };
        if (string.IsNullOrEmpty(note.Text))
            Loaded += (_, _) => { Activate(); _box.Focus(); };
        root.Children.Add(_box);
        Glass.Child = root;
        Paint();
    }

    void Paint() => _strip.Background = UiKit.Hex(_note.Color);

    protected override Point DefaultPosition(Rect wa, Size s) =>
        // to the left of the weather/calendar column, cascading
        new(wa.Right - 420 - s.Width - 30 - _index * 30, wa.Top + 60 + _index * 40);

    protected override void OnClosed(EventArgs e)
    {
        if (_save.IsEnabled) { _save.Stop(); _note.Text = _box.Text; SettingsStore.Save(); }
        base.OnClosed(e);
    }
}
