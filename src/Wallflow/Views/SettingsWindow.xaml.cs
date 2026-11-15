using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wallflow.Core;
using Wallflow.Widgets;

namespace Wallflow.Views;

public partial class SettingsWindow : Window
{
    bool _ready;
    static AppSettings S => SettingsStore.Current;
    static WidgetSettings W => SettingsStore.Current.Widgets;

    public SettingsWindow()
    {
        InitializeComponent();
        UiKit.DarkTitleBar(this);
        Load();
        _ready = true;
        App.Me.Widgets.EditModeChanged += SyncEditBtn;
        Closed += (_, _) => App.Me.Widgets.EditModeChanged -= SyncEditBtn;
        SyncEditBtn();
    }

    public void SelectTab(string tab)
    {
        (tab switch
        {
            "desktop" => NavDesktop,
            "wallpapers" => NavWallpapers,
            "general" => NavGeneral,
            _ => NavWidgets
        }).IsChecked = true;
    }

    void Nav_Checked(object s, RoutedEventArgs e)
    {
        if (PageWidgets is null) return;
        PageWidgets.Visibility = s == NavWidgets ? Visibility.Visible : Visibility.Collapsed;
        PageDesktop.Visibility = s == NavDesktop ? Visibility.Visible : Visibility.Collapsed;
        PageWallpapers.Visibility = s == NavWallpapers ? Visibility.Visible : Visibility.Collapsed;
        PageGeneral.Visibility = s == NavGeneral ? Visibility.Visible : Visibility.Collapsed;
        if (s == NavGeneral) UpdateMem();
        if (s == NavDesktop) LoadFolders();
    }

    void Load()
    {
        WClock.IsChecked = W.Clock; WWeather.IsChecked = W.Weather; WCalendar.IsChecked = W.Calendar; WFolders.IsChecked = W.Folders;
        LangEn.IsChecked = W.Language != "ru"; LangRu.IsChecked = W.Language == "ru";
        H24.IsChecked = W.Use24h; Seconds.IsChecked = W.ShowSeconds;
        City.Text = W.City;
        CityStatus.Text = string.IsNullOrEmpty(W.CityResolved) ? "" : $"Сейчас: {W.CityResolved}";
        TintDark.IsChecked = W.Tint != "light"; TintLight.IsChecked = W.Tint == "light";
        ModeFrost.IsChecked = W.GlassMode == "frost"; ModeTint.IsChecked = W.GlassMode == "tint"; ModeClear.IsChecked = W.GlassMode == "clear";
        Shadow.IsChecked = W.TextShadow;
        OpacitySlider.Value = W.Opacity; ScaleSlider.Value = W.Scale; BlurSlider.Value = W.BlurStrength; RadiusSlider.Value = W.Radius;
        BuildFaces();
        BuildClockColors();

        HideIcons.IsChecked = S.HideDesktopIcons; AutoAdd.IsChecked = S.AutoAddNewShortcuts;

        foreach (ComboBoxItem i in WpStyle.Items) if ((string)i.Tag == S.WallpaperStyle) WpStyle.SelectedItem = i;
        SaveDir.Text = S.ResolvedSaveDir;
        ApiKey.Text = S.WallhavenApiKey;
        SsOn.IsChecked = S.Slideshow.Enabled; SsMin.Text = S.Slideshow.Minutes.ToString();
        foreach (ComboBoxItem i in SsSrc.Items) if ((string)i.Tag == S.Slideshow.Source) SsSrc.SelectedItem = i;

        AutoStart.IsChecked = S.AutoStart;
    }

    void SyncEditBtn() => EditBtn.Content = App.Me.Widgets.EditMode ? "✓ Готово (закрепить виджеты)" : "✎ Переставить виджеты";

