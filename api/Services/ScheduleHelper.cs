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
    public static bool IsOpen(ShowConfig config) => config.ShowMode switch {
        ModeOn => true,
        ModeOff => false,
        _ => IsOpen(config.ShowScheduleJson)
    };

    /// <summary>Season is running AND the show is inside its hours. Used for gating requests and playback.</summary>
    public static bool IsRunning(ShowConfig config) => IsSeasonActive(config) && IsOpen(config);

    /// <summary>
    /// Returns true if the show is currently open based on the stored schedule JSON.
    /// If no schedule is configured, the show is always open.
    /// </summary>
    public static bool IsOpen(string? scheduleJson) {
        if (string.IsNullOrWhiteSpace(scheduleJson)) return true;

        List<ShowScheduleEntryDto>? schedule;
        try { schedule = JsonSerializer.Deserialize<List<ShowScheduleEntryDto>>(scheduleJson, _jsonOpts); } catch { return true; }

        if (schedule == null || schedule.Count == 0) return true;

        var todayName = DateTime.Now.DayOfWeek.ToString(); // "Sunday", "Monday", …
        var todayEntry = schedule.FirstOrDefault(e => e.Day == todayName);

        if (todayEntry == null || !todayEntry.Enabled) return false;

        if (TimeOnly.TryParse(todayEntry.Start, out var start) && TimeOnly.TryParse(todayEntry.End, out var end)) {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            // Handle overnight spans (e.g. 22:00 – 02:00)
            return start <= end ? now >= start && now <= end : now >= start || now <= end;
        }

        // Sun-relative times (Dusk/Sunset/Sunrise), FPP handles those; treat as open
        return true;
    }
}
