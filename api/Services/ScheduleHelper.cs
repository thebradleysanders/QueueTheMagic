using System.Text.Json;
using XlightsQueue.DTOs;

namespace XlightsQueue.Services;

public static class ScheduleHelper {
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

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

        // Sun-relative times (Dusk/Sunset/Sunrise) — FPP handles those; treat as open
        return true;
    }
}
