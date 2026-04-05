using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Services;

namespace XlightsQueue.Controllers;

[ApiController]
[Route("api/show")]
public class ShowController(
    IDbContextFactory<AppDbContext> dbFactory,
    QueueManagerService queueManager,
    FppService fppService,
    IConfiguration configuration) : ControllerBase {
    [HttpGet("nowplaying")]
    public async Task<IActionResult> GetNowPlaying() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return NotFound();

        var diag = queueManager.GetDiagnostics();
        var fppStatus = await fppService.GetStatusAsync();

        var played = int.TryParse(fppStatus?.seconds_played, out var sp) ? sp : 0;
        var remaining = int.TryParse(fppStatus?.seconds_remaining, out var sr) ? sr : 0;

        NowPlayingDto nowPlaying = BuildFppNowPlaying(fppStatus, diag.State, played, remaining);

        // Override title/artist with our queue item if one is actively playing
        if (diag.ActiveQueueItemId.HasValue) {
            var item = await db.QueueItems.Include(q => q.Song)
                .FirstOrDefaultAsync(q => q.Id == diag.ActiveQueueItemId.Value);
            if (item != null)
                nowPlaying = new NowPlayingDto(
                    item.Song.Title, item.Song.Artist, item.Song.Filename,
                    item.StartedAt, true, diag.State, played, remaining, item.Song.Id);
        }

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var socialLinks = new List<SocialLinkDto>();
        try { socialLinks = JsonSerializer.Deserialize<List<SocialLinkDto>>(config.SocialMediaJson, jsonOpts) ?? []; } catch { }

        var schedule = new List<ShowScheduleEntryDto>();
        try { schedule = JsonSerializer.Deserialize<List<ShowScheduleEntryDto>>(config.ShowScheduleJson, jsonOpts) ?? []; } catch { }

        bool isOpen = ScheduleHelper.IsOpen(config.ShowScheduleJson);
        var publicConfig = new PublicConfigDto(
            config.SiteName, config.FmRadioStation, socialLinks, config.DefaultTheme,
            config.SongRequestCost, config.BumpCost, config.DonateCost,
            isOpen, schedule, config.IsSeasonActive, config.OffSeasonMessage,
            configuration["Stripe:PublishableKey"] ?? string.Empty);

        return Ok(new { nowPlaying, config = publicConfig });
    }

    private static NowPlayingDto BuildFppNowPlaying(FppStatus? fppStatus, string state, int played, int remaining) {
        if (fppStatus == null || fppStatus.IsIdle || fppStatus.CurrentTrackName == null)
            return new NowPlayingDto(null, null, null, null, false, state, 0, 0);

        var trackName = Path.GetFileNameWithoutExtension(fppStatus.CurrentTrackName);
        return new NowPlayingDto(trackName, null, fppStatus.CurrentTrackName, null, true, state, played, remaining);
    }
}
