using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>iOS-style folder: glass tile with a 3×3 preview of the apps inside, name underneath.</summary>
public sealed class FolderWidget : GlassWindow
{
    public FolderDef Folder { get; }

    public FolderWidget(FolderDef folder, int index) : base(folder.Pos)
    {
        Folder = folder;
        _index = index;
        AllowDrop = true;
        Glass.Padding = new Thickness(14, 14, 14, 10);
        Build();

        MouseLeftButtonUp += (_, e) => { if (!EditMode) { e.Handled = true; Open(); } };
        // "open on hover" mode
        _hover.Tick += (_, _) => { _hover.Stop(); if (IsMouseOver && !EditMode) Open(hover: true); };
        MouseEnter += (_, _) => { if (SettingsStore.Current.Widgets.FolderOpenMode == "hover" && !EditMode) _hover.Start(); };
        MouseLeave += (_, _) => _hover.Stop();
        Drop += OnDrop;
        ContextMenu = BuildMenu();
    }

    readonly int _index;
    readonly System.Windows.Threading.DispatcherTimer _hover = new() { Interval = TimeSpan.FromMilliseconds(110) };
    FolderPopup? _popup;

    protected override Point DefaultPosition(Rect wa, Size s)
    {
        // grid of folders under the clock, leaving the right side for weather/calendar
        double gap = 22, left = wa.Left + 70, top = wa.Top + Math.Min(340, wa.Height * 0.36);
        int perRow = Math.Max(2, (int)((wa.Width - 70 - 420) / (s.Width + gap)));
        perRow = Math.Min(perRow, 6);
        double x = left + (_index % perRow) * (s.Width + gap);
        double y = top + (_index / perRow) * (s.Height + gap);
        return new Point(x, y);
    }

    void Build()
    {
        var root = new StackPanel { Width = 116 };
        var grid = new UniformGrid { Columns = 3, Rows = 3, Width = 116, Height = 116 };
        foreach (var path in Folder.Items.Take(9))
        {
            var img = new Image { Source = ShellIcon.Get(path, 64), Width = 30, Height = 30, Margin = new Thickness(4) };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            grid.Children.Add(img);
        }
        root.Children.Add(grid);
        var name = UiKit.Text(Folder.Name, UiKit.SysFont("Segoe UI Variable Text, Segoe UI"), 13.5, UiKit.White, FontWeights.SemiBold);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.Margin = new Thickness(0, 6, 0, 0);
        root.Children.Add(name);
        var count = UiKit.Text(Plural(Folder.Items.Count), UiKit.SysFont("Segoe UI"), 11, UiKit.WhiteDim);
        root.Children.Add(count);
        Glass.Child = root;
        ToolTip = string.Join("\n", Folder.Items.Take(15).Select(Shortcuts.DisplayName));
    }

    static string Plural(int n)
    {
        int m10 = n % 10, m100 = n % 100;
        var w = m10 == 1 && m100 != 11 ? "ярлык" : m10 is >= 2 and <= 4 && (m100 < 12 || m100 > 14) ? "ярлыка" : "ярлыков";
        return $"{n} {w}";
    }

    public void Open(bool hover = false)
    {
        if (Folder.Items.Count == 0 || _popup != null) return;
        _popup = new FolderPopup(Folder, this, hover);
        _popup.Closed += (_, _) => _popup = null;
        _popup.Show();
    }

    void OnDrop(object s, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files)
        {
            foreach (var other in SettingsStore.Current.Folders) other.Items.RemoveAll(x => x.Equals(f, StringComparison.OrdinalIgnoreCase));
            Folder.Items.Add(f);
        }
        SettingsStore.Save();
        App.Me.Widgets.RebuildFolders();
    }

    ContextMenu BuildMenu()
    {
        var m = new ContextMenu();
        var open = new MenuItem { Header = "Открыть" };
        open.Click += (_, _) => Open();
        var rename = new MenuItem { Header = "Переименовать…" };
        rename.Click += (_, _) =>
        {
            var name = Prompt.Ask("Название папки", Folder.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            Folder.Name = name.Trim();
            SettingsStore.Save();
            App.Me.Widgets.RebuildFolders();
        };
        var edit = new MenuItem { Header = "Переставить виджеты" };
        edit.Click += (_, _) => App.Me.Widgets.SetEditMode(true);
        var settings = new MenuItem { Header = "Настройки папок…" };
        settings.Click += (_, _) => App.Me.OpenSettings("desktop");
        m.Items.Add(open);
        m.Items.Add(rename);
        m.Items.Add(new Separator());
        m.Items.Add(edit);
        m.Items.Add(settings);
        return m;
    }
}

