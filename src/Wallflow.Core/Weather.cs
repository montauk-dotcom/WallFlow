using System.Globalization;
using System.Text.Json;

namespace Wallflow.Core;

public enum Sky { Clear, PartlyCloudy, Cloudy, Fog, Drizzle, Rain, Snow, Thunder }

public sealed record DayForecast(DateTime Date, Sky Sky, double Max, double Min);

public sealed record WeatherNow(
    string City, double Temp, double Feels, int Humidity, double Wind,
    Sky Sky, bool IsDay, string Description, List<DayForecast> Days, DateTime Updated);

/// <summary>Open-Meteo: free, no API key.</summary>
public static class WeatherApi
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static async Task<(double lat, double lon, string name)> Geocode(string city, string lang, CancellationToken ct = default)
    {
        using var doc = await Http.GetJson(
            $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language={lang}&format=json", ct);
        if (!doc.RootElement.TryGetProperty("results", out var r) || r.GetArrayLength() == 0)
            throw new InvalidOperationException($"Город «{city}» не найден");
        var g = r[0];
        return (g.GetProperty("latitude").GetDouble(), g.GetProperty("longitude").GetDouble(), g.GetProperty("name").GetString() ?? city);
    }

    public static async Task<WeatherNow> Get(WidgetSettings w, CancellationToken ct = default)
    {
        if (w.Lat is null || w.Lon is null || string.IsNullOrEmpty(w.CityResolved))
        {
            var (la, lo, name) = await Geocode(w.City, w.Language, ct);
            w.Lat = la; w.Lon = lo; w.CityResolved = name;
            SettingsStore.Save();
        }
        var url = "https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={w.Lat!.Value.ToString(Inv)}&longitude={w.Lon!.Value.ToString(Inv)}" +
                  "&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code,is_day" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=5&timezone=auto&wind_speed_unit=ms";
        using var doc = await Http.GetJson(url, ct);
        var cur = doc.RootElement.GetProperty("current");
        var code = cur.GetProperty("weather_code").GetInt32();
        var daily = doc.RootElement.GetProperty("daily");
        var days = new List<DayForecast>();
        var times = daily.GetProperty("time");
        for (int i = 0; i < times.GetArrayLength(); i++)
        {
            days.Add(new DayForecast(
                DateTime.ParseExact(times[i].GetString()!, "yyyy-MM-dd", Inv),
                Map(daily.GetProperty("weather_code")[i].GetInt32()),
                daily.GetProperty("temperature_2m_max")[i].GetDouble(),
                daily.GetProperty("temperature_2m_min")[i].GetDouble()));
        }
        return new WeatherNow(
            w.CityResolved,
            cur.GetProperty("temperature_2m").GetDouble(),
            cur.GetProperty("apparent_temperature").GetDouble(),
            (int)Math.Round(cur.GetProperty("relative_humidity_2m").GetDouble()),
            cur.GetProperty("wind_speed_10m").GetDouble(),
            Map(code), cur.GetProperty("is_day").GetInt32() == 1,
            Describe(code, w.Language), days, DateTime.Now);
    }

    public static Sky Map(int c) => c switch
    {
        0 or 1 => Sky.Clear,
        2 => Sky.PartlyCloudy,
        3 => Sky.Cloudy,
        45 or 48 => Sky.Fog,
        51 or 53 or 55 or 56 or 57 => Sky.Drizzle,
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => Sky.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => Sky.Snow,
        95 or 96 or 99 => Sky.Thunder,
        _ => Sky.Cloudy
    };

    public static string Describe(int c, string lang)
    {
        bool ru = lang == "ru";
        return c switch
        {
            0 => ru ? "Ясно" : "Clear",
            1 => ru ? "Преимущественно ясно" : "Mostly clear",
            2 => ru ? "Переменная облачность" : "Partly cloudy",
            3 => ru ? "Пасмурно" : "Overcast",
            45 or 48 => ru ? "Туман" : "Fog",
            51 or 53 or 55 => ru ? "Морось" : "Drizzle",
            56 or 57 => ru ? "Ледяная морось" : "Freezing drizzle",
            61 or 80 => ru ? "Небольшой дождь" : "Light rain",
            63 or 81 => ru ? "Дождь" : "Rain",
            65 or 82 => ru ? "Ливень" : "Heavy rain",
            66 or 67 => ru ? "Ледяной дождь" : "Freezing rain",
            71 or 85 => ru ? "Небольшой снег" : "Light snow",
            73 => ru ? "Снег" : "Snow",
            75 or 86 => ru ? "Снегопад" : "Heavy snow",
            77 => ru ? "Снежная крупа" : "Snow grains",
            95 => ru ? "Гроза" : "Thunderstorm",
            96 or 99 => ru ? "Гроза с градом" : "Thunderstorm, hail",
            _ => ""
        };
    }
}

public static class Lang
{
    public static CultureInfo Culture(string lang) => CultureInfo.GetCultureInfo(lang == "ru" ? "ru-RU" : "en-US");

    public static string DayName(DateTime d, string lang)
    {
        var s = Culture(lang).DateTimeFormat.GetDayName(d.DayOfWeek);
        return char.ToUpper(s[0]) + s[1..];
    }

    public static string ShortDay(DateTime d, string lang)
    {
        var s = Culture(lang).DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek);
        return char.ToUpper(s[0]) + s[1..];
    }

    public static string MonthYear(DateTime d, string lang)
    {
        var c = Culture(lang);
        var m = c.DateTimeFormat.GetMonthName(d.Month);
        return char.ToUpper(m[0]) + m[1..] + " " + d.Year;
    }

    /// <summary>"15 November" / "15 ноября"</summary>
    public static string DayMonth(DateTime d, string lang)
    {
        var c = Culture(lang);
        var m = lang == "ru" ? c.DateTimeFormat.MonthGenitiveNames[d.Month - 1] : c.DateTimeFormat.GetMonthName(d.Month);
        return $"{d.Day} {m}";
    }
}
