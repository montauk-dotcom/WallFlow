using System;
using System.Drawing;
using System.Windows.Forms;
using Wallflow.Core;

namespace Wallflow;

/// <summary>Notification-area icon. WinForms NotifyIcon is the lightest option (no extra packages).</summary>
public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;
    readonly ToolStripMenuItem _edit, _hideIcons, _slideshow, _autostart, _wClock, _wWeather, _wCalendar, _wFolders;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkColors()) { RoundedEdges = true },
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(231, 233, 243),
            BackColor = Color.FromArgb(20, 23, 36)
        };

        menu.Items.Add(Item("🖼  Обои…", () => App.Me.OpenGallery(), bold: true));
        menu.Items.Add(Item("⟳  Следующие обои", async () => await App.Me.NextWallpaper()));
        menu.Items.Add(_slideshow = Check("Слайдшоу обоев", v =>
        {
            SettingsStore.Current.Slideshow.Enabled = v;
            SettingsStore.Save();
            App.Me.ApplySlideshow();
        }));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(_edit = Check("✎  Переставить виджеты", v => App.Me.Widgets.SetEditMode(v)));
        var widgets = new ToolStripMenuItem("Виджеты");
        widgets.DropDown.BackColor = menu.BackColor;
        widgets.DropDown.ForeColor = menu.ForeColor;
        widgets.DropDown.Renderer = menu.Renderer;
        widgets.DropDownItems.Add(_wClock = Check("Часы и день недели", v => SetWidget(w => w.Clock = v)));
        widgets.DropDownItems.Add(_wWeather = Check("Погода", v => SetWidget(w => w.Weather = v)));
        widgets.DropDownItems.Add(_wCalendar = Check("Календарь", v => SetWidget(w => w.Calendar = v)));
        widgets.DropDownItems.Add(_wFolders = Check("Папки с ярлыками", v => SetWidget(w => w.Folders = v)));
        widgets.DropDownItems.Add(new ToolStripSeparator());
        widgets.DropDownItems.Add(Item("Вернуть на места по умолчанию", () => App.Me.Widgets.ResetPositions()));
        menu.Items.Add(widgets);
        menu.Items.Add(_hideIcons = Check("Скрыть обычные значки рабочего стола", v => App.Me.SetDesktopIconsHidden(v)));
        menu.Items.Add(Item("Разложить ярлыки по папкам заново", () =>
        {
            if (System.Windows.MessageBox.Show("Папки соберутся заново по категориям (игры, браузеры…). Твои ручные перестановки сбросятся. Продолжить?",
                    "Wallflow", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
                App.Me.Widgets.Rearrange();
        }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("⚙  Настройки…", () => App.Me.OpenSettings()));
        menu.Items.Add(_autostart = Check("Запускать вместе с Windows", v =>
        {
            SettingsStore.Current.AutoStart = v;
            SettingsStore.Save();
            Native.SetAutoStart(v);
        }));
        menu.Items.Add(Item("Выход", () => App.Me.Quit()));
        menu.Opening += (_, _) => Sync();

        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            Text = "Wallflow",
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) App.Me.OpenGallery(); };
        Sync();
    }

    static void SetWidget(Action<WidgetSettings> change)
    {
        change(SettingsStore.Current.Widgets);
        SettingsStore.Save();
        App.Me.Widgets.RebuildAll();
    }

    public void Sync()
    {
        var s = SettingsStore.Current;
        _edit.Checked = App.Me.Widgets.EditMode;
        _hideIcons.Checked = s.HideDesktopIcons;
        _slideshow.Checked = s.Slideshow.Enabled;
        _autostart.Checked = s.AutoStart;
        _wClock.Checked = s.Widgets.Clock;
        _wWeather.Checked = s.Widgets.Weather;
        _wCalendar.Checked = s.Widgets.Calendar;
        _wFolders.Checked = s.Widgets.Folders;
    }

    public void Balloon(string title, string text) => _icon.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);

    static ToolStripMenuItem Item(string text, Action onClick, bool bold = false)
    {
        var i = new ToolStripMenuItem(text) { ForeColor = Color.FromArgb(231, 233, 243) };
        if (bold) i.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        i.Click += (_, _) => onClick();
        return i;
    }

    static ToolStripMenuItem Check(string text, Action<bool> onChange)
    {
        var i = new ToolStripMenuItem(text) { CheckOnClick = true, ForeColor = Color.FromArgb(231, 233, 243) };
        i.Click += (_, _) => onChange(i.Checked);
        return i;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    sealed class DarkColors : ProfessionalColorTable
    {
        static readonly Color Bg = Color.FromArgb(20, 23, 36), Hover = Color.FromArgb(45, 40, 80), Line = Color.FromArgb(38, 43, 64);
        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Line;
        public override Color CheckBackground => Color.FromArgb(124, 92, 255);
        public override Color CheckSelectedBackground => Color.FromArgb(160, 139, 255);
        public override Color CheckPressedBackground => Color.FromArgb(124, 92, 255);
    }
}
