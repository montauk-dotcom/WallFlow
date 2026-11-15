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
        WPlayer.IsChecked = W.Player; WTodo.IsChecked = W.Todo;
        UpdateNotesInfo();
        GradOn.IsChecked = W.ClockGradient; GradAngle.Value = W.GradientAngle;
        OpenClick.IsChecked = W.FolderOpenMode != "hover"; OpenHover.IsChecked = W.FolderOpenMode == "hover";
        UpdateGradientUi();
        BuildGradientPresets();
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

        VideoMute.IsChecked = S.Live.Mute; VideoPause.IsChecked = S.Live.PauseWhenCovered;
        VideoReliable.IsChecked = Wallflow.Motion.LiveEngine.ReliableMode;
        UpdateVideoNow();
    }

    // ---------------- video wallpaper ----------------
    void UpdateVideoNow() => VideoNow.Text = App.Me.Live.IsPlaying
        ? "▶ Сейчас: " + (string.IsNullOrEmpty(S.Live.Title) ? System.IO.Path.GetFileName(S.Live.File) : S.Live.Title)
        : "Выключены";

    void VideoPick_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Видео|*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.mkv|Все файлы|*.*", Title = "Видео для обоев" };
        if (dlg.ShowDialog(this) != true) return;
        S.Live.Title = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
        App.Me.Live.PlayAndRemember(dlg.FileName);
        UpdateVideoNow();
    }

    void VideoStop_Click(object s, RoutedEventArgs e) { App.Me.Live.Stop(); UpdateVideoNow(); }

    void Video_Changed(object s, RoutedEventArgs e)
    {
        if (!_ready) return;
        App.Me.Live.SetMute(VideoMute.IsChecked == true);
        S.Live.PauseWhenCovered = VideoPause.IsChecked == true;
        bool reliable = VideoReliable.IsChecked == true;
        if (reliable != Wallflow.Motion.LiveEngine.ReliableMode)
        {
            S.Live.AttachMode = reliable ? "owned" : "auto";
            if (reliable && !S.HideDesktopIcons) App.Me.SetDesktopIconsHidden(true);
            if (App.Me.Live.IsPlaying && System.IO.File.Exists(S.Live.File)) App.Me.Live.Play(S.Live.File);
        }
        SettingsStore.Save();
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