    // ---------------- widgets ----------------
    void BuildFaces()
    {
        Faces.Items.Clear();
        var now = DateTime.Now;
        foreach (var (key, title) in ClockWidget.Faces)
        {
            var face = ClockWidget.BuildFace(key, now, W);
            var preview = new Border
            {
                Height = 120, Padding = new Thickness(10),
                Background = new LinearGradientBrush(Color.FromRgb(42, 32, 92), Color.FromRgb(16, 60, 90), 35),
                CornerRadius = new CornerRadius(10),
                Child = new Viewbox { Child = face, Stretch = Stretch.Uniform }
            };
            var sp = new StackPanel();
            sp.Children.Add(preview);
            sp.Children.Add(new TextBlock { Text = title, Margin = new Thickness(2, 8, 0, 0), FontSize = 12.5 });
            var selected = W.ClockStyle == key;
            var card = new Border
            {
                Width = 200, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(8), CornerRadius = new CornerRadius(12),
                Background = (Brush)FindResource("Panel"), Cursor = Cursors.Hand, Child = sp,
                BorderThickness = new Thickness(2),
                BorderBrush = selected ? (Brush)FindResource("Accent") : (Brush)FindResource("Line")
            };
            var k = key;
            card.MouseLeftButtonUp += (_, _) =>
            {
                W.ClockStyle = k;
                if (!W.Clock) { W.Clock = true; WClock.IsChecked = true; App.Me.Widgets.RebuildAll(); }
                SettingsStore.Save();
                App.Me.Widgets.RefreshLook();
                BuildFaces();
            };
            Faces.Items.Add(card);
        }
    }

    void WidgetToggle_Click(object s, RoutedEventArgs e)
    {
        W.Clock = WClock.IsChecked == true; W.Weather = WWeather.IsChecked == true;
        W.Calendar = WCalendar.IsChecked == true; W.Folders = WFolders.IsChecked == true;
        SettingsStore.Save();
        App.Me.Widgets.RebuildAll();
        App.Me.Tray?.Sync();
    }

    void Look_Changed(object s, RoutedEventArgs e)
    {
        if (!_ready) return;
        var langChanged = (LangRu.IsChecked == true ? "ru" : "en") != W.Language;
        W.Language = LangRu.IsChecked == true ? "ru" : "en";
        W.Use24h = H24.IsChecked == true;
        W.ShowSeconds = Seconds.IsChecked == true;
        W.Tint = TintLight.IsChecked == true ? "light" : "dark";
        W.GlassMode = ModeTint.IsChecked == true ? "tint" : ModeClear.IsChecked == true ? "clear" : "frost";
        W.TextShadow = Shadow.IsChecked == true;
        if (langChanged) W.CityResolved = ""; // city name in the new language
        SettingsStore.Save();
        App.Me.Widgets.RefreshLook();
        BuildFaces();
    }

