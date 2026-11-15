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

    void EnsurePlaced()
    {
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
        if (!IsLoaded || _edit) return;
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
        Native.PinToDesktop(_hwnd, noActivate: true);
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
        else if (msg == WM_MOUSEACTIVATE && !_edit)
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

    void OnMouseDown(object s, MouseButtonEventArgs e)
    {
        if (!_edit) return;
        e.Handled = true;
        try { DragMove(); } catch { /* mouse released early */ }
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
