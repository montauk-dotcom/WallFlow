using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Views;

public partial class GalleryWindow : Window
{
    static readonly (string res, string label)[] Resolutions =
    {
        ("1280x720", "HD"), ("1920x1080", "Full HD"), ("1920x1200", "WUXGA"), ("2560x1080", "UW-FHD"),
        ("2560x1440", "2K"), ("2560x1600", "WQXGA"), ("3440x1440", "UW-QHD"), ("3840x2160", "4K"),
        ("5120x1440", "DQHD"), ("5120x2880", "5K"), ("7680x4320", "8K")
    };
    static readonly (string r, string label)[] Ratios =
    {
        ("landscape", "Альбомные"), ("portrait", "Портретные"), ("16x9", "16:9"), ("16x10", "16:10"), ("21x9", "21:9"),
        ("32x9", "32:9"), ("4x3", "4:3"), ("5x4", "5:4"), ("9x16", "9:16"), ("10x16", "10:16"), ("1x1", "1:1")
    };
    static readonly string[] Colors =
    {
        "660000", "990000", "cc0000", "cc3333", "ea4c88", "993399", "663399", "333399", "0066cc", "0099cc",
        "66cccc", "77cc33", "669900", "336600", "666600", "999900", "cccc33", "ffff00", "ffcc33", "ff9900",
        "ff6600", "cc6633", "996633", "663300", "000000", "999999", "cccccc", "ffffff", "424153"
    };