    void Slider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        W.Opacity = Math.Round(OpacitySlider.Value, 2);
        W.Scale = Math.Round(ScaleSlider.Value, 2);
        W.Radius = Math.Round(RadiusSlider.Value);
        var blur = Math.Round(BlurSlider.Value);
        bool blurChanged = Math.Abs(blur - W.BlurStrength) > 0.5;
        W.BlurStrength = blur;
        SettingsStore.Save();
        if (blurChanged) Backdrop.Invalidate(); // re-blurs once, widgets update by themselves
        App.Me.Widgets.RefreshLook();
    }

    void BuildClockColors()
    {
        ClockColors.Children.Clear();
        foreach (var (key, title, hex) in ClockWidget.Palette)
        {
            var k = key;
            var b = new Border
            {
                Width = 26, Height = 26, Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(13),
                Background = UiKit.Hex(hex), Cursor = Cursors.Hand, ToolTip = title,
                BorderThickness = new Thickness(W.ClockColor == key ? 3 : 1),
                BorderBrush = W.ClockColor == key ? (Brush)FindResource("Accent") : UiKit.Brush(60, 255, 255, 255)
            };
            b.MouseLeftButtonUp += (_, _) =>
            {
                W.ClockColor = k;
                SettingsStore.Save();
                App.Me.Widgets.RefreshLook();
                BuildClockColors();
                BuildFaces();
            };
            ClockColors.Children.Add(b);
        }
    }

    async void City_Click(object s, RoutedEventArgs e)
    {
        var city = City.Text.Trim();
        if (city.Length == 0) return;
        CityStatus.Text = "Ищу…";
        try
        {
            var (lat, lon, name) = await WeatherApi.Geocode(city, W.Language);
            W.City = city; W.Lat = lat; W.Lon = lon; W.CityResolved = name;
            SettingsStore.Save();
            CityStatus.Text = $"Сейчас: {name} ({lat:0.00}, {lon:0.00})";
            App.Me.Widgets.RefreshLook();
        }
        catch (Exception ex) { CityStatus.Text = "Ошибка: " + ex.Message; }
    }

    void ResetPos_Click(object s, RoutedEventArgs e) => App.Me.Widgets.ResetPositions();
    void Edit_Click(object s, RoutedEventArgs e) => App.Me.Widgets.SetEditMode(!App.Me.Widgets.EditMode);
    void OpenGallery_Click(object s, RoutedEventArgs e) => App.Me.OpenGallery();

    // ---------------- desktop & folders ----------------
    void HideIcons_Click(object s, RoutedEventArgs e) => App.Me.SetDesktopIconsHidden(HideIcons.IsChecked == true);

    void AutoAdd_Click(object s, RoutedEventArgs e)
    {
        S.AutoAddNewShortcuts = AutoAdd.IsChecked == true;
        SettingsStore.Save();
    }

    void Rearrange_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Папки соберутся заново по категориям. Ручные перестановки сбросятся. Продолжить?", "Wallflow",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        App.Me.Widgets.Rearrange();
        LoadFolders();
    }

    void NewFolder_Click(object s, RoutedEventArgs e)
    {
        var name = Prompt.Ask("Название новой папки", "Новая папка");
        if (string.IsNullOrWhiteSpace(name)) return;
        S.Folders.Add(new FolderDef { Name = name.Trim() });
        SettingsStore.Save();
        LoadFolders(S.Folders.Count - 1);
    }

    FolderDef? SelectedFolder => FolderList.SelectedItem is ListBoxItem { Tag: FolderDef f } ? f : null;

    void LoadFolders(int select = -1)
    {
        var prev = FolderList.SelectedIndex;
        FolderList.Items.Clear();
        foreach (var f in S.Folders)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = f.Name, FontSize = 14, Margin = new Thickness(0, 0, 8, 0) });
            row.Children.Add(new TextBlock { Text = f.Items.Count.ToString(), Foreground = (Brush)FindResource("Muted") });
            FolderList.Items.Add(new ListBoxItem { Content = row, Tag = f, Padding = new Thickness(8, 6, 8, 6) });
        }
        MoveTarget.Items.Clear();
        foreach (var f in S.Folders) MoveTarget.Items.Add(new ComboBoxItem { Content = f.Name, Tag = f });
        if (MoveTarget.Items.Count > 0) MoveTarget.SelectedIndex = 0;
        int idx = select >= 0 ? select : Math.Max(0, Math.Min(prev, FolderList.Items.Count - 1));
        if (FolderList.Items.Count > 0) FolderList.SelectedIndex = idx;
        else ItemList.Items.Clear();
    }

    void FolderList_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        ItemList.Items.Clear();
        var f = SelectedFolder;
        if (f is null) return;
        foreach (var path in f.Items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image { Source = ShellIcon.Get(path, 48), Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0) });
            row.Children.Add(new TextBlock { Text = Shortcuts.DisplayName(path), VerticalAlignment = VerticalAlignment.Center });
            ItemList.Items.Add(new ListBoxItem { Content = row, Tag = path, Padding = new Thickness(6, 4, 6, 4), ToolTip = path });
        }
    }

    void FoldersChanged(int select = -1)
    {
        SettingsStore.Save();
        App.Me.Widgets.RebuildFolders();
        LoadFolders(select);
    }

    void FolderUp_Click(object s, RoutedEventArgs e) => MoveFolder(-1);
    void FolderDown_Click(object s, RoutedEventArgs e) => MoveFolder(1);

    void MoveFolder(int d)
    {
        var f = SelectedFolder;
        if (f is null) return;
        int i = S.Folders.IndexOf(f), j = i + d;
        if (j < 0 || j >= S.Folders.Count) return;
        (S.Folders[i], S.Folders[j]) = (S.Folders[j], S.Folders[i]);
        FoldersChanged(j);
    }

    void RenameFolder_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFolder;
        if (f is null) return;
        var name = Prompt.Ask("Название папки", f.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        f.Name = name.Trim();
        FoldersChanged(S.Folders.IndexOf(f));
    }

    void DeleteFolder_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFolder;
        if (f is null) return;
        S.Folders.Remove(f);
        if (f.Items.Count > 0)
        {
            var other = S.Folders.FirstOrDefault(x => x.Name == Shortcuts.Other);
            if (other is null) S.Folders.Add(other = new FolderDef { Name = Shortcuts.Other });
            other.Items.AddRange(f.Items);
        }
        FoldersChanged();
    }

    void MoveItems_Click(object s, RoutedEventArgs e)
    {
        var from = SelectedFolder;
        if (from is null || MoveTarget.SelectedItem is not ComboBoxItem { Tag: FolderDef to } || to == from) return;
        var paths = ItemList.SelectedItems.Cast<ListBoxItem>().Select(i => (string)i.Tag).ToList();
        if (paths.Count == 0) return;
        foreach (var p in paths) { from.Items.Remove(p); to.Items.Add(p); }
        FoldersChanged(S.Folders.IndexOf(from));
    }

    void RemoveItems_Click(object s, RoutedEventArgs e)
    {
        var from = SelectedFolder;
        if (from is null) return;
        var paths = ItemList.SelectedItems.Cast<ListBoxItem>().Select(i => (string)i.Tag).ToList();
        foreach (var p in paths) { from.Items.Remove(p); App.Me.Widgets.Ignore(p); }
        FoldersChanged(S.Folders.IndexOf(from));
    }

    // ---------------- wallpapers ----------------
    void WpStyle_Changed(object s, SelectionChangedEventArgs e)
    {
        if (!_ready || WpStyle.SelectedItem is not ComboBoxItem i) return;
        S.WallpaperStyle = (string)i.Tag;
        SettingsStore.Save();
    }

    void PickDir_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Куда сохранять обои", InitialDirectory = S.ResolvedSaveDir };
        if (dlg.ShowDialog(this) != true) return;
        S.SaveDir = dlg.FolderName;
        SettingsStore.Save();
        SaveDir.Text = S.ResolvedSaveDir;
    }

    void OpenDir_Click(object s, RoutedEventArgs e)
    {
        Directory.CreateDirectory(S.ResolvedSaveDir);
        Process.Start("explorer.exe", $"\"{S.ResolvedSaveDir}\"");
    }

    void ApiKey_LostFocus(object s, RoutedEventArgs e)
    {
        S.WallhavenApiKey = ApiKey.Text.Trim();
        SettingsStore.Save();
    }

    void Slideshow_Changed(object s, RoutedEventArgs e)
    {
        if (!_ready) return;
        S.Slideshow.Enabled = SsOn.IsChecked == true;
        S.Slideshow.Minutes = int.TryParse(SsMin.Text, out var m) ? Math.Clamp(m, 1, 1440) : 30;
        SsMin.Text = S.Slideshow.Minutes.ToString();
        if (SsSrc.SelectedItem is ComboBoxItem i) S.Slideshow.Source = (string)i.Tag;
        SettingsStore.Save();
        App.Me.ApplySlideshow();
    }

    async void Next_Click(object s, RoutedEventArgs e) => await App.Me.NextWallpaper();

    // ---------------- general ----------------
    void AutoStart_Click(object s, RoutedEventArgs e)
    {
        S.AutoStart = AutoStart.IsChecked == true;
        SettingsStore.Save();
        try { Native.SetAutoStart(S.AutoStart); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Автозапуск"); }
        App.Me.Tray?.Sync();
    }

    void UpdateMem()
    {
        using var p = Process.GetCurrentProcess();
        p.Refresh();
        MemInfo.Text = $"Сейчас Wallflow занимает {p.WorkingSet64 / 1048576.0:0} МБ RAM (с открытым окном настроек; в трее — меньше). " +
                       $"Приватная память: {p.PrivateMemorySize64 / 1048576.0:0} МБ.";
    }

    void Trim_Click(object s, RoutedEventArgs e)
    {
        Native.TrimMemory();
        UpdateMem();
    }
}
