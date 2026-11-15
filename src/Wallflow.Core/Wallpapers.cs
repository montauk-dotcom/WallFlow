using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wallflow.Core;

public sealed class Wallpaper
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "wallhaven"; // wallhaven | bing
    public string Thumb { get; set; } = "";
    public string Full { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Ratio { get; set; } = "";
    public List<string> Colors { get; set; } = new();
    public long Size { get; set; }
    public int Views { get; set; }
    public int Favs { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Page { get; set; } = "";

    [JsonIgnore] public string Resolution => $"{Width}×{Height}";
    [JsonIgnore] public string Badge
    {
        get
        {
            var m = Math.Max(Width, Height);
            return m >= 7680 ? "8K" : m >= 5120 ? "5K" : m >= 3840 ? "4K" : m >= 2560 ? "2K" : m >= 1920 ? "FHD" : "HD";
        }
    }
    [JsonIgnore] public string Key => $"{Source}:{Id}";
}

public sealed class SearchFilters
{
    public string Query { get; set; } = "";
    /// <summary>atleast | exact</summary>
    public string ResolutionMode { get; set; } = "atleast";
    public string AtLeast { get; set; } = "2560x1440";
    public List<string> Resolutions { get; set; } = new();
    public List<string> Ratios { get; set; } = new();
    public string Color { get; set; } = "";
    /// <summary>Topic keyword chosen from the chips; combined with the typed query.</summary>
    public string Topic { get; set; } = "";
    public bool General { get; set; } = true;
    public bool Anime { get; set; } = true;
    public bool People { get; set; } = true;
    public string Sorting { get; set; } = "toplist";
    public string TopRange { get; set; } = "1M";

    public SearchFilters Clone()
    {
        var c = (SearchFilters)MemberwiseClone();
        c.Resolutions = new(Resolutions);
        c.Ratios = new(Ratios);
        return c;
    }
}

public sealed record SearchPage(List<Wallpaper> Items, int Page, int LastPage, int Total, string? Seed);

public static class Http
{
    public static readonly HttpClient Client = Create();
    static HttpClient Create()
    {
        var c = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30)
        }) { Timeout = TimeSpan.FromSeconds(60) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Wallflow/2.0 (+https://github.com/montauk-dotcom/wallflow)");
        return c;
    }

    public static async Task<JsonDocument> GetJson(string url, CancellationToken ct = default)
    {
        using var res = await Client.GetAsync(url, ct);
        if ((int)res.StatusCode == 429)
            throw new InvalidOperationException("Слишком много запросов (лимит Wallhaven 45/мин). Подожди минуту.");
        res.EnsureSuccessStatusCode();
        await using var s = await res.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(s, cancellationToken: ct);
    }
}

public static class WallhavenApi
{
    const string Base = "https://wallhaven.cc/api/v1";

    public static string BuildQuery(SearchFilters f, int page, string? seed, string apiKey)
    {
        var p = new List<string>();
        void Add(string k, string v) => p.Add($"{k}={Uri.EscapeDataString(v)}");
        var q = $"{f.Topic} {f.Query}".Trim();
        if (q.Length > 0) Add("q", q);
        Add("categories", $"{(f.General ? 1 : 0)}{(f.Anime ? 1 : 0)}{(f.People ? 1 : 0)}");
        Add("purity", "100"); // SFW only
        Add("sorting", f.Sorting);
        Add("order", "desc");
        if (f.Sorting == "toplist") Add("topRange", f.TopRange);
        if (f.ResolutionMode == "exact" && f.Resolutions.Count > 0) Add("resolutions", string.Join(",", f.Resolutions));
        else if (f.ResolutionMode != "exact" && !string.IsNullOrEmpty(f.AtLeast)) Add("atleast", f.AtLeast);
        if (f.Ratios.Count > 0) Add("ratios", string.Join(",", f.Ratios));
        if (!string.IsNullOrEmpty(f.Color)) Add("colors", f.Color);
        if (!string.IsNullOrEmpty(seed)) Add("seed", seed);
        Add("page", page.ToString());
        if (!string.IsNullOrWhiteSpace(apiKey)) Add("apikey", apiKey.Trim());
        return string.Join("&", p);
    }

