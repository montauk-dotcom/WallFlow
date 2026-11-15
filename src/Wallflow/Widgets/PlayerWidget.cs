using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wallflow.Core;
using GSMTC = global::Windows.Media.Control;

namespace Wallflow.Widgets;

/// <summary>
/// "Now playing" for whatever app produces sound and reports to Windows' media overlay
/// (Spotify, browsers incl. YouTube, Yandex Music, VLC, Telegram…). Event driven — no polling.
/// </summary>
public sealed class PlayerWidget : GlassWindow
{
    GSMTC.GlobalSystemMediaTransportControlsSessionManager? _mgr;
    GSMTC.GlobalSystemMediaTransportControlsSession? _session;
    readonly DispatcherTimer _tick = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };

    readonly Border _art = new() { Width = 72, Height = 72, CornerRadius = new CornerRadius(14) };
    readonly TextBlock _title, _artist, _app, _glyph;
    readonly Border _barFill = new() { Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Background = UiKit.White };
    readonly Grid _bar = new() { Height = 4, Margin = new Thickness(0, 12, 0, 4) };
    readonly TextBlock _pos, _len;
    TimeSpan _position, _end;
    readonly TextBlock _volIcon, _volText;
    readonly Grid _volBar = new() { Height = 16, Margin = new Thickness(10, 0, 8, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
    readonly Border _volFill = new() { Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Background = UiKit.White };
    float _volume = 1;
    bool _muted;
    string _appKey = "";
    bool _appVolume;
    DateTimeOffset _updated;
    bool _playing;

    static readonly FontFamily Icons = UiKit.SysFont("Segoe Fluent Icons, Segoe MDL2 Assets");

    public PlayerWidget() : base(SettingsStore.Current.Widgets.PlayerPos)
    {
        Glass.Padding = new Thickness(16);
        var f = UiKit.SysFont("Segoe UI Variable Text, Segoe UI");
        var root = new StackPanel { Width = 330 };

        var top = new DockPanel();
        _art.Background = UiKit.Brush(40, 255, 255, 255);
        _art.Child = new TextBlock { Text = "", FontFamily = Icons, FontSize = 28, Foreground = UiKit.WhiteDim, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(_art, Dock.Left);
        top.Children.Add(_art);
        var info = new StackPanel { Margin = new Thickness(14, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _app = UiKit.Text("", f, 11.5, UiKit.WhiteDim, FontWeights.SemiBold, HorizontalAlignment.Left);
        _title = UiKit.Text("Ничего не играет", f, 16, UiKit.White, FontWeights.SemiBold, HorizontalAlignment.Left);
        _artist = UiKit.Text("Включи музыку или видео", f, 13.5, UiKit.WhiteSoft, null, HorizontalAlignment.Left);
        foreach (var t in new[] { _app, _title, _artist }) { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextAlignment = TextAlignment.Left; }
        info.Children.Add(_app); info.Children.Add(_title); info.Children.Add(_artist);
        top.Children.Add(info);
        root.Children.Add(top);

        _bar.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = UiKit.Brush(50, 255, 255, 255) });
        _bar.Children.Add(_barFill);
        root.Children.Add(_bar);
        var times = new DockPanel();
        _pos = UiKit.Text("", f, 11, UiKit.WhiteDim, null, HorizontalAlignment.Left);
        _len = UiKit.Text("", f, 11, UiKit.WhiteDim, null, HorizontalAlignment.Right);
        DockPanel.SetDock(_len, Dock.Right);
        times.Children.Add(_len); times.Children.Add(_pos);
        root.Children.Add(times);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(Btn("", 18, async () => { if (_session != null) await _session.TrySkipPreviousAsync(); }));
        var play = Btn("", 26, async () => { if (_session != null) await _session.TryTogglePlayPauseAsync(); });
        _glyph = (TextBlock)((Border)play).Child;
        buttons.Children.Add(play);
        buttons.Children.Add(Btn("", 18, async () => { if (_session != null) await _session.TrySkipNextAsync(); }));
        root.Children.Add(buttons);

        // volume: the playing app's own volume (like the Volume Mixer), or the system volume
        var vol = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        _volIcon = new TextBlock { Text = "\uE767", FontFamily = Icons, FontSize = 15, Foreground = UiKit.WhiteSoft, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Без звука" };
        _volIcon.MouseLeftButtonUp += (_, e) => { e.Handled = true; ToggleMute(); };
        DockPanel.SetDock(_volIcon, Dock.Left);
        vol.Children.Add(_volIcon);
        _volText = UiKit.Text("", f, 11, UiKit.WhiteDim, null, HorizontalAlignment.Right);
        _volText.Width = 70; _volText.TextAlignment = TextAlignment.Right; _volText.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_volText, Dock.Right);
        vol.Children.Add(_volText);
        _volBar.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = UiKit.Brush(50, 255, 255, 255), Height = 4, VerticalAlignment = VerticalAlignment.Center });
        _volBar.Children.Add(_volFill);
        _volBar.MouseLeftButtonDown += (_, e) => { e.Handled = true; _volBar.CaptureMouse(); VolumeAt(e.GetPosition(_volBar).X); };
        _volBar.MouseMove += (_, e) => { if (_volBar.IsMouseCaptured) VolumeAt(e.GetPosition(_volBar).X); };
        _volBar.MouseLeftButtonUp += (_, _) => _volBar.ReleaseMouseCapture();
        vol.Children.Add(_volBar);
        root.Children.Add(vol);
        MouseWheel += (_, e) => { e.Handled = true; SetVolume(_volume + (e.Delta > 0 ? 0.05f : -0.05f)); };
        MouseEnter += (_, _) => ReadVolume();

        Glass.Child = root;
        _tick.Tick += (_, _) => UpdateProgress();
        Loaded += async (_, _) => { ReadVolume(); await Init(); };
    }

    FrameworkElement Btn(string glyph, double size, Func<Task> click)
    {
        var t = new TextBlock { Text = glyph, FontFamily = Icons, FontSize = size, Foreground = UiKit.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var b = new Border { Width = 48, Height = 40, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, Cursor = Cursors.Hand, Child = t, Margin = new Thickness(6, 0, 6, 0) };
        b.MouseEnter += (_, _) => b.Background = UiKit.Brush(30, 255, 255, 255);
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += async (_, e) => { e.Handled = true; try { await click(); } catch { } };
        return b;
    }

    protected override Point DefaultPosition(Rect wa, Size s) => new(wa.Right - s.Width - 70, wa.Bottom - s.Height - 40);

    async Task Init()
    {
        try
        {
            _mgr = await GSMTC.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _mgr.CurrentSessionChanged += (_, _) => Dispatcher.BeginInvoke(Attach);
            _mgr.SessionsChanged += (_, _) => Dispatcher.BeginInvoke(Attach);
            Attach();
        }
        catch (Exception ex)
        {
            App.Log(ex);
            _artist.Text = "Медиа-управление Windows недоступно";
        }
    }

    void Attach()
    {
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMedia;
            _session.PlaybackInfoChanged -= OnPlayback;
            _session.TimelinePropertiesChanged -= OnTimeline;
        }
        _session = _mgr?.GetCurrentSession();
        if (_session == null) { ShowIdle(); return; }
        _session.MediaPropertiesChanged += OnMedia;
        _session.PlaybackInfoChanged += OnPlayback;
        _session.TimelinePropertiesChanged += OnTimeline;
        _app.Text = AppName(_session.SourceAppUserModelId);
        _appKey = AudioControl.AppKey(_session.SourceAppUserModelId);
        ReadVolume();
        _ = LoadMedia();
        LoadPlayback();
        LoadTimeline();
    }

    void OnMedia(GSMTC.GlobalSystemMediaTransportControlsSession s, GSMTC.MediaPropertiesChangedEventArgs e) => Dispatcher.BeginInvoke(() => _ = LoadMedia());
    void OnPlayback(GSMTC.GlobalSystemMediaTransportControlsSession s, GSMTC.PlaybackInfoChangedEventArgs e) => Dispatcher.BeginInvoke(LoadPlayback);
    void OnTimeline(GSMTC.GlobalSystemMediaTransportControlsSession s, GSMTC.TimelinePropertiesChangedEventArgs e) => Dispatcher.BeginInvoke(LoadTimeline);

    void ShowIdle()
    {
        _app.Text = "";
        _title.Text = "Ничего не играет";
        _artist.Text = "Включи музыку или видео";
        _art.Background = UiKit.Brush(40, 255, 255, 255);
        _art.Child ??= new TextBlock { Text = "", FontFamily = Icons, FontSize = 28, Foreground = UiKit.WhiteDim, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _playing = false; _end = TimeSpan.Zero;
        _glyph.Text = "";
        _tick.Stop();
        UpdateProgress();
    }

    async Task LoadMedia()
    {
        var s = _session;
        if (s == null) return;
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            if (s != _session) return;
            _title.Text = string.IsNullOrWhiteSpace(p.Title) ? "Без названия" : p.Title;
            _artist.Text = !string.IsNullOrWhiteSpace(p.Artist) ? p.Artist : p.AlbumTitle ?? "";
            if (p.Thumbnail != null)
            {
                using var ras = await p.Thumbnail.OpenReadAsync();
                using var src = ras.AsStreamForRead();
                var ms = new MemoryStream();
                await src.CopyToAsync(ms);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.DecodePixelWidth = 160;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                if (s != _session) return;
                _art.Child = null;
                _art.Background = new ImageBrush(bi) { Stretch = Stretch.UniformToFill };
            }
            else
            {
                _art.Background = UiKit.Brush(40, 255, 255, 255);
            }
        }
        catch (Exception ex) { App.Log(ex); }
    }

    void LoadPlayback()
    {
        if (_session == null) return;
        var info = _session.GetPlaybackInfo();
        _playing = info.PlaybackStatus == GSMTC.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        _glyph.Text = _playing ? "" : "";
        if (_playing && _end > TimeSpan.Zero) _tick.Start(); else _tick.Stop();
        UpdateProgress();
    }

    void LoadTimeline()
    {
        if (_session == null) return;
        var t = _session.GetTimelineProperties();
        _position = t.Position - t.StartTime;
        _end = t.EndTime - t.StartTime;
        _updated = t.LastUpdatedTime;
        if (_playing && _end > TimeSpan.Zero) _tick.Start();
        UpdateProgress();
    }

    void UpdateProgress()
    {
        bool has = _end > TimeSpan.FromSeconds(1);
        _bar.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _pos.Visibility = _len.Visibility = _bar.Visibility;
        if (!has) return;
        var pos = _position;
        if (_playing && _updated != default) pos += DateTimeOffset.Now - _updated;
        if (pos > _end) pos = _end;
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        double w = _bar.ActualWidth > 0 ? _bar.ActualWidth : 330;
        _barFill.Width = Math.Max(4, w * pos.TotalSeconds / _end.TotalSeconds);
        _pos.Text = Fmt(pos);
        _len.Text = Fmt(_end);
    }

    // ---------------- volume ----------------
    void ReadVolume()
    {
        var app = _session != null ? AudioControl.GetApp(_appKey) : null;
        _appVolume = app != null;
        var v = app ?? AudioControl.GetSystem();
        if (v is not { } x) return;
        _volume = x.volume; _muted = x.mute;
        PaintVolume();
    }

    void VolumeAt(double x)
    {
        double w = _volBar.ActualWidth > 0 ? _volBar.ActualWidth : 200;
        SetVolume((float)Math.Clamp(x / w, 0, 1));
    }

    void SetVolume(float v)
    {
        _volume = Math.Clamp(v, 0, 1);
        if (!(_appVolume && AudioControl.SetApp(_appKey, _volume)))
        {
            _appVolume = false;
            AudioControl.SetSystem(_volume);
        }
        if (_muted && _volume > 0) ToggleMute();
        PaintVolume();
    }

    void ToggleMute()
    {
        _muted = !_muted;
        if (!(_appVolume && AudioControl.SetAppMute(_appKey, _muted))) AudioControl.SetSystemMute(_muted);
        PaintVolume();
    }

    void PaintVolume()
    {
        double w = _volBar.ActualWidth > 0 ? _volBar.ActualWidth : 200;
        _volFill.Width = Math.Max(4, w * (_muted ? 0 : _volume));
        _volIcon.Text = _muted || _volume < 0.01 ? "\uE74F" : _volume < 0.34 ? "\uE993" : _volume < 0.67 ? "\uE994" : "\uE995";
        _volText.Text = $"{(_appVolume ? "звук" : "система")} {Math.Round(_volume * 100)}%";
    }

    static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    static string AppName(string aumid)
    {
        var a = aumid ?? "";
        var low = a.ToLowerInvariant();
        if (low.Contains("spotify")) return "SPOTIFY";
        if (low.Contains("chrome")) return "CHROME";
        if (low.Contains("msedge") || low.Contains("edge")) return "EDGE";
        if (low.Contains("firefox") || a == "308046B0AF4A39CB") return "FIREFOX";
        if (low.Contains("opera")) return "OPERA";
        if (low.Contains("yandex")) return "YANDEX";
        if (low.Contains("telegram")) return "TELEGRAM";
        if (low.Contains("zunemusic")) return "МЕДИАПЛЕЕР";
        if (low.Contains("vlc")) return "VLC";
        var name = a.Split('!')[0].Split('_')[0];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        var dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1) name = name[(dot + 1)..];
        return name.ToUpperInvariant();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tick.Stop();
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMedia;
            _session.PlaybackInfoChanged -= OnPlayback;
            _session.TimelinePropertiesChanged -= OnTimeline;
        }
        base.OnClosed(e);
    }
}
