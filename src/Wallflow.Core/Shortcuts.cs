using System.Runtime.InteropServices;
using System.Text;

namespace Wallflow.Core;

/// <summary>Finds desktop shortcuts and sorts them into iOS-style folders.</summary>
public static class Shortcuts
{
    /// <summary>Things that start a program: categorised by name (games, browsers…).</summary>
    static readonly string[] AppExts = { ".lnk", ".url", ".exe", ".appref-ms" };

    static readonly string[] SkipExts = { ".ini", ".tmp", ".crdownload", ".part", ".partial", ".download", ".!ut", ".db" };

    public static IEnumerable<string> DesktopDirs()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }

    /// <summary>Everything visible on the desktop: shortcuts, files of any kind and folders.</summary>
    public static List<string> ScanDesktop()
    {
        var res = new List<string>();
        foreach (var d in DesktopDirs())
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(d).ToList(); } catch { continue; }
            foreach (var e in entries)
                if (Visible(e) && Allowed(e)) res.Add(e);
        }
        return res.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => DisplayName(f), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    static bool Visible(string path)
    {
        try
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith("~$") || name.StartsWith(".")) return false;           // Office lock files, dotfiles
            if (SkipExts.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
            var attr = File.GetAttributes(path);
            return (attr & (FileAttributes.Hidden | FileAttributes.System)) == 0;
        }
        catch { return false; }
    }

    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>Shortcuts lose their extension (like on the desktop), real files keep it.</summary>
    public static string DisplayName(string path)
    {
        var ext = Path.GetExtension(path);
        if (Directory.Exists(path) || AppExts.Contains(ext, StringComparer.OrdinalIgnoreCase)) return Path.GetFileNameWithoutExtension(path) is { Length: > 0 } n ? n : Path.GetFileName(path);
        return Path.GetFileName(path);
    }

    public static bool IsApp(string path) => AppExts.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) && !Directory.Exists(path);

    // ---------------- file types ----------------
    public static readonly (string Name, string[] Exts)[] FileKinds =
    {
        ("Документы", new[] { ".doc", ".docx", ".docm", ".dot", ".dotx", ".odt", ".rtf", ".txt", ".md", ".pdf", ".xps", ".djvu", ".epub", ".fb2",
                              ".xls", ".xlsx", ".xlsm", ".xlsb", ".ods", ".csv", ".ppt", ".pptx", ".pptm", ".odp", ".pages", ".numbers", ".key", ".one", ".vsdx" }),
        ("Картинки", new[] { ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".heic", ".heif", ".avif", ".svg", ".ico", ".raw", ".cr2", ".nef", ".psd", ".ai" }),
        ("Видео", new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".flv", ".3gp", ".ts" }),
        ("Музыка", new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma" }),
        ("Архивы", new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".iso", ".cab" }),
        ("Установщики", new[] { ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".apk" }),
        ("Код", new[] { ".py", ".js", ".ts", ".html", ".css", ".json", ".xml", ".yaml", ".yml", ".cs", ".cpp", ".c", ".h", ".java", ".ps1", ".bat", ".cmd", ".sh", ".sql", ".ipynb" }),
        ("Торренты", new[] { ".torrent" }),
    };

    public const string Screens = "Скриншоты";
    public const string Dirs = "Папки";
    public const string Files = "Файлы";

    static readonly string[] ScreenWords = { "screenshot", "screen shot", "снимок экрана", "скриншот", "screen", "capture", "lightshot", "sharex", "photo_", "img_20" };

    public static bool IsShortcut(string path) => IsApp(path);

    /// <summary>Folder names created for plain files — removed when they end up empty.</summary>
    public static readonly string[] FileKindFolders =
        { "Документы", "Картинки", "Скриншоты", "Видео", "Музыка", "Архивы", "Установщики", "Код", "Торренты", "Папки", "Файлы" };

    // ---------------- categories ----------------
    public sealed record Category(string Name, string[] Keywords);

    public static readonly Category[] Categories =
    {
        new("Игры", new[] { "steam", "epic games", "battle.net", "battlenet", "riot", "valorant", "league of legends", "counter-strike", "cs2", "csgo",
            "dota", "minecraft", "tlauncher", "gta", "grand theft", "cyberpunk", "genshin", "honkai", "zenless", "wuthering", "ea app", "origin", "ubisoft", "uplay",
            "gog galaxy", "rockstar", "xbox", "fortnite", "apex", "overwatch", "roblox", "warcraft", "world of tanks", "wargaming", "lesta", "pubg", "rust",
            "elden ring", "witcher", "rdr", "red dead", "forza", "fifa", "ea sports", "fc 2", "call of duty", "warzone", "battlefield", "destiny", "hogwarts",
            "baldur", "starfield", "skyrim", "fallout", "terraria", "stardew", "palworld", "helldivers", "game", "launcher", "osu", "hearthstone", "diablo", "path of exile",
            "marvel rivals", "deadlock", "the finals", "escape from tarkov", "dayz", "arma", "rainbow six", "r6", "sims", "playnite", "heroic" }),
        new("Браузеры", new[] { "chrome", "firefox", "edge", "opera", "yandex", "яндекс", "brave", "vivaldi", "tor browser", "arc", "zen browser", "librewolf", "chromium", "thorium" }),
        new("Общение", new[] { "telegram", "discord", "whatsapp", "zoom", "skype", "teams", "slack", "viber", "signal", "vk messenger", "element", "mattermost", "outlook", "thunderbird", "mail" }),
        new("Медиа", new[] { "spotify", "vlc", "potplayer", "mpc", "aimp", "foobar", "itunes", "apple music", "яндекс музыка", "yandex music", "netflix", "youtube", "twitch",
            "obs", "streamlabs", "plex", "kodi", "winamp", "musicbee", "audacity", "media player", "кинопоиск" }),
        new("Творчество", new[] { "photoshop", "illustrator", "premiere", "after effects", "lightroom", "davinci", "resolve", "capcut", "blender", "figma", "gimp", "krita",
            "paint.net", "inkscape", "fl studio", "ableton", "cubase", "reaper", "vegas", "cinema 4d", "maya", "3ds max", "zbrush", "substance", "affinity", "clip studio", "canva", "audition", "adobe" }),
        new("Разработка", new[] { "visual studio", "vs code", "code", "pycharm", "intellij", "webstorm", "rider", "clion", "goland", "android studio", "git", "github", "docker",
            "terminal", "powershell", "cmd", "postman", "insomnia", "unity", "unreal", "godot", "sublime", "notepad++", "cursor", "windsurf", "dbeaver", "heidisql", "wsl",
            "putty", "winscp", "filezilla", "node", "python", "anaconda", "jupyter", "fork", "sourcetree", "claude" }),
        new("Офис", new[] { "word", "excel", "powerpoint", "onenote", "access", "publisher", "office", "libreoffice", "openoffice", "wps", "acrobat", "pdf", "foxit",
            "notion", "obsidian", "evernote", "todoist", "trello", "1с", "1c", "calculator", "калькулятор", "блокнот", "notepad" }),
        new("Утилиты", new[] { "nvidia", "geforce", "amd software", "radeon", "afterburner", "rivatuner", "cpu-z", "gpu-z", "hwmonitor", "hwinfo", "aida", "crystaldisk",
            "ccleaner", "revo", "7-zip", "winrar", "bandizip", "anydesk", "teamviewer", "rustdesk", "parsec", "control panel", "панель управления", "корзина", "recycle",
            "this pc", "этот компьютер", "settings", "параметры", "utorrent", "qbittorrent", "bittorrent", "vpn", "amnezia", "outline", "nekoray", "hiddify", "v2ray",
            "logitech", "razer", "steelseries", "corsair", "icue", "armoury", "msi center", "wallpaper", "everything", "powertoys", "rufus", "ventoy", "malwarebytes",
            "kaspersky", "avast", "eset", "defender", "driver", "translucenttb", "rainmeter", "lively", "explorer", "проводник", "wallflow" })
    };

    public const string Other = "Другое";

    /// <summary>Set by the app: translates category names for new folders (interface language).</summary>
    public static Func<string, string> Translate { get; set; } = s => s;

    /// <summary>Which kinds of plain desktop items go into glass folders (user setting; shortcuts always do).</summary>
    public static bool KindEnabled(string kind)
    {
        var d = SettingsStore.Current.DesktopFiles;
        return kind switch
        {
            "Документы" => d.Documents,
            "Картинки" or Screens => d.Images,
            "Архивы" => d.Archives,
            Dirs => d.Folders,
            "Видео" or "Музыка" => d.Media,
            "Установщики" or "Код" or "Торренты" or Files => d.Other,
            _ => true
        };
    }

    /// <summary>Should this desktop item appear in a glass folder with the current settings?</summary>
    public static bool Allowed(string path) => IsApp(path) || KindEnabled(Categorize(path));

    public static string Categorize(string path)
    {
        if (Directory.Exists(path)) return Dirs;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!AppExts.Contains(ext)) return KindOf(path);
        // a shortcut to a folder or a document belongs with folders/documents, not with apps
        if (ext == ".lnk")
        {
            var t = ResolveTarget(path);
            if (!string.IsNullOrEmpty(t) && !t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(t) && KindEnabled(Dirs)) return Dirs;
                if (File.Exists(t)) { var k = KindOf(t); if (k != Files && KindEnabled(k)) return k; }
            }
        }
        if (ext == ".exe" && Path.GetFileName(path) is var fn && (fn.Contains("setup", StringComparison.OrdinalIgnoreCase) || fn.Contains("install", StringComparison.OrdinalIgnoreCase))
            && KindEnabled("Установщики"))
            return "Установщики";
        return AppCategory(path);
    }

    static string KindOf(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        foreach (var (name, exts) in FileKinds)
            if (exts.Contains(ext))
            {
                if (name == "Картинки")
                {
                    var n = Path.GetFileName(path).ToLowerInvariant();
                    if (ScreenWords.Any(n.Contains)) return Screens;
                }
                return name;
            }
        return Files;
    }

    static string AppCategory(string path)
    {
        var name = DisplayName(path).ToLowerInvariant();
        var target = (ResolveTarget(path) ?? "").ToLowerInvariant();

        // strong signals first
        if (target.StartsWith("steam://") || target.Contains(@"\steamapps\") || target.Contains("com.epicgames.launcher://")
            || target.Contains(@"\epic games\") || target.Contains(@"\riot games\") || target.Contains(@"\games\")
            || target.Contains("uplay://") || target.Contains("battlenet://") || target.Contains("origin2://"))
            return "Игры";

        var hay = name + " | " + Path.GetFileName(target);
        foreach (var c in Categories)
            foreach (var k in c.Keywords)
                if (Matches(hay, k)) return c.Name;
        return Other;
    }

    static bool Matches(string hay, string keyword)
    {
        // short keywords (code, git, arc, obs…) must match a whole word, longer ones can be substrings
        if (keyword.Length > 4) return hay.Contains(keyword);
        int i = 0;
        while ((i = hay.IndexOf(keyword, i, StringComparison.Ordinal)) >= 0)
        {
            bool l = i == 0 || !char.IsLetterOrDigit(hay[i - 1]);
            int e = i + keyword.Length;
            bool r = e >= hay.Length || !char.IsLetterOrDigit(hay[e]);
            if (l && r) return true;
            i = e;
        }
        return false;
    }

    /// <summary>Builds folders from the current desktop. Category order is fixed, empty ones are skipped.</summary>
    public static List<FolderDef> AutoArrange(IEnumerable<string> files)
    {
        var groups = files.GroupBy(Categorize).ToDictionary(g => g.Key, g => g.ToList());
        var order = Categories.Select(c => c.Name).Append(Other)
            .Concat(new[] { Dirs, "Документы", "Картинки", Screens, "Видео", "Музыка", "Архивы", "Установщики", "Код", "Торренты", Files });
        return order.Where(groups.ContainsKey)
            .Select(n => new FolderDef { Name = Translate(n), Items = groups[n] })
            .ToList();
    }

    /// <summary>Returns shortcuts on the desktop that are not in any folder yet.</summary>
    public static List<string> FindNew(IEnumerable<FolderDef> folders)
    {
        var known = new HashSet<string>(folders.SelectMany(f => f.Items), StringComparer.OrdinalIgnoreCase);
        return ScanDesktop().Where(f => !known.Contains(f)).ToList();
    }

    // ---------------- shortcut target ----------------
    public static string? ResolveTarget(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".url")
            {
                foreach (var line in File.ReadLines(path))
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) return line[4..];
                return null;
            }
            if (ext == ".lnk" && OperatingSystem.IsWindows())
            {
                var link = (IShellLinkW)new ShellLink();
                ((IPersistFile)link).Load(path, 0);
                var sb = new StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                Marshal.FinalReleaseComObject(link);
                return sb.ToString();
            }
            return path;
        }
        catch { return null; }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr fd, int flags);
        // remaining methods unused
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    interface IPersistFile
    {
        void GetClassID(out Guid clsid);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string file, int mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string file, bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string file);
    }
}