    public static async Task<SearchPage> Search(SearchFilters f, int page = 1, string? seed = null, CancellationToken ct = default)
    {
        using var doc = await Http.GetJson($"{Base}/search?{BuildQuery(f, page, seed, SettingsStore.Current.WallhavenApiKey)}", ct);
        var root = doc.RootElement;
        var list = new List<Wallpaper>();
        foreach (var w in root.GetProperty("data").EnumerateArray())
        {
            list.Add(new Wallpaper
            {
                Id = w.GetProperty("id").GetString()!,
                Source = "wallhaven",
                Thumb = w.GetProperty("thumbs").GetProperty("large").GetString()!,
                Full = w.GetProperty("path").GetString()!,
                Width = w.GetProperty("dimension_x").GetInt32(),
                Height = w.GetProperty("dimension_y").GetInt32(),
                Ratio = w.TryGetProperty("ratio", out var r) ? r.ToString() : "",
                Colors = w.TryGetProperty("colors", out var c) ? c.EnumerateArray().Select(x => x.GetString()!).ToList() : new(),
                Size = w.TryGetProperty("file_size", out var s) ? s.GetInt64() : 0,
                Views = w.TryGetProperty("views", out var v) ? v.GetInt32() : 0,
                Favs = w.TryGetProperty("favorites", out var fv) ? fv.GetInt32() : 0,
                Category = w.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "",
                Page = w.TryGetProperty("url", out var u) ? u.GetString() ?? "" : ""
            });
        }
        var meta = root.GetProperty("meta");
        string? newSeed = meta.TryGetProperty("seed", out var sd) && sd.ValueKind == JsonValueKind.String ? sd.GetString() : null;
        return new SearchPage(list,
            meta.GetProperty("current_page").GetInt32(),
            meta.GetProperty("last_page").GetInt32(),
            meta.TryGetProperty("total", out var t) ? t.GetInt32() : list.Count,
            newSeed);
    }

    public static async Task<List<string>> Tags(string id, CancellationToken ct = default)
    {
        var key = SettingsStore.Current.WallhavenApiKey;
        using var doc = await Http.GetJson($"{Base}/w/{id}{(string.IsNullOrWhiteSpace(key) ? "" : "?apikey=" + key)}", ct);
        return doc.RootElement.GetProperty("data").GetProperty("tags").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();
    }
}

public static class BingApi
{
    public static async Task<List<Wallpaper>> Daily(string market = "en-US", CancellationToken ct = default)
    {
        var res = new List<Wallpaper>();
        foreach (var idx in new[] { 0, 8 })
        {
            try
            {
                using var doc = await Http.GetJson($"https://www.bing.com/HPImageArchive.aspx?format=js&idx={idx}&n=8&mkt={market}", ct);
                foreach (var im in doc.RootElement.GetProperty("images").EnumerateArray())
                {
                    var id = im.GetProperty("hsh").GetString()!;
                    if (res.Any(x => x.Id == id)) continue;
                    var b = "https://www.bing.com" + im.GetProperty("urlbase").GetString();
                    res.Add(new Wallpaper
                    {
                        Id = id, Source = "bing",
                        Thumb = b + "_800x480.jpg", Full = b + "_UHD.jpg",
                        Width = 3840, Height = 2160, Ratio = "1.78",
                        Title = im.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Category = im.TryGetProperty("copyright", out var c) ? c.GetString() ?? "" : "",
                        Page = im.TryGetProperty("copyrightlink", out var l) ? l.GetString() ?? "" : ""
                    });
                }
            }
            catch when (res.Count > 0) { }
        }
        return res;
    }
}

public static class WallpaperService
{
    public static async Task<string> Download(Wallpaper w, CancellationToken ct = default)
    {
        var dir = SettingsStore.Current.ResolvedSaveDir;
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(new Uri(w.Full).AbsolutePath);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var file = Path.Combine(dir, $"{w.Source}-{w.Id}{ext}");
        if (File.Exists(file) && new FileInfo(file).Length > 0) return file;
        var tmp = file + ".part";
        using (var res = await Http.Client.GetAsync(w.Full, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            res.EnsureSuccessStatusCode();
            await using var fs = File.Create(tmp);
            await res.Content.CopyToAsync(fs, ct);
        }
        File.Move(tmp, file, true);
        return file;
    }

    public static async Task<string> Apply(Wallpaper w, CancellationToken ct = default)
    {
        var file = await Download(w, ct);
        Native.SetWallpaper(file, SettingsStore.Current.WallpaperStyle);
        return file;
    }

    static readonly Random Rng = new();

    public static async Task<Wallpaper?> PickNext(CancellationToken ct = default)
    {
        var s = SettingsStore.Current;
        switch (s.Slideshow.Source)
        {
            case "favorites" when s.Favorites.Count > 0:
                return s.Favorites[Rng.Next(s.Favorites.Count)];
            case "bing":
                var b = await BingApi.Daily(ct: ct);
                return b.Count > 0 ? b[Rng.Next(b.Count)] : null;
            default:
                var f = s.LastFilters.Clone();
                f.Sorting = "random";
                var p = await WallhavenApi.Search(f, 1, null, ct);
                return p.Items.Count > 0 ? p.Items[Rng.Next(p.Items.Count)] : null;
        }
    }
}