    SearchFilters _f;
    string _view = "browse";
    int _page = 1, _lastPage = 1;
    string? _seed;
    bool _loading;
    CancellationTokenSource _cts = new();
    Wallpaper? _current;
    readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3.2) };

    static AppSettings S => SettingsStore.Current;

    public GalleryWindow()
    {
        InitializeComponent();
        UiKit.DarkTitleBar(this);
        _f = S.LastFilters.Clone();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Toast.Visibility = Visibility.Collapsed; };
        RestoreFilters();
        UpdateFavCount();
        Loaded += async (_, _) => await LoadPage(true);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && Preview.Visibility == Visibility.Visible) { HidePreview(); e.Handled = true; } };
        InitLive();
        Closed += (_, _) =>
        {
            PVideo.Stop(); PVideo.Close();
            _cts.Cancel();
            Cards.Items.Clear();
            PImg.Source = null;
        };
    }

    // ---------------- filters ----------------
    void RestoreFilters()
    {
        Query.Text = _f.Query;
        ModeAtLeast.IsChecked = _f.ResolutionMode != "exact";
        ModeExact.IsChecked = _f.ResolutionMode == "exact";
        CatGeneral.IsChecked = _f.General; CatAnime.IsChecked = _f.Anime; CatPeople.IsChecked = _f.People;
        Select(Sorting, _f.Sorting);
        Select(TopRange, _f.TopRange);
        TopRange.Visibility = _f.Sorting == "toplist" ? Visibility.Visible : Visibility.Collapsed;
        BuildResChips(); BuildRatioChips(); BuildColors();
    }

    static void Select(ComboBox cb, string tag)
    {
        foreach (ComboBoxItem i in cb.Items) if ((string)i.Tag == tag) { cb.SelectedItem = i; return; }
    }

    static string TagOf(ComboBox cb) => (string)((ComboBoxItem)cb.SelectedItem).Tag;

    ToggleButton Chip(string content, bool on, Action click)
    {
        var b = new ToggleButton { Content = content, IsChecked = on, Style = (Style)FindResource("Chip") };
        b.Click += (_, _) => click();
        return b;
    }

    void BuildResChips()
    {
        ResChips.Children.Clear();
        bool exact = _f.ResolutionMode == "exact";
        if (!exact) ResChips.Children.Add(Chip("Любое", string.IsNullOrEmpty(_f.AtLeast), () => { _f.AtLeast = ""; BuildResChips(); }));
        foreach (var (res, label) in Resolutions)
        {
            bool on = exact ? _f.Resolutions.Contains(res) : _f.AtLeast == res;
            var r = res;
            ResChips.Children.Add(Chip($"{label}  {res.Replace('x', '×')}", on, () =>
            {
                if (exact) { if (!_f.Resolutions.Remove(r)) _f.Resolutions.Add(r); }
                else _f.AtLeast = _f.AtLeast == r ? "" : r;
                BuildResChips();
            }));
        }
        if (!exact && !string.IsNullOrEmpty(_f.AtLeast) && Resolutions.All(x => x.res != _f.AtLeast))
            ResChips.Children.Add(Chip($"Мой экран  {_f.AtLeast.Replace('x', '×')}", true, () => { _f.AtLeast = ""; BuildResChips(); }));
    }

    void BuildRatioChips()
    {
        RatioChips.Children.Clear();
        foreach (var (r, label) in Ratios)
        {
            var rr = r;
            RatioChips.Children.Add(Chip(label, _f.Ratios.Contains(r), () => { if (!_f.Ratios.Remove(rr)) _f.Ratios.Add(rr); BuildRatioChips(); }));
        }
    }

    void BuildColors()
    {
        ColorChips.Children.Clear();
        foreach (var c in Colors)
        {
            var cc = c;
            var b = new Border
            {
                Width = 26, Height = 26, Margin = new Thickness(0, 0, 5, 5), CornerRadius = new CornerRadius(6),
                Background = UiKit.Hex(c), Cursor = Cursors.Hand, ToolTip = "#" + c,
                BorderThickness = new Thickness(_f.Color == c ? 3 : 1),
                BorderBrush = _f.Color == c ? Brushes.White : UiKit.Brush(50, 255, 255, 255)
            };
            b.MouseLeftButtonUp += (_, _) => { _f.Color = _f.Color == cc ? "" : cc; BuildColors(); };
            ColorChips.Children.Add(b);
        }
    }

    void Mode_Checked(object s, RoutedEventArgs e)
    {
        if (_f is null) return;
        _f.ResolutionMode = ModeExact.IsChecked == true ? "exact" : "atleast";
        BuildResChips();
    }

    void Sorting_Changed(object s, SelectionChangedEventArgs e)
    {
        if (TopRange is null || Sorting.SelectedItem is null) return;
        TopRange.Visibility = TagOf(Sorting) == "toplist" ? Visibility.Visible : Visibility.Collapsed;
    }

    void MyScreen_Click(object s, RoutedEventArgs e)
    {
        var src = PresentationSource.FromVisual(this);
        double sx = src?.CompositionTarget.TransformToDevice.M11 ?? 1, sy = src?.CompositionTarget.TransformToDevice.M22 ?? 1;
        int w = (int)Math.Round(SystemParameters.PrimaryScreenWidth * sx), h = (int)Math.Round(SystemParameters.PrimaryScreenHeight * sy);
        _f.ResolutionMode = "atleast";
        ModeAtLeast.IsChecked = true;
        _f.AtLeast = $"{w}x{h}";
        BuildResChips();
        ShowToast($"Ищу не меньше {w}×{h} — как твой экран");
    }

    SearchFilters Collect()
    {
        _f.Query = Query.Text.Trim();
        _f.General = CatGeneral.IsChecked == true;
        _f.Anime = CatAnime.IsChecked == true;
        _f.People = CatPeople.IsChecked == true;
        _f.Sorting = TagOf(Sorting);
        _f.TopRange = TagOf(TopRange);
        return _f;
    }

    async void Apply_Click(object s, RoutedEventArgs e)
    {
        if (_view == "live") { await LoadPage(true); return; }
        if (TabBrowse.IsChecked != true) { TabBrowse.IsChecked = true; return; } // Tab_Checked reloads
        await LoadPage(true);
    }

    void Query_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Apply_Click(s, e);
    }

    async void Reset_Click(object s, RoutedEventArgs e)
    {
        _f = new SearchFilters();
        RestoreFilters();
        await LoadPage(true);
    }

    void Settings_Click(object s, RoutedEventArgs e) => App.Me.OpenSettings("wallpapers");

    async void Tab_Checked(object s, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _view = s == TabBing ? "bing" : s == TabFav ? "favorites" : s == TabLive ? "live" : "browse";
        FiltersPanel.IsEnabled = _view == "browse";
        FiltersPanel.Opacity = _view == "browse" ? 1 : 0.45;
        FiltersPanel.Visibility = _view == "live" ? Visibility.Collapsed : Visibility.Visible;
        LiveFiltersPanel.Visibility = _view == "live" ? Visibility.Visible : Visibility.Collapsed;
        Query.ToolTip = _view == "live" ? "Что ищем: ocean, rain, city night, nebula…" : "Слова, #тег, like:id (похожие)";
        if (_view == "live") UpdateLiveNow();
        await LoadPage(true);
    }

    // ---------------- loading ----------------
    async Task LoadPage(bool reset)
    {
        if (reset)
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _page = 1; _seed = null; _loading = false;
            Cards.Items.Clear();
            Scroller.ScrollToTop();
        }
        else
        {
            if (_loading || _page >= _lastPage || (_view != "browse" && _view != "live")) return;
            _page++;
        }
        var ct = _cts.Token;
        _loading = true;
        Status.Text = "Загрузка…";
        try
        {
            if (_view == "live")
            {
                var lf = CollectLive();
                var r = await LiveApi.Search(lf, _page, ct);
                if (ct.IsCancellationRequested) return;
                _lastPage = r.HasMore ? _page + 1 : _page;
                foreach (var w in r.Items) Cards.Items.Add(LiveCard(w));
                Status.Text = Cards.Items.Count > 0
                    ? $"Живые обои — {LiveApi.Sources.First(x => x.Key == lf.Source).Title}: найдено {r.Total:N0}"
                    : "Ничего не найдено — попробуй другие слова или меньшее разрешение.";
            }
            else if (_view == "favorites")
            {
                AddCards(S.Favorites);
                _lastPage = 1;
                Status.Text = S.Favorites.Count > 0 ? $"В избранном: {S.Favorites.Count}" : "Пока пусто — жми ☆ на обоях.";
            }
            else if (_view == "bing")
            {
                var list = await BingApi.Daily(ct: ct);
                if (ct.IsCancellationRequested) return;
                AddCards(list);
                _lastPage = 1;
                Status.Text = $"Bing — обои дня за последние {list.Count} дней, 4K";
            }
            else
            {
                var f = Collect();
                S.LastFilters = f.Clone();
                SettingsStore.Save();
                var r = await WallhavenApi.Search(f, _page, _seed, ct);
                if (ct.IsCancellationRequested) return;
                _lastPage = r.LastPage; _seed = r.Seed;
                AddCards(r.Items);
                Status.Text = r.Total > 0
                    ? $"Найдено: {r.Total:N0} · страница {r.Page} из {r.LastPage}"
                    : "Ничего не найдено — попробуй ослабить фильтры.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!reset) _page--;
            Status.Text = "Ошибка: " + ex.Message;
        }
        finally { if (!ct.IsCancellationRequested) _loading = false; }
    }

    async void Scroller_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        if (Scroller.VerticalOffset + Scroller.ViewportHeight > Scroller.ExtentHeight - 700)
            await LoadPage(false);
    }

    void AddCards(IEnumerable<Wallpaper> items)
    {
        foreach (var w in items) Cards.Items.Add(Card(w));
    }

    FrameworkElement Card(Wallpaper w)
    {
        var img = new Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        try
        {
            var bi = UiKit.WebImage(w.Thumb, 380);
            img.Source = bi;
            if (bi.IsDownloading) bi.DownloadCompleted += (_, _) => img.Opacity = 1; else img.Opacity = 1;
            bi.DownloadFailed += (_, _) => img.Opacity = 0.2;
        }
        catch { }

        var badge = new Border
        {
            Background = UiKit.Brush(170, 0, 0, 0), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8),
            Child = new TextBlock { Text = w.Badge, FontSize = 11, FontWeight = FontWeights.SemiBold }
        };

        var fav = new Button { Content = IsFav(w) ? "★" : "☆", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0), ToolTip = "Избранное" };
        fav.Click += (_, e) => { ToggleFav(w); fav.Content = IsFav(w) ? "★" : "☆"; e.Handled = true; };
        var set = new Button { Content = "🖥", Padding = new Thickness(8, 2, 8, 2), ToolTip = "Установить на рабочий стол" };
        set.Click += async (_, e) => { e.Handled = true; await SetWallpaper(w); };

        var bar = new DockPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0), Visibility = Visibility.Collapsed,
            Background = new LinearGradientBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), System.Windows.Media.Color.FromArgb(210, 0, 0, 0), 90)
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        buttons.Children.Add(fav); buttons.Children.Add(set);
        DockPanel.SetDock(buttons, Dock.Right);
        bar.Children.Add(buttons);
        bar.Children.Add(new TextBlock
        {
            Text = w.Resolution + (string.IsNullOrEmpty(w.Title) ? "" : " · " + w.Title), FontSize = 12, Margin = new Thickness(10, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Bottom, TextTrimming = TextTrimming.CharacterEllipsis
        });

        var grid = new Grid();
        grid.Children.Add(img);
        grid.Children.Add(badge);
        grid.Children.Add(bar);

        var card = new Border
        {
            Width = 300, Height = 188, Margin = new Thickness(6), CornerRadius = new CornerRadius(10),
            Background = (Brush)FindResource("Panel2"), Cursor = Cursors.Hand, Child = grid, ClipToBounds = true
        };
        card.Clip = new RectangleGeometry(new Rect(0, 0, 300, 188), 10, 10);
        card.MouseEnter += (_, _) => bar.Visibility = Visibility.Visible;
        card.MouseLeave += (_, _) => bar.Visibility = Visibility.Collapsed;
        card.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not Button && !IsInsideButton(e.OriginalSource as DependencyObject)) ShowPreview(w); };
        return card;
    }

    static bool IsInsideButton(DependencyObject? d)
    {
        while (d != null) { if (d is ButtonBase) return true; d = VisualTreeHelper.GetParent(d); }
        return false;
    }

    // ---------------- actions ----------------
    static bool IsFav(Wallpaper w) => S.Favorites.Any(f => f.Key == w.Key);

    void ToggleFav(Wallpaper w)
    {
        if (IsFav(w)) S.Favorites.RemoveAll(f => f.Key == w.Key);
        else S.Favorites.Insert(0, w);
        SettingsStore.Save();
        UpdateFavCount();
    }

    void UpdateFavCount() => FavCount.Text = S.Favorites.Count.ToString();

    async Task SetWallpaper(Wallpaper w)
    {
        ShowToast("Скачиваю и ставлю обои…");
        try { await WallpaperService.Apply(w); Backdrop.Invalidate(); ShowToast("Обои установлены ✓"); }
        catch (Exception ex) { ShowToast("Ошибка: " + ex.Message); }
    }

    void ShowToast(string text)
    {
        ToastText.Text = text;
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop(); _toastTimer.Start();
    }

    // ---------------- preview ----------------
    async void ShowPreview(Wallpaper w)
    {
        _current = w;
        Preview.Visibility = Visibility.Visible;
        PLoading.Visibility = Visibility.Visible;
        PImg.Source = null;
        PTitle.Text = !string.IsNullOrEmpty(w.Title) ? w.Title : w.Source == "bing" ? "Bing" : $"Wallhaven #{w.Id}";
        var meta = new List<string> { $"Разрешение: {w.Resolution}" };
        if (!string.IsNullOrEmpty(w.Category)) meta.Add((w.Source == "bing" ? "" : "Категория: ") + w.Category);
        if (w.Size > 0) meta.Add($"Размер: {w.Size / 1048576.0:0.0} МБ");
        if (w.Views > 0) meta.Add($"Просмотры: {w.Views:N0} · ★ {w.Favs:N0}");
        meta.Add("Источник: " + (w.Source == "bing" ? "Bing" : "Wallhaven"));
        PMeta.Text = string.Join("\n", meta);
        PFav.Content = IsFav(w) ? "★  В избранном" : "☆  В избранное";
        PSimilar.Visibility = w.Source == "wallhaven" ? Visibility.Visible : Visibility.Collapsed;

        PColors.Children.Clear();
        foreach (var c in w.Colors)
        {
            var cc = c.TrimStart('#');
            var b = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 5, 0), Background = UiKit.Hex(c), Cursor = Cursors.Hand, ToolTip = "Искать в этом цвете" };
            b.MouseLeftButtonUp += async (_, _) => { _f.Color = cc; BuildColors(); HidePreview(); TabBrowse.IsChecked = true; await LoadPage(true); };
            PColors.Children.Add(b);
        }

        // big image, decoded no larger than needed to save RAM
        try
        {
            var bi = UiKit.WebImage(w.Full, 2200);
            PImg.Source = bi;
            if (bi.IsDownloading)
            {
                bi.DownloadCompleted += (_, _) => PLoading.Visibility = Visibility.Collapsed;
                bi.DownloadFailed += (_, _) => { PLoading.Text = "Не удалось загрузить"; PImg.Source = UiKit.WebImage(w.Thumb, 800); };
            }
            else PLoading.Visibility = Visibility.Collapsed;
        }
        catch { PImg.Source = UiKit.WebImage(w.Thumb, 800); }

        PTags.Children.Clear();
        if (w.Source == "wallhaven")
        {
            try
            {
                var tags = await WallhavenApi.Tags(w.Id);
                if (_current != w) return;
                foreach (var t in tags.Take(18))
                {
                    var tag = t;
                    var b = new Button { Content = "#" + t, FontSize = 11, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 4) };
                    b.Click += async (_, _) => { Query.Text = tag; HidePreview(); TabBrowse.IsChecked = true; await LoadPage(true); };
                    PTags.Children.Add(b);
                }
            }
            catch { }
        }
    }

    void HidePreview()
    {
        Preview.Visibility = Visibility.Collapsed;
        PImg.Source = null;
        PVideo.MediaOpened -= OnPreviewOpened; PVideo.MediaEnded -= OnPreviewEnded;
        PVideo.Stop(); PVideo.Source = null; PVideo.Visibility = Visibility.Collapsed;
        PImg.Visibility = Visibility.Visible;
        PFav.Visibility = Visibility.Visible;
        _current = null;
        _currentLive = null;
    }

    void PreviewBg_Click(object s, MouseButtonEventArgs e) { if (e.OriginalSource == Preview) HidePreview(); }
    void PreviewBody_Click(object s, MouseButtonEventArgs e) => e.Handled = false;
    void ClosePreview_Click(object s, RoutedEventArgs e) => HidePreview();
    async void PSet_Click(object s, RoutedEventArgs e)
    {
        if (_currentLive != null) { await ApplyLive(_currentLive); return; }
        if (_current != null) await SetWallpaper(_current);
    }
    void PFav_Click(object s, RoutedEventArgs e)
    {
        if (_current == null) return;
        ToggleFav(_current);
        PFav.Content = IsFav(_current) ? "★  В избранном" : "☆  В избранное";
    }
    async void PDownload_Click(object s, RoutedEventArgs e)
    {
        if (_currentLive != null)
        {
            try
            {
                var lw = _currentLive;
                var file = await LiveApi.Download(lw, new Progress<double>(p => ShowToast($"Скачиваю видео… {p:P0}")));
                ShowToast("Сохранено");
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"");
            }
            catch (Exception ex) { ShowToast("Ошибка: " + ex.Message); }
            return;
        }
        if (_current == null) return;
        try
        {
            var file = await WallpaperService.Download(_current);
            ShowToast("Сохранено");
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"");
        }
        catch (Exception ex) { ShowToast("Ошибка: " + ex.Message); }
    }
    async void PSimilar_Click(object s, RoutedEventArgs e)
    {
        if (_current == null) return;
        Query.Text = "like:" + _current.Id;
        HidePreview();
        if (TabBrowse.IsChecked == true) await LoadPage(true); else TabBrowse.IsChecked = true;
    }
    void POpen_Click(object s, RoutedEventArgs e)
    {
        if (_current == null) return;
        Native.OpenUrl(string.IsNullOrEmpty(_current.Page) ? _current.Full : _current.Page);
    }

    // ================= live wallpapers =================
    LiveWallpaper? _currentLive;
    int _liveMinWidth = 1920;
    static LiveSettings LS => SettingsStore.Current.Live;

    static readonly (int w, string label)[] LiveRes = { (0, "Любое"), (1280, "HD"), (1920, "Full HD"), (2560, "2K"), (3840, "4K") };

    static readonly string[] NasaIdeas = { "earth from space", "nebula", "aurora", "galaxy", "sun", "ISS", "mars", "moon", "hubble", "black hole" };
    static readonly string[] StockIdeas = { "ocean", "rain", "city night", "neon", "forest", "clouds", "abstract", "space", "fire", "snow", "anime", "cyberpunk" };

    void InitLive()
    {
        LiveMute.IsChecked = LS.Mute;
        LivePause.IsChecked = LS.PauseWhenCovered;
        BuildLiveRes();
        LiveSource_Checked(this, new RoutedEventArgs());
        App.Me.Live.StateChanged += UpdateLiveNow;
        Closed += (_, _) => App.Me.Live.StateChanged -= UpdateLiveNow;
    }

    string LiveSource => LivePexels.IsChecked == true ? "pexels" : LivePixabay.IsChecked == true ? "pixabay" : "nasa";

    LiveFilters CollectLive() => new()
    {
        Source = LiveSource, Query = Query.Text.Trim(), MinWidth = _liveMinWidth,
        Orientation = LivePort.IsChecked == true ? "portrait" : LiveAny.IsChecked == true ? "any" : "landscape",
        Order = LiveOrder.SelectedItem is ComboBoxItem { Tag: string t } ? t : "popular"
    };

    void BuildLiveRes()
    {
        LiveResChips.Children.Clear();
        foreach (var (w, label) in LiveRes)
        {
            var ww = w;
            LiveResChips.Children.Add(Chip(label, _liveMinWidth == w, () => { _liveMinWidth = ww; BuildLiveRes(); }));
        }
    }

    void LiveSource_Checked(object s, RoutedEventArgs e)
    {
        if (LiveKeyPanel is null || LiveIdeas is null) return;
        var src = LiveSource;
        LiveKeyPanel.Visibility = src == "nasa" ? Visibility.Collapsed : Visibility.Visible;
        LiveKey.Text = src == "pexels" ? LS.PexelsKey : src == "pixabay" ? LS.PixabayKey : "";
        LiveKeyHint.Text = src == "pexels"
            ? "Ключ Pexels API (бесплатно): зарегистрируйся на pexels.com/api и скопируй ключ сюда."
            : "Ключ Pixabay API (бесплатно): войди на pixabay.com, ключ будет на странице pixabay.com/api/docs.";
        LiveIdeas.Children.Clear();
        foreach (var idea in src == "nasa" ? NasaIdeas : StockIdeas)
        {
            var q = idea;
            var b = new Button { Content = idea, FontSize = 11.5, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 5, 5) };
            b.Click += async (_, _) => { Query.Text = q; await LoadPage(true); };
            LiveIdeas.Children.Add(b);
        }
        if (IsLoaded && _view == "live") _ = LoadPage(true);
    }

    void LiveKey_LostFocus(object s, RoutedEventArgs e)
    {
        if (LiveSource == "pexels") LS.PexelsKey = LiveKey.Text.Trim();
        else if (LiveSource == "pixabay") LS.PixabayKey = LiveKey.Text.Trim();
        SettingsStore.Save();
    }

    void LiveGetKey_Click(object s, RoutedEventArgs e) =>
        Native.OpenUrl(LiveSource == "pexels" ? "https://www.pexels.com/api/" : "https://pixabay.com/api/docs/");

    void LiveMute_Click(object s, RoutedEventArgs e) => App.Me.Live.SetMute(LiveMute.IsChecked == true);

    void LivePause_Click(object s, RoutedEventArgs e)
    {
        LS.PauseWhenCovered = LivePause.IsChecked == true;
        SettingsStore.Save();
    }

    void LiveFile_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Видео|*.mp4;*.m4v;*.mov;*.wmv;*.avi|Все файлы|*.*", Title = "Видео для живых обоев" };
        if (dlg.ShowDialog(this) != true) return;
        LS.Title = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
        App.Me.Live.PlayAndRemember(dlg.FileName);
        ShowToast("Живые обои включены ✓");
    }

    void LiveStop_Click(object s, RoutedEventArgs e)
    {
        App.Me.Live.Stop();
        ShowToast("Живые обои выключены");
    }

    void UpdateLiveNow()
    {
        LiveNow.Text = App.Me.Live.IsPlaying ? "▶ " + (string.IsNullOrEmpty(LS.Title) ? System.IO.Path.GetFileName(LS.File) : LS.Title) : "Выключены";
    }

    async Task ApplyLive(LiveWallpaper w)
    {
        try
        {
            ShowToast("Скачиваю видео…");
            await App.Me.Live.Apply(w, new Progress<double>(p => ShowToast($"Скачиваю видео… {p:P0}")));
            ShowToast("Живые обои установлены ✓");
            UpdateLiveNow();
        }
        catch (Exception ex) { ShowToast("Ошибка: " + ex.Message); }
    }

    FrameworkElement LiveCard(LiveWallpaper w)
    {
        var img = new Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        try
        {
            var bi = UiKit.WebImage(w.Thumb.Replace("http://", "https://"), 380);
            img.Source = bi;
            if (bi.IsDownloading) bi.DownloadCompleted += (_, _) => img.Opacity = 1; else img.Opacity = 1;
        }
        catch { }
        var badge = new Border
        {
            Background = UiKit.Brush(170, 0, 0, 0), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8),
            Child = new TextBlock { Text = "▶ " + w.Badge + (w.Duration > 0 ? $" · {w.Duration}s" : ""), FontSize = 11, FontWeight = FontWeights.SemiBold }
        };
        var set = new Button { Content = "🖥", Padding = new Thickness(8, 2, 8, 2), ToolTip = "Поставить живыми обоями", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8), Visibility = Visibility.Collapsed };
        set.Click += async (_, e) => { e.Handled = true; await ApplyLive(w); };
        var title = new TextBlock
        {
            Text = w.Title, FontSize = 12, Margin = new Thickness(10, 0, 50, 10), VerticalAlignment = VerticalAlignment.Bottom,
            TextTrimming = TextTrimming.CharacterEllipsis, Visibility = Visibility.Collapsed
        };
        var shade = new Border { Background = new LinearGradientBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), System.Windows.Media.Color.FromArgb(200, 0, 0, 0), 90), Visibility = Visibility.Collapsed };
        var grid = new Grid();
        grid.Children.Add(img); grid.Children.Add(shade); grid.Children.Add(badge); grid.Children.Add(title); grid.Children.Add(set);
        var card = new Border
        {
            Width = 300, Height = 188, Margin = new Thickness(6), CornerRadius = new CornerRadius(10),
            Background = (Brush)FindResource("Panel2"), Cursor = Cursors.Hand, Child = grid,
            Clip = new RectangleGeometry(new Rect(0, 0, 300, 188), 10, 10)
        };
        card.MouseEnter += (_, _) => shade.Visibility = title.Visibility = set.Visibility = Visibility.Visible;
        card.MouseLeave += (_, _) => shade.Visibility = title.Visibility = set.Visibility = Visibility.Collapsed;
        card.MouseLeftButtonUp += (_, e) => { if (!IsInsideButton(e.OriginalSource as DependencyObject)) ShowLivePreview(w); };
        return card;
    }

    async void ShowLivePreview(LiveWallpaper w)
    {
        _current = null;
        _currentLive = w;
        Preview.Visibility = Visibility.Visible;
        PFav.Visibility = Visibility.Collapsed;
        PSimilar.Visibility = Visibility.Collapsed;
        PTags.Children.Clear();
        PColors.Children.Clear();
        PTitle.Text = string.IsNullOrEmpty(w.Title) ? "Живые обои" : w.Title;
        var meta = new List<string>();
        if (w.Width > 0) meta.Add($"Разрешение: {w.Width}×{w.Height}");
        if (w.Duration > 0) meta.Add($"Длительность: {w.Duration} с (крутится по кругу)");
        if (!string.IsNullOrEmpty(w.Author)) meta.Add("Автор: " + w.Author);
        meta.Add("Источник: " + w.Source switch { "pexels" => "Pexels", "pixabay" => "Pixabay", _ => "NASA" });
        PMeta.Text = string.Join("\n", meta);
        PImg.Source = UiKit.WebImage(w.Thumb.Replace("http://", "https://"), 1280);
        PImg.Visibility = Visibility.Visible;
        PLoading.Text = "Загружаю превью видео…";
        PLoading.Visibility = Visibility.Visible;
        try
        {
            if (string.IsNullOrEmpty(w.Url) && w.Source == "nasa") w.Url = await LiveApi.ResolveNasa(w);
            if (_currentLive != w) return;
            PVideo.Source = new Uri(w.Url);
            PVideo.MediaOpened += OnPreviewOpened;
            PVideo.MediaEnded += OnPreviewEnded;
            PVideo.Visibility = Visibility.Visible;
            PVideo.Play();
        }
        catch (Exception ex) { PLoading.Text = "Превью недоступно: " + ex.Message; }
    }

    void OnPreviewOpened(object? s, RoutedEventArgs e)
    {
        PVideo.MediaOpened -= OnPreviewOpened;
        PImg.Visibility = Visibility.Collapsed;
        PLoading.Visibility = Visibility.Collapsed;
    }

    void OnPreviewEnded(object? s, RoutedEventArgs e)
    {
        PVideo.Position = TimeSpan.Zero;
        PVideo.Play();
    }
}
