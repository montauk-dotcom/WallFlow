using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wallflow.Core;

/// <summary>A video wallpaper found through one of the open APIs.</summary>
public sealed class LiveWallpaper
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";   // nasa | pexels | pixabay
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Thumb { get; set; } = "";
    public string Url { get; set; } = "";      // direct mp4 (empty for NASA until resolved)
    public int Width { get; set; }
    public int Height { get; set; }
    public int Duration { get; set; }
    public string Page { get; set; } = "";

    [JsonIgnore] public string Key => $"{Source}:{Id}";
    [JsonIgnore] public string Badge
    {
        get
        {
            if (Width == 0) return "HD";
            var m = Math.Max(Width, Height);
            return m >= 3840 ? "4K" : m >= 2560 ? "2K" : m >= 1920 ? "FHD" : "HD";
        }
    }
}

public sealed class LiveFilters
{
    public string Source { get; set; } = "nasa";
    public string Query { get; set; } = "";
    /// <summary>0 any, 1280, 1920, 2560, 3840</summary>
    public int MinWidth { get; set; } = 1920;
    /// <summary>landscape | portrait | any</summary>
    public string Orientation { get; set; } = "landscape";
    /// <summary>popular | latest</summary>
    public string Order { get; set; } = "popular";
}

public sealed record LivePage(List<LiveWallpaper> Items, bool HasMore, int Total);

public static class LiveApi
{
    public static readonly (string Key, string Title, bool NeedsKey)[] Sources =
    {
        ("nasa", "NASA — космос и Земля (без ключа)", false),
        ("pexels", "Pexels (бесплатный ключ)", true),
        ("pixabay", "Pixabay (бесплатный ключ)", true)
    };

    public static Task<LivePage> Search(LiveFilters f, int page, CancellationToken ct = default) => f.Source switch
    {
        "pexels" => Pexels(f, page, ct),
        "pixabay" => Pixabay(f, page, ct),
        _ => Nasa(f, page, ct)
    };

    // ---------------- NASA Image and Video Library (no key) ----------------
    static async Task<LivePage> Nasa(LiveFilters f, int page, CancellationToken ct)
    {
        var q = string.IsNullOrWhiteSpace(f.Query) ? "earth from space" : f.Query.Trim();
        using var doc = await Http.GetJson($"https://images-api.nasa.gov/search?q={Uri.EscapeDataString(q)}&media_type=video&page={page}&page_size=30", ct);
        var col = doc.RootElement.GetProperty("collection");
        var list = new List<LiveWallpaper>();
        foreach (var it in col.GetProperty("items").EnumerateArray())
        {
            var data = it.GetProperty("data")[0];
            string thumb = "";
            if (it.TryGetProperty("links", out var links))
                foreach (var l in links.EnumerateArray())
                    if (l.TryGetProperty("href", out var h) && h.GetString() is { } hs && (hs.EndsWith(".jpg") || hs.EndsWith(".png"))) { thumb = hs; break; }
            var id = data.GetProperty("nasa_id").GetString()!;
            list.Add(new LiveWallpaper
            {
                Id = id, Source = "nasa",
                Title = data.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                Author = data.TryGetProperty("center", out var c) ? "NASA " + c.GetString() : "NASA",
                Thumb = thumb,
                Page = $"https://images.nasa.gov/details/{Uri.EscapeDataString(id)}"
            });
        }
        int total = col.TryGetProperty("metadata", out var md) && md.TryGetProperty("total_hits", out var th) ? th.GetInt32() : list.Count;
        bool more = col.TryGetProperty("links", out var nav) && nav.EnumerateArray().Any(x => x.TryGetProperty("rel", out var r) && r.GetString() == "next");
        return new LivePage(list.Where(x => x.Thumb != "").ToList(), more, total);
    }

