using System.Text.Json;
using XlightsQueue.DTOs;
using XlightsQueue.Models;

namespace XlightsQueue.Services;

public static class PublicConfigBuilder {
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static PublicConfigDto Build(ShowConfig config, string stripePublishableKey) {
        var socialLinks = new List<SocialLinkDto>();
        try { socialLinks = JsonSerializer.Deserialize<List<SocialLinkDto>>(config.SocialMediaJson, _jsonOpts) ?? []; } catch { }
        var schedule = new List<ShowScheduleEntryDto>();
        try { schedule = JsonSerializer.Deserialize<List<ShowScheduleEntryDto>>(config.ShowScheduleJson, _jsonOpts) ?? []; } catch { }

        return new PublicConfigDto(
            config.SiteName, config.FmRadioStation, socialLinks, config.DefaultTheme,
            config.SongRequestCost, config.BumpCost, config.DonateCost,
            ScheduleHelper.IsOpen(config), schedule,
            ScheduleHelper.IsSeasonActive(config), config.OffSeasonMessage,
            stripePublishableKey,
            config.ShowMode,
            config.SeasonStart?.ToString("yyyy-MM-dd"),
            config.SeasonEnd?.ToString("yyyy-MM-dd"),
            config.ShowSupporters,
            config.ListenEnabled,
            config.GuestPassesEnabled);
    }
}
