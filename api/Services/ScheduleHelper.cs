using System.Text.Json;
using XlightsQueue.DTOs;
using XlightsQueue.Models;

namespace XlightsQueue.Services;

public static class ScheduleHelper {
    public const string ModeAuto = "auto";
    public const string ModeOn = "on";
    public const string ModeOff = "off";

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Normalises user/MQTT input to auto|on|off, or null if it isn't recognised.</summary>
    public static string? ParseMode(string? raw) {
        switch (raw?.Trim().ToLowerInvariant()) {
            case "auto": case "automatic": case "schedule": return ModeAuto;
            case "on": case "open": case "true": case "1": return ModeOn;
            case "off": case "closed": case "close": case "false": case "0": return ModeOff;
            default: return null;
        }
    }

    /// <summary>
    /// Is the season running? "on" and "off" are manual overrides; "auto" checks the season
    /// start/end dates (inclusive, server-local time). Missing dates mean no limit.
    /// </summary>
    public static bool IsSeasonActive(ShowConfig config, DateTime? now = null) {
        switch (config.ShowMode) {
            case ModeOn: return true;
            case ModeOff: return false;
        }
        var today = DateOnly.FromDateTime(now ?? DateTime.Now);
        if (config.SeasonStart is { } s && today < s) return false;
        if (config.SeasonEnd is { } e && today > e) return false;
        return true;
    }

    /// <summary>Is the show open right now? A manual "on" ignores the weekly hours.</summary>
    public static bool IsOpen(ShowConfig config, DateTime? now = null) => config.ShowMode switch {
        ModeOn => true,
        ModeOff => false,
        _ => IsWithinSchedule(config.ShowScheduleJson, config.Latitude, config.Longitude, now ?? DateTime.Now)
    };

    /// <summary>Season is running AND the show is inside its hours. Used for gating requests and playback.</summary>
    public static bool IsRunning(ShowConfig config) => IsSeasonActive(config) && IsOpen(config);

    /// <summary>True if the schedule uses Dawn/Sunrise/Sunset/Dusk but no location is set to work them out.</summary>
    public static bool NeedsLocation(ShowConfig config) {
        if (config.Latitude != null && config.Longitude != null) return false;
        return ParseSchedule(config.ShowScheduleJson).Any(e => e.Enabled && (SunTimes.IsSunLabel(e.Start) || SunTimes.IsSunLabel(e.End)));
    }

    private static List<ShowScheduleEntryDto> ParseSchedule(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<ShowScheduleEntryDto>>(json, _jsonOpts) ?? []; } catch { return []; }
    }

    /// <summary>
    /// Is <paramref name="now"/> inside the weekly schedule? No schedule means always open.
    /// Handles overnight windows (a Monday 17:00 to 01:00 window is still open at 00:30 Tuesday),
    /// "24:00" ends, and sun-relative times with offsets. A sun-relative time that can't be worked out
    /// (no location set) counts as closed, so the default playlist never runs outside the real schedule.
    /// </summary>
    public static bool IsWithinSchedule(string? scheduleJson, double? lat, double? lon, DateTime now) {
        var schedule = ParseSchedule(scheduleJson);
        if (schedule.Count == 0) return true;

        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);

        // Today's window, then yesterday's window if it ran past midnight.
        if (WindowFor(schedule, today, lat, lon) is { } w && Inside(w, time)) return true;
        if (WindowFor(schedule, today.AddDays(-1), lat, lon) is { } y && y.Start > y.End && time <= y.End) return true;
        return false;
    }

    private static bool Inside((TimeOnly Start, TimeOnly End) w, TimeOnly t) =>
        w.Start <= w.End ? t >= w.Start && t <= w.End : t >= w.Start;

    private static (TimeOnly Start, TimeOnly End)? WindowFor(List<ShowScheduleEntryDto> schedule, DateOnly date, double? lat, double? lon) {
        var entry = schedule.FirstOrDefault(e => e.Day == date.DayOfWeek.ToString());
        if (entry == null || !entry.Enabled) return null;

        var start = ResolveTime(entry.Start, entry.StartOffset, date, lat, lon);
        var end = ResolveTime(entry.End, entry.EndOffset, date, lat, lon);
        // Unresolvable (bad text or no location for a sun label): closed.
        return start == null || end == null ? null : (start.Value, end.Value);
    }

    private static TimeOnly? ResolveTime(string? text, int offsetMinutes, DateOnly date, double? lat, double? lon) {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (SunTimes.IsSunLabel(text)) {
            if (lat == null || lon == null) return null;
            var sun = SunTimes.Resolve(text, date, lat.Value, lon.Value);
            return sun?.AddMinutes(offsetMinutes);
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("24:", StringComparison.Ordinal)) return TimeOnly.MaxValue;
        return TimeOnly.TryParse(trimmed, out var t) ? t.AddMinutes(offsetMinutes) : null;
    }
}