    /// <summary>NASA search results don't contain the video file — ask the asset manifest for the best mp4.</summary>
    public static async Task<string> ResolveNasa(LiveWallpaper w, CancellationToken ct = default)
    {
        using var doc = await Http.GetJson($"https://images-api.nasa.gov/asset/{Uri.EscapeDataString(w.Id)}", ct);
        var files = doc.RootElement.GetProperty("collection").GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("href").GetString() ?? "")
            .Where(h => h.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)).ToList();
        string? Pick(string tag) => files.FirstOrDefault(h => h.Contains(tag, StringComparison.OrdinalIgnoreCase));
        var url = Pick("~large.mp4") ?? Pick("~orig.mp4") ?? Pick("~medium.mp4") ?? files.FirstOrDefault()
                  ?? throw new InvalidOperationException("У этого видео нет mp4-файла");
        return url.Replace("http://", "https://");
    }

    // ---------------- Pexels ----------------
    static async Task<LivePage> Pexels(LiveFilters f, int page, CancellationToken ct)
    {
        var key = SettingsStore.Current.Live.PexelsKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Нужен бесплатный ключ Pexels: pexels.com/api → Get Started. Вставь его в поле ключа справа.");
        string size = f.MinWidth >= 3840 ? "&size=large" : f.MinWidth >= 1920 ? "&size=medium" : "";
        string orient = f.Orientation is "landscape" or "portrait" ? "&orientation=" + f.Orientation : "";
        var url = string.IsNullOrWhiteSpace(f.Query)
            ? $"https://api.pexels.com/videos/popular?per_page=30&page={page}&min_width={f.MinWidth}"
            : $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(f.Query.Trim())}&per_page=30&page={page}{size}{orient}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", key.Trim());
        using var res = await Http.Client.SendAsync(req, ct);
        if ((int)res.StatusCode is 401 or 403) throw new InvalidOperationException("Pexels не принял ключ — проверь его.");
        res.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var list = new List<LiveWallpaper>();
        foreach (var v in doc.RootElement.GetProperty("videos").EnumerateArray())
        {
            var files = v.GetProperty("video_files").EnumerateArray()
                .Where(x => x.TryGetProperty("file_type", out var ft) && ft.GetString() == "video/mp4"
                            && x.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number)
                .Select(x => (w: x.GetProperty("width").GetInt32(), h: x.GetProperty("height").GetInt32(), link: x.GetProperty("link").GetString()!))
                .OrderBy(x => x.w).ToList();
            if (files.Count == 0) continue;
            // smallest file that is still at least as wide as asked (and not above 4K), else the biggest
            var best = files.FirstOrDefault(x => x.w >= Math.Max(f.MinWidth, 1920) && x.w <= 3840);
            if (best.link is null) best = files.Last();
            if (best.w < f.MinWidth) continue;
            if (f.Orientation == "landscape" && best.w < best.h) continue;
            if (f.Orientation == "portrait" && best.w > best.h) continue;
            list.Add(new LiveWallpaper
            {
                Id = v.GetProperty("id").GetInt64().ToString(), Source = "pexels",
                Title = Slug(v.GetProperty("url").GetString() ?? ""),
                Author = v.TryGetProperty("user", out var u) && u.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Thumb = v.GetProperty("image").GetString() ?? "", Url = best.link,
                Width = best.w, Height = best.h, Duration = v.GetProperty("duration").GetInt32(),
                Page = v.GetProperty("url").GetString() ?? ""
            });
        }
        int total = doc.RootElement.TryGetProperty("total_results", out var tr) ? tr.GetInt32() : list.Count;
        bool more = doc.RootElement.TryGetProperty("next_page", out var np) && np.ValueKind == JsonValueKind.String;
        return new LivePage(list, more, total);
    }

    static string Slug(string url)
    {
        // https://www.pexels.com/video/waves-crashing-on-the-shore-1234/ -> "Waves crashing on the shore"
        var seg = url.TrimEnd('/').Split('/').LastOrDefault() ?? "";
        var parts = seg.Split('-').ToList();
        if (parts.Count > 1 && parts[^1].All(char.IsDigit)) parts.RemoveAt(parts.Count - 1);
        var s = string.Join(' ', parts);
        return s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : "Pexels";
    }

    // ---------------- Pixabay ----------------
    static async Task<LivePage> Pixabay(LiveFilters f, int page, CancellationToken ct)
    {
        var key = SettingsStore.Current.Live.PixabayKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Нужен бесплатный ключ Pixabay: pixabay.com/api/docs (после входа ключ на этой странице). Вставь его в поле ключа справа.");
        var url = $"https://pixabay.com/api/videos/?key={Uri.EscapeDataString(key.Trim())}&q={Uri.EscapeDataString(f.Query.Trim())}" +
                  $"&per_page=30&page={page}&safesearch=true&order={(f.Order == "latest" ? "latest" : "popular")}" +
                  (f.MinWidth > 0 ? $"&min_width={f.MinWidth}" : "");
        using var res = await Http.Client.GetAsync(url, ct);
        if ((int)res.StatusCode is 400 or 401 or 403) throw new InvalidOperationException("Pixabay не принял ключ или запрос.");
        res.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var list = new List<LiveWallpaper>();
        foreach (var h in doc.RootElement.GetProperty("hits").EnumerateArray())
        {
            var vids = h.GetProperty("videos");
            (int w, int hgt, string url, string thumb)? pick = null;
            foreach (var q in new[] { "large", "medium", "small" })
            {
                if (!vids.TryGetProperty(q, out var v) || v.GetProperty("url").GetString() is not { Length: > 0 } u) continue;
                int w = v.GetProperty("width").GetInt32(), hh = v.GetProperty("height").GetInt32();
                var th = v.TryGetProperty("thumbnail", out var t) ? t.GetString() ?? "" : "";
                if (pick is null || (w >= Math.Max(f.MinWidth, 1920) && w <= 3840)) pick = (w, hh, u, th);
                if (w <= 3840 && w >= Math.Max(f.MinWidth, 1920)) break;
            }
            if (pick is not { } p || p.w < f.MinWidth) continue;
            if (f.Orientation == "landscape" && p.w < p.hgt) continue;
            if (f.Orientation == "portrait" && p.w > p.hgt) continue;
            var tags = h.TryGetProperty("tags", out var tg) ? tg.GetString() ?? "" : "";
            list.Add(new LiveWallpaper
            {
                Id = h.GetProperty("id").GetInt64().ToString(), Source = "pixabay",
                Title = tags.Length > 0 ? char.ToUpper(tags[0]) + tags[1..] : "Pixabay",
                Author = h.TryGetProperty("user", out var us) ? us.GetString() ?? "" : "",
                Thumb = p.thumb, Url = p.url, Width = p.w, Height = p.hgt,
                Duration = h.TryGetProperty("duration", out var d) ? d.GetInt32() : 0,
                Page = h.TryGetProperty("pageURL", out var pu) ? pu.GetString() ?? "" : ""
            });
        }
        int total = doc.RootElement.TryGetProperty("totalHits", out var th2) ? th2.GetInt32() : list.Count;
        return new LivePage(list, page * 30 < total, total);
    }

    // ---------------- download ----------------
    public static string LiveDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Wallflow Live");

    public static async Task<string> Download(LiveWallpaper w, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(w.Url) && w.Source == "nasa") w.Url = await ResolveNasa(w, ct);
        Directory.CreateDirectory(LiveDir);
        var file = Path.Combine(LiveDir, $"{w.Source}-{string.Concat(w.Id.Split(Path.GetInvalidFileNameChars()))}.mp4");
        if (File.Exists(file) && new FileInfo(file).Length > 0) return file;
        var tmp = file + ".part";
        using (var res = await Http.Client.GetAsync(w.Url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            res.EnsureSuccessStatusCode();
            var total = res.Content.Headers.ContentLength ?? 0;
            await using var src = await res.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(tmp);
            var buf = new byte[1 << 16];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress?.Report((double)done / total);
            }
        }
        File.Move(tmp, file, true);
        return file;
    }

    /// <summary>Saves the preview image too — it becomes the normal wallpaper (used for the glass and when the app is closed).</summary>
    public static async Task<string?> DownloadThumb(LiveWallpaper w, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(w.Thumb)) return null;
        Directory.CreateDirectory(LiveDir);
        var file = Path.Combine(LiveDir, $"{w.Source}-{string.Concat(w.Id.Split(Path.GetInvalidFileNameChars()))}.jpg");
        if (!File.Exists(file))
            await File.WriteAllBytesAsync(file, await Http.Client.GetByteArrayAsync(w.Thumb.Replace("http://", "https://"), ct), ct);
        return file;
    }
}
