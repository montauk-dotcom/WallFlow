using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>
/// Base class for every desktop widget: borderless, transparent, glued to the desktop
/// (survives Win+D, never in Alt-Tab/taskbar, always under normal windows), optional frosted glass.
/// </summary>
public abstract class GlassWindow : Window
{
    protected readonly Border Glass = new();
    readonly Rectangle _editOutline;
    readonly Grid _root = new();
    readonly ScaleTransform _scale = new(1, 1);
    protected readonly WidgetPos Pos;
    IntPtr _hwnd;
    bool _edit;

    protected virtual bool HasGlass => true;
    protected virtual double Radius => 26;

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
        _root.Children.Add(Glass);
        _root.Children.Add(_editOutline);
        _root.LayoutTransform = _scale;
        Content = _root;

        if (!double.IsNaN(pos.Left)) { Left = pos.Left; Top = pos.Top; }
        else WindowStartupLocation = WindowStartupLocation.Manual;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => EnsurePlaced();
        SizeChanged += (_, _) => UpdateRegion();
        MouseLeftButtonDown += OnMouseDown;
        ApplyLook();
    }

    /// <summary>Where to put the widget the first time (DIPs, primary work area).</summary>
    protected abstract Point DefaultPosition(Rect workArea, Size size);

    void EnsurePlaced()
    {
        var wa = SystemParameters.WorkArea;
        if (double.IsNaN(Pos.Left))
        {
            var p = DefaultPosition(wa, new Size(ActualWidth, ActualHeight));
            Left = p.X; Top = p.Y;
            Pos.Left = Left; Pos.Top = Top;
            SettingsStore.Save();
        }
        // keep it on screen if monitors changed
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
        if (Left > vr - 40 || Top > vb - 40 || Left + ActualWidth < vl + 40 || Top + ActualHeight < vt + 40)
        {
            Left = wa.Left + 40; Top = wa.Top + 40;
        }
    }

    void OnSourceInitialized(object? s, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.PinToDesktop(_hwnd, noActivate: true);
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        ApplyBlur();
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
            return (IntPtr)3; // MA_NOACTIVATE: clicks work, window never jumps to the front
        }
        return IntPtr.Zero;
    }

    // ---------- look ----------
    public void ApplyLook()
    {
        var w = SettingsStore.Current.Widgets;
        _scale.ScaleX = _scale.ScaleY = Math.Clamp(w.Scale, 0.5, 2.0);
        if (HasGlass)
        {
            var a = (byte)Math.Round(Math.Clamp(w.Opacity, 0, 1) * 255);
            Glass.Background = w.Tint == "light" ? UiKit.Brush((byte)(a * 0.7), 255, 255, 255) : UiKit.Brush(a, 12, 14, 24);
            Glass.BorderBrush = UiKit.Brush(55, 255, 255, 255);
            Glass.BorderThickness = new Thickness(1);
            Glass.CornerRadius = new CornerRadius(Radius);
            _editOutline.RadiusX = _editOutline.RadiusY = Radius;
        }
        else
        {
            Glass.Background = Brushes.Transparent;
            Glass.BorderThickness = new Thickness(0);
            _editOutline.RadiusX = _editOutline.RadiusY = 12;
        }
        ApplyBlur();
    }

    bool BlurOn => HasGlass && SettingsStore.Current.Widgets.Blur;

    void ApplyBlur()
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.SetBlur(_hwnd, BlurOn);
        UpdateRegion();
    }

    void UpdateRegion()
    {
        if (_hwnd == IntPtr.Zero || !BlurOn) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var sc = _scale.ScaleX;
        Native.SetRoundRegion(_hwnd,
            (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY),
            (int)Math.Round(Radius * sc * dpi.DpiScaleX));
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
            if (!value && _hwnd != IntPtr.Zero) Native.SendToBottom(_hwnd);
        }
    }

    void OnMouseDown(object s, MouseButtonEventArgs e)
    {
        if (!_edit) return;
        e.Handled = true;
        try { DragMove(); } catch { /* mouse released early */ }
        Pos.Left = Left; Pos.Top = Top;
        SettingsStore.Save();
    }

    /// <summary>Called by the host every time something global changes (language, style…).</summary>
    public virtual void Refresh() { }
}
