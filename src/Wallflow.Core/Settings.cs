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
    public string Text { get; set; } = "";
    /// <summary>accent color of the note strip, hex</summary>
    public string Color { get; set; } = "#FFD27A";
    public WidgetPos Pos { get; set; } = new();
}

public sealed class TodoItem
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class DockSettings
{
    public bool Enabled { get; set; }
    /// <summary>Hide the Windows taskbar (auto-hide) while the dock is on.</summary>
    public bool AutoHideTaskbar { get; set; } = true;
    public bool ShowStart { get; set; } = true;
    public bool ShowSearch { get; set; } = true;
    public bool ShowRunning { get; set; } = true;
    public bool ShowClock { get; set; } = true;
    public double IconSize { get; set; } = 40;
    /// <summary>Reserve screen space so maximized windows stop above the dock.</summary>
    public bool ReserveSpace { get; set; } = true;
    public List<string> Pinned { get; set; } = new();
    public bool PinnedImported { get; set; }
    /// <summary>Taskbar auto-hide state before the dock changed it, to restore later.</summary>
    public bool? PrevTaskbarAutoHide { get; set; }
}

public sealed class TaskbarTweaks
{
    /// <summary>0 hidden, 1 icon, 2 box, 3 icon+label; -1 = don't touch</summary>
    public int SearchMode { get; set; } = -1;
    public int TaskView { get; set; } = -1;
    public int Widgets { get; set; } = -1;
    /// <summary>0 left, 1 center</summary>
    public int Alignment { get; set; } = -1;
}

public sealed class LiveSettings
{
    public bool Enabled { get; set; }
    public string File { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Mute { get; set; } = true;
    public bool PauseWhenCovered { get; set; } = true;
    public string PexelsKey { get; set; } = "";
    public string PixabayKey { get; set; } = "";
    public List<LiveWallpaper> Favorites { get; set; } = new();
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
    public bool HideDesktopIcons { get; set; }
    public bool AutoAddNewShortcuts { get; set; } = true;
    public List<string> IgnoredShortcuts { get; set; } = new();

    public bool AutoStart { get; set; } = true;

    public DockSettings Dock { get; set; } = new();
    public TaskbarTweaks Taskbar { get; set; } = new();
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
