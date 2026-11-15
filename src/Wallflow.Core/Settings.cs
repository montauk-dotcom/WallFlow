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
    /// <summary>Real background blur (DWM). Off = plain translucent glass, cheapest.</summary>
    public bool Blur { get; set; } = true;
    public double Opacity { get; set; } = 0.35;
    public double Scale { get; set; } = 1.0;

    public WidgetPos ClockPos { get; set; } = new();
    public WidgetPos WeatherPos { get; set; } = new();
    public WidgetPos CalendarPos { get; set; } = new();
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
