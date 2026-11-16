using System.Runtime.InteropServices;
using System.Text;

namespace Wallflow.Core;

/// <summary>Finds desktop shortcuts and sorts them into iOS-style folders.</summary>
public static class Shortcuts
{
    static readonly string[] Exts = { ".lnk", ".url", ".exe", ".appref-ms" };

    public static IEnumerable<string> DesktopDirs()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }

    public static List<string> ScanDesktop() =>
        DesktopDirs()
            .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
            .SelectMany(d => Directory.EnumerateFiles(d))
            .Where(f => Exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public static string DisplayName(string path) => Path.GetFileNameWithoutExtension(path);

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

    public static string Categorize(string path)
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
        var order = Categories.Select(c => c.Name).Append(Other);
        return order.Where(groups.ContainsKey)
            .Select(n => new FolderDef { Name = n, Items = groups[n] })
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
