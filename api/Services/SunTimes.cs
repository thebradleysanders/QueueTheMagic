namespace XlightsQueue.Services;

/// <summary>
/// Sunrise, sunset and twilight for FPP's sun-relative schedule times (Dawn, SunRise, SunSet, Dusk).
/// NOAA / Almanac algorithm, accurate to about a minute, which is plenty for a show schedule.
/// Results are in the server's local time zone (set TZ in Docker).
/// </summary>
public static class SunTimes {
    private const double Official = 90.833; // sunrise / sunset
    private const double Civil = 96.0;      // dawn / dusk (sun 6 degrees below the horizon)

    /// <summary>True if the text is a sun-relative label rather than a clock time.</summary>
    public static bool IsSunLabel(string? text) => Kind(text) != null;

    private static string? Kind(string? text) => text?.Trim().ToLowerInvariant().Replace(" ", "") switch {
        "dawn" => "dawn",
        "sunrise" => "sunrise",
        "sunset" => "sunset",
        "dusk" => "dusk",
        _ => null
    };

    /// <summary>Local time of the event on <paramref name="date"/>, or null if the sun never crosses it that day.</summary>
    public static TimeOnly? Resolve(string? label, DateOnly date, double lat, double lon) {
        var kind = Kind(label);
        if (kind == null) return null;
        var rising = kind is "dawn" or "sunrise";
        var zenith = kind is "dawn" or "dusk" ? Civil : Official;
        return Event(date, lat, lon, zenith, rising);
    }

    private static TimeOnly? Event(DateOnly date, double lat, double lon, double zenith, bool rising) {
        static double Rad(double d) => d * Math.PI / 180;
        static double Deg(double r) => r * 180 / Math.PI;
        static double Mod(double v, double m) => ((v % m) + m) % m;

        var n = date.DayOfYear;
        var lngHour = lon / 15.0;
        var t = n + ((rising ? 6 : 18) - lngHour) / 24.0;
        var m = 0.9856 * t - 3.289;
        var l = Mod(m + 1.916 * Math.Sin(Rad(m)) + 0.020 * Math.Sin(Rad(2 * m)) + 282.634, 360);

        var ra = Mod(Deg(Math.Atan(0.91764 * Math.Tan(Rad(l)))), 360);
        ra += Math.Floor(l / 90) * 90 - Math.Floor(ra / 90) * 90;
        ra /= 15;

        var sinDec = 0.39782 * Math.Sin(Rad(l));
        var cosDec = Math.Cos(Math.Asin(sinDec));
        var cosH = (Math.Cos(Rad(zenith)) - sinDec * Math.Sin(Rad(lat))) / (cosDec * Math.Cos(Rad(lat)));
        if (cosH is > 1 or < -1) return null; // polar day / night

        var h = (rising ? 360 - Deg(Math.Acos(cosH)) : Deg(Math.Acos(cosH))) / 15;
        var localMean = h + ra - 0.06571 * t - 6.622;
        var utc = Mod(localMean - lngHour, 24);

        var offset = TimeZoneInfo.Local.GetUtcOffset(date.ToDateTime(new TimeOnly(12, 0))).TotalHours;
        var local = Mod(utc + offset, 24);
        return TimeOnly.FromTimeSpan(TimeSpan.FromHours(local));
    }
}
