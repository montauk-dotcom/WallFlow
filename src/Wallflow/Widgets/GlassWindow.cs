using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>
/// Base class for every desktop widget: borderless, transparent, glued to the desktop
/// (survives Win+D, never in Alt-Tab/taskbar, always under normal windows), frosted glass background.
/// </summary>
public abstract class GlassWindow : Window
{
    /// <summary>Inner border: derived widgets put their content and padding here.</summary>
    protected readonly Border Glass = new();
    readonly Border _frame = new();       // frosted wallpaper + outline
    readonly Rectangle _editOutline;
    readonly Grid _root = new();
    readonly ScaleTransform _scale = new(1, 1);
    readonly DispatcherTimer _frostTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(80) };
    protected readonly WidgetPos Pos;
    IntPtr _hwnd;
    bool _edit;

    protected virtual bool HasGlass => true;
    /// <summary>Widgets you type into (notes, to-do) must be able to take keyboard focus.</summary>
    protected virtual bool Activatable => false;
    protected double Radius => SettingsStore.Current.Widgets.Radius;

    protected GlassWindow(WidgetPos pos)
    {
        Pos = pos;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Topmost = false;
        UseLayoutRounding = true;
        Focusable = false;
        Title = "Wallflow widget";

        _editOutline = new Rectangle
        {
            Stroke = UiKit.Hex("#A08BFF"), StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 3 },
            Visibility = Visibility.Collapsed, IsHitTestVisible = false
        };
        _frame.Child = Glass;
        _root.Children.Add(_frame);
        _root.Children.Add(_editOutline);
        _root.LayoutTransform = _scale;
        Content = _root;

        if (!double.IsNaN(pos.Left)) { Left = pos.Left; Top = pos.Top; }

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { EnsurePlaced(); UpdateFrost(); };
        SizeChanged += (_, _) => { KeepOnScreen(); QueueFrost(); };
        LocationChanged += (_, _) => QueueFrost();
        MouseLeftButtonDown += OnMouseDown;
        _frostTimer.Tick += (_, _) => { _frostTimer.Stop(); UpdateFrost(); };
        Backdrop.Changed += OnBackdropChanged;
        ApplyLook();
    }

    void OnBackdropChanged() => QueueFrost();

    /// <summary>Where to put the widget the first time (DIPs, primary work area).</summary>
    protected abstract Point DefaultPosition(Rect workArea, Size size);

    public WidgetPos Position => Pos;

    /// <summary>Used by the host's automatic layout.</summary>
    public void PlaceAt(double x, double y)
    {
        Left = Math.Round(x); Top = Math.Round(y);
        Pos.Left = Left; Pos.Top = Top;
        UpdateFrost();
    }

    void EnsurePlaced()
    {
        if (double.IsNaN(Pos.Left) && App.Me.Widgets.LayoutPending)
        {
            // the host will place everything together once all widgets know their size
            Left = -30000; Top = -30000;
            return;
        }
        if (double.IsNaN(Pos.Left))
        {
            var p = DefaultPosition(SystemParameters.WorkArea, new Size(ActualWidth, ActualHeight));
            Left = p.X; Top = p.Y;
            Pos.Left = Left; Pos.Top = Top;
            SettingsStore.Save();
        }
        KeepOnScreen();
    }

    /// <summary>Never let a widget hang off the edge or hide under the taskbar.</summary>
    void KeepOnScreen()
    {
        if (!IsLoaded || _edit || App.Me.Widgets.LayoutPending) return;
        var wa = WorkArea();
        double l = Math.Max(wa.Left, Math.Min(Left, wa.Right - ActualWidth));
        double t = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - ActualHeight));
        if (Math.Abs(l - Left) > 0.5 || Math.Abs(t - Top) > 0.5)
        {
            Left = l; Top = t;
            Pos.Left = l; Pos.Top = t;
            SettingsStore.Save();
        }
    }

    /// <summary>Work area (without taskbar) of the monitor this widget is on, in DIPs.</summary>
    Rect WorkArea()
    {
        if (_hwnd == IntPtr.Zero) return SystemParameters.WorkArea;
        var scr = System.Windows.Forms.Screen.FromHandle(_hwnd).WorkingArea;
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is null) return SystemParameters.WorkArea;
        var m = src.CompositionTarget.TransformFromDevice;
        var tl = m.Transform(new Point(scr.Left, scr.Top));
        var br = m.Transform(new Point(scr.Right, scr.Bottom));
        return new Rect(tl, br);
    }

    void OnSourceInitialized(object? s, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // with a live wallpaper in "reliable" mode the widgets are owned by the video window, so they stay above it
        Native.PinToDesktop(_hwnd, noActivate: !Activatable, owner: App.Me.Live.OwnerHwnd);
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
    }

    // ---------- keep at the bottom of the Z-order ----------
    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        const int WM_WINDOWPOSCHANGING = 0x0046, WM_MOUSEACTIVATE = 0x0021;
        if (msg == WM_WINDOWPOSCHANGING && !_edit)
        {
            var wp = Marshal.PtrToStructure<WINDOWPOS>(l);
            if ((wp.flags & 0x0004 /*SWP_NOZORDER*/) == 0)
            {
                wp.hwndInsertAfter = (IntPtr)1; // HWND_BOTTOM
                Marshal.StructureToPtr(wp, l, false);
            }
        }
        else if (msg == WM_MOUSEACTIVATE && !_edit && !Activatable)
        {
            handled = true;
            return (IntPtr)3; // MA_NOACTIVATE
        }
        return IntPtr.Zero;
    }

    // ---------- look ----------
    public void ApplyLook()
    {
        var w = SettingsStore.Current.Widgets;
        double newScale = Math.Clamp(w.Scale, 0.5, 2.0);
        bool scaleChanged = Math.Abs(newScale - _scale.ScaleX) > 0.001;
        _scale.ScaleX = _scale.ScaleY = newScale;

        var r = new CornerRadius(Radius);
        _frame.CornerRadius = r;
        Glass.CornerRadius = new CornerRadius(Math.Max(0, Radius - 1));
        _editOutline.RadiusX = _editOutline.RadiusY = HasGlass ? Radius : 12;

        if (HasGlass && w.GlassMode != "clear")
        {
            var a = (byte)Math.Round(Math.Clamp(w.Opacity, 0, 0.95) * 255);
            // tint mode has no wallpaper underneath, so it needs a denser tint to stay readable
            if (w.GlassMode == "tint") a = (byte)Math.Min(255, a + 70);
            Glass.Background = w.Tint == "light" ? UiKit.Brush(a, 255, 255, 255) : UiKit.Brush(a, 10, 10, 18);
            _frame.BorderBrush = new LinearGradientBrush(Color.FromArgb(90, 255, 255, 255), Color.FromArgb(25, 255, 255, 255), 90);
            _frame.BorderThickness = new Thickness(1);
        }
        else if (HasGlass) // clear: outline only
        {
            Glass.Background = Brushes.Transparent;
            _frame.Background = Brushes.Transparent;
            _frame.BorderBrush = UiKit.Brush(70, 255, 255, 255);
            _frame.BorderThickness = new Thickness(1);
        }
        else
        {
            Glass.Background = Brushes.Transparent;
            _frame.Background = Brushes.Transparent;
            _frame.BorderThickness = new Thickness(0);
        }

        if (scaleChanged && IsLoaded)
        {
            // force the window to re-fit the scaled content
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;
        }
        QueueFrost();
    }

    void QueueFrost() { _frostTimer.Stop(); _frostTimer.Start(); }

    void UpdateFrost()
    {
        var w = SettingsStore.Current.Widgets;
        if (!HasGlass || w.GlassMode != "frost") { if (HasGlass && w.GlassMode == "tint") _frame.Background = Brushes.Transparent; return; }
        _frame.Background = Backdrop.BrushFor(_frame) ?? UiKit.Brush(120, 20, 20, 30);
    }

    // ---------- edit mode (drag to move) ----------
    public bool EditMode
    {
        get => _edit;
        set
        {
            _edit = value;
            _editOutline.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            Cursor = value ? Cursors.SizeAll : null;
            if (!value && _hwnd != IntPtr.Zero) { Native.SendToBottom(_hwnd); KeepOnScreen(); }
        }
    }

    // manual drag so we can snap: hold Shift to line up with the edges/centres of other widgets
    Point _dragStart;      // cursor, DIPs
    Point _winStart;       // window Left/Top
    bool _dragging;

    void OnMouseDown(object s, MouseButtonEventArgs e)
    {
        if (!_edit) return;
        e.Handled = true;
        _dragStart = CursorDip();
        _winStart = new Point(Left, Top);
        _dragging = true;
        CaptureMouse();
        MouseMove += OnDragMove;
        MouseLeftButtonUp += OnDragEnd;
        LostMouseCapture += OnLostCapture;
    }

    Point CursorDip()
    {
        var p = System.Windows.Forms.Cursor.Position;
        var src = PresentationSource.FromVisual(this);
        return src?.CompositionTarget is null ? new Point(p.X, p.Y) : src.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y));
    }

    void OnDragMove(object s, MouseEventArgs e)
    {
        if (!_dragging) return;
        var c = CursorDip();
        double x = _winStart.X + (c.X - _dragStart.X), y = _winStart.Y + (c.Y - _dragStart.Y);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) (x, y) = Snap(x, y);
        Left = x; Top = y;
    }

    (double, double) Snap(double x, double y)
    {
        const double T = 18, Gap = 20;
        double w = ActualWidth, h = ActualHeight;
        double bestDx = T + 1, bestDy = T + 1, sx = x, sy = y;
        void TryX(double cand) { var d = Math.Abs(cand - x); if (d < bestDx) { bestDx = d; sx = cand; } }
        void TryY(double cand) { var d = Math.Abs(cand - y); if (d < bestDy) { bestDy = d; sy = cand; } }
        foreach (var r in App.Me.Widgets.OtherRects(this))
        {
            // same left / right / centre, or side by side with a gap
            TryX(r.Left); TryX(r.Right - w); TryX(r.Left + (r.Width - w) / 2); TryX(r.Right + Gap); TryX(r.Left - w - Gap);
            TryY(r.Top); TryY(r.Bottom - h); TryY(r.Top + (r.Height - h) / 2); TryY(r.Bottom + Gap); TryY(r.Top - h - Gap);
        }
        var wa = WorkArea();
        TryX(wa.Left + 40); TryX(wa.Right - w - 40); TryX(wa.Left + (wa.Width - w) / 2);
        TryY(wa.Top + 40); TryY(wa.Bottom - h - 40);
        return (bestDx <= T ? sx : x, bestDy <= T ? sy : y);
    }

    void OnDragEnd(object s, MouseButtonEventArgs e) => EndDrag();
    void OnLostCapture(object s, MouseEventArgs e) => EndDrag();

    void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        MouseMove -= OnDragMove;
        MouseLeftButtonUp -= OnDragEnd;
        LostMouseCapture -= OnLostCapture;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Pos.Left = Left; Pos.Top = Top;
        SettingsStore.Save();
        UpdateFrost();
    }

    protected override void OnClosed(EventArgs e)
    {
        Backdrop.Changed -= OnBackdropChanged;
        _frostTimer.Stop();
        base.OnClosed(e);
    }

    /// <summary>Called by the host every time something global changes (language, style…).</summary>
    public virtual void Refresh() { }
}