/// <summary>The opened folder: large glass panel with app icons, closes when you click elsewhere.</summary>
public sealed class FolderPopup : Window
{
    readonly FolderDef _folder;
    readonly FrameworkElement _anchor;
    readonly Border _glass;

    readonly bool _hoverMode;
    readonly System.Windows.Threading.DispatcherTimer _leave = new() { Interval = TimeSpan.FromMilliseconds(140) };

    public FolderPopup(FolderDef folder, FrameworkElement anchor, bool hoverMode = false)
    {
        _hoverMode = hoverMode;
        _folder = folder;
        _anchor = anchor;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;

        var w = SettingsStore.Current.Widgets;
        _glass = new Border
        {
            CornerRadius = new CornerRadius(30),
            Padding = new Thickness(22, 18, 22, 22),
            Background = w.Tint == "light" ? UiKit.Brush(150, 255, 255, 255) : UiKit.Brush(160, 14, 16, 26),
            BorderBrush = UiKit.Brush(60, 255, 255, 255),
            BorderThickness = new Thickness(1),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.92, 0.92),
            Opacity = 0
        };
        Content = _glass;
        Build();

        Deactivated += (_, _) => SafeClose();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) SafeClose(); };
        Loaded += (_, _) => { Place(); Frost(); Animate(); Activate(); };
        if (hoverMode)
        {
            // close shortly after the mouse leaves both the popup and the folder tile
            _leave.Tick += (_, _) =>
            {
                if (_menuOpen || IsMouseOver || _anchor.IsMouseOver) return;
                _leave.Stop();
                SafeClose();
            };
            MouseLeave += (_, _) => _leave.Start();
            MouseEnter += (_, _) => { _leave.Stop(); Reopen(); };
            Loaded += (_, _) => _leave.Start();
            Closed += (_, _) => _leave.Stop();
        }
    }

    /// <summary>Same frosted look as the widgets: blurred wallpaper under a tint.</summary>
    void Frost()
    {
        var w = SettingsStore.Current.Widgets;
        if (w.GlassMode != "frost") return;
        var frost = Backdrop.BrushFor(this);
        if (frost is null) return;
        // outer layer = wallpaper blur, inner layer = tint
        var inner = new Border { CornerRadius = new CornerRadius(29), Background = w.Tint == "light" ? UiKit.Brush(110, 255, 255, 255) : UiKit.Brush(120, 10, 10, 18) };
        var content = _glass.Child;
        _glass.Child = null;
        inner.Child = content;
        inner.Padding = _glass.Padding;
        _glass.Padding = new Thickness(0);
        _glass.Background = frost;
        _glass.Child = inner;
    }

    bool _closing, _closed;

    /// <summary>Fade + shrink out, then close. Coming back with the mouse during the fade cancels it.</summary>
    void SafeClose()
    {
        if (_closing || _closed) return;
        _closing = true;
        var d = new Duration(TimeSpan.FromMilliseconds(130));
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(0, d) { EasingFunction = ease };
        fade.Completed += (_, _) => { if (_closing && !_closed) { _closed = true; Close(); } };
        _glass.BeginAnimation(OpacityProperty, fade);
        var st = (ScaleTransform)_glass.RenderTransform;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, d) { EasingFunction = ease });
        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, d) { EasingFunction = ease });
    }

    void Reopen()
    {
        if (!_closing || _closed) return;
        _closing = false;
        Animate();
    }

    void Build()
    {
        var root = new StackPanel();
        var title = UiKit.Text(_folder.Name, UiKit.SysFont("Segoe UI Variable Display, Segoe UI"), 26, UiKit.White, FontWeights.SemiBold, HorizontalAlignment.Left);
        title.Margin = new Thickness(6, 0, 0, 14);
        root.Children.Add(title);

        int cols = Math.Clamp(_folder.Items.Count, 3, 5);
        var wrap = new WrapPanel { Width = cols * 98 };
        foreach (var path in _folder.Items.ToList())
            wrap.Children.Add(Item(path));
        root.Children.Add(new ScrollViewer
        {
            Content = wrap, MaxHeight = 470,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        _glass.Child = root;
    }

    FrameworkElement Item(string path)
    {
        var sp = new StackPanel { Width = 92, Margin = new Thickness(3, 3, 3, 8) };
        var img = new Image { Source = ShellIcon.Get(path, 96), Width = 54, Height = 54, Margin = new Thickness(0, 6, 0, 6) };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        sp.Children.Add(img);
        var t = UiKit.Text(Shortcuts.DisplayName(path), UiKit.SysFont("Segoe UI Variable Text, Segoe UI"), 12, UiKit.White);
        t.TextWrapping = TextWrapping.Wrap;
        t.TextTrimming = TextTrimming.CharacterEllipsis;
        t.MaxHeight = 34;
        sp.Children.Add(t);

        var b = new Border { Child = sp, CornerRadius = new CornerRadius(14), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = Shortcuts.DisplayName(path) };
        b.MouseEnter += (_, _) => b.Background = UiKit.Brush(35, 255, 255, 255);
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (_, _) =>
        {
            SafeClose();
            try { Native.Launch(path); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Не удалось запустить", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        b.ContextMenu = ItemMenu(path);
        return b;
    }

    ContextMenu ItemMenu(string path)
    {
        var m = new ContextMenu();
        var loc = new MenuItem { Header = "Открыть расположение" };
        loc.Click += (_, _) =>
        {
            var target = Shortcuts.ResolveTarget(path);
            var p = target is not null && File.Exists(target) ? target : path;
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{p}\"");
        };
        m.Items.Add(loc);

        var move = new MenuItem { Header = "Переместить в" };
        foreach (var f in SettingsStore.Current.Folders.Where(f => f != _folder))
        {
            var mi = new MenuItem { Header = f.Name };
            var target = f;
            mi.Click += (_, _) => { _folder.Items.Remove(path); target.Items.Add(path); Changed(); };
            move.Items.Add(mi);
        }
        var newFolder = new MenuItem { Header = "Новая папка…" };
        newFolder.Click += (_, _) =>
        {
            var name = Prompt.Ask("Название новой папки", "Новая папка");
            if (string.IsNullOrWhiteSpace(name)) return;
            _folder.Items.Remove(path);
            SettingsStore.Current.Folders.Add(new FolderDef { Name = name.Trim(), Items = { path } });
            Changed();
        };
        move.Items.Add(new Separator());
        move.Items.Add(newFolder);
        m.Items.Add(move);

        var remove = new MenuItem { Header = "Убрать из папки (ярлык останется на диске)" };
        remove.Click += (_, _) => { _folder.Items.Remove(path); App.Me.Widgets.Ignore(path); Changed(); };
        m.Items.Add(remove);
        // keep the popup open while the menu is shown
        m.Opened += (_, _) => _menuOpen = true;
        m.Closed += (_, _) => _menuOpen = false;
        return m;
    }

    bool _menuOpen;
    protected override void OnDeactivated(EventArgs e)
    {
        if (_menuOpen) return;
        base.OnDeactivated(e);
    }

    void Changed()
    {
        SettingsStore.Save();
        SafeClose();
        App.Me.Widgets.RebuildFolders();
    }

    void Place()
    {
        var wa = SystemParameters.WorkArea;
        var c = _anchor.PointToScreen(new Point(_anchor.ActualWidth / 2, _anchor.ActualHeight / 2));
        var dpi = VisualTreeHelper.GetDpi(_anchor);
        double cx = c.X / dpi.DpiScaleX, cy = c.Y / dpi.DpiScaleY;
        Left = Math.Clamp(cx - ActualWidth / 2, wa.Left + 10, wa.Right - ActualWidth - 10);
        Top = Math.Clamp(cy - ActualHeight / 2, wa.Top + 10, wa.Bottom - ActualHeight - 10);
    }

    void Animate()
    {
        var d = new Duration(TimeSpan.FromMilliseconds(170));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _glass.BeginAnimation(OpacityProperty, new DoubleAnimation(1, d) { EasingFunction = ease });
        var st = (ScaleTransform)_glass.RenderTransform;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, d) { EasingFunction = ease });
        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, d) { EasingFunction = ease });
    }
}
