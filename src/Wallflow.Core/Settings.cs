using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wallflow.Core;

public sealed class WidgetPos
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
}

public sealed class FolderDef
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Папка";
    public List<string> Items { get; set; } = new();
    public WidgetPos Pos { get; set; } = new();
}

public sealed class NoteDef
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public double FontSize { get; set; } = 15;
    public double Width { get; set; } = 260;
    /// <summary>accent color of the note strip, hex</summary>
    public string Color { get; set; } = "#FFD27A";
    public WidgetPos Pos { get; set; } = new();
}

public sealed class TodoItem
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

/// <summary>Only kept to undo the taskbar auto-hide that the old dock may have switched on.</summary>
public sealed class DockSettings
{
    public bool? PrevTaskbarAutoHide { get; set; }
}

public sealed class LiveSettings
{
    public bool Enabled { get; set; }
    public string File { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Mute { get; set; } = true;
    public bool PauseWhenCovered { get; set; } = true;
    /// <summary>auto | workerw | progman | owned — how the video is put on the desktop</summary>
    public string AttachMode { get; set; } = "auto";
}

/// <summary>Plain desktop items (not shortcuts) that may be put into glass folders. All off by default.</summary>
public sealed class DesktopFileKinds
{
    public bool Documents { get; set; }
    public bool Images { get; set; }
    public bool Archives { get; set; }
    public bool Folders { get; set; }
    public bool Media { get; set; }
    public bool Other { get; set; }
}

public sealed class SlideshowSettings
{
    public bool Enabled { get; set; }
    public int Minutes { get; set; } = 30;
    /// <summary>favorites | search | bing</summary>
    public string Source { get; set; } = "favorites";
}

public sealed class WidgetSettings
{
    public bool Clock { get; set; } = true;
    public bool Weather { get; set; } = true;
    public bool Calendar { get; set; } = true;
    public bool Folders { get; set; } = true;
    public bool Player { get; set; } = true;
    public bool Todo { get; set; }
    public List<NoteDef> Notes { get; set; } = new();
    public List<TodoItem> TodoItems { get; set; } = new();
    public string TodoTitle { get; set; } = "";
    public double TodoFontSize { get; set; } = 14.5;
    /// <summary>extra vertical space around each task, px</summary>
    public double TodoSpacing { get; set; } = 3;
    /// <summary>Move finished tasks to the bottom (true) or keep them where they are.</summary>
    public bool TodoDoneToBottom { get; set; } = true;
    /// <summary>click | hover</summary>
    public string FolderOpenMode { get; set; } = "click";

    /// <summary>ios | poster | neon | script | retro | mono | bold</summary>
    public string ClockStyle { get; set; } = "poster";
    public bool ShowSeconds { get; set; }
    public bool Use24h { get; set; } = true;
    /// <summary>en | ru — language of day/month names</summary>
    public string Language { get; set; } = "en";

    public string City { get; set; } = "Tashkent";
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string CityResolved { get; set; } = "";

    /// <summary>dark | light glass tint</summary>
    public string Tint { get; set; } = "dark";
    /// <summary>frost = blurred wallpaper behind (iOS-like) | tint = plain translucent | clear = outline only</summary>
    public string GlassMode { get; set; } = "frost";
    public double BlurStrength { get; set; } = 60;
    /// <summary>Darkness/lightness of the tint layer on top of the glass, 0..0.9</summary>
    public double Opacity { get; set; } = 0.22;
    public double Radius { get; set; } = 26;
    /// <summary>Palette key (white, pink…) or a custom "#RRGGBB"</summary>
    public string ClockColor { get; set; } = "white";
    /// <summary>Fill the clock text with a gradient instead of one color.</summary>
    public bool ClockGradient { get; set; }
    public string GradientFrom { get; set; } = "#FF5CA8";
    public string GradientTo { get; set; } = "#5AE6FF";
    public double GradientAngle { get; set; } = 0;
    public bool TextShadow { get; set; } = true;
    public double Scale { get; set; } = 1.0;
    /// <summary>Pick the widget size from the screen size (off as soon as the user moves the size slider).</summary>
    public bool AutoScale { get; set; } = true;

    public WidgetPos ClockPos { get; set; } = new();
    public WidgetPos WeatherPos { get; set; } = new();
    public WidgetPos CalendarPos { get; set; } = new();
    public WidgetPos PlayerPos { get; set; } = new();
    public WidgetPos TodoPos { get; set; } = new();
}

public sealed class AppSettings
{
    // wallpapers
    public string WallhavenApiKey { get; set; } = "";
    public string WallpaperStyle { get; set; } = "fill";
    public string SaveDir { get; set; } = "";
    public List<Wallpaper> Favorites { get; set; } = new();
    public SearchFilters LastFilters { get; set; } = new();
    public SlideshowSettings Slideshow { get; set; } = new();

    // desktop
    public WidgetSettings Widgets { get; set; } = new();
    public List<FolderDef> Folders { get; set; } = new();
    public bool FoldersInitialized { get; set; }
    public bool HideDesktopIcons { get; set; } = true;
    /// <summary>One-time switch to "icons hidden by default" for people upgrading from older versions.</summary>
    public bool IconsDefaultApplied { get; set; }

    /// <summary>Widget positions remembered per screen setup ("1536x816" etc.) — laptop and monitor keep their own layouts.</summary>
    public Dictionary<string, Dictionary<string, WidgetPos>> Layouts { get; set; } = new();
    public Dictionary<string, double> LayoutScales { get; set; } = new();
    public string LayoutKey { get; set; } = "";
    public bool AutoAddNewShortcuts { get; set; } = true;
    public DesktopFileKinds DesktopFiles { get; set; } = new();
    public List<string> IgnoredShortcuts { get; set; } = new();

    public bool AutoStart { get; set; } = true;
    /// <summary>ru | en | "" = by Windows language</summary>
    public string UiLanguage { get; set; } = "";

    public DockSettings Dock { get; set; } = new();
    public LiveSettings Live { get; set; } = new();

    [JsonIgnore]
    public string ResolvedSaveDir => string.IsNullOrWhiteSpace(SaveDir)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Wallflow")
        : SaveDir;
}

public static class SettingsStore
{
    static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wallflow");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Opts) ?? new();
        }
        catch { Current = new(); }
        return Current;
    }

    static readonly object Gate = new();
    public static void Save()
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Opts));
            File.Move(tmp, FilePath, true);
        }
    }
}
