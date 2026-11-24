using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    MqttService mqttService,
    CommunityService community,
    IConfiguration configuration) : ControllerBase {
    // A device that re-opens the site within this window counts as the same visit
    // (stops refresh-spam inflating stats and flooding MQTT).
    private static readonly TimeSpan VisitWindow = TimeSpan.FromMinutes(30);

    [EnableRateLimiting("visit")]
    [HttpPost("visit")]
    public async Task<IActionResult> RecordVisit([FromBody] VisitRequest request) {
        if (string.IsNullOrWhiteSpace(request.SessionToken) || request.SessionToken.Length > 64)
            return BadRequest(new { error = "Invalid session." });

        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var last = await db.Visits
            .Where(v => v.SessionToken == request.SessionToken)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => (DateTime?)v.CreatedAt)
            .FirstOrDefaultAsync();

        if (last != null && now - last.Value < VisitWindow)
            return Ok(new { counted = false });

        var isNewDevice = last == null;
        db.Visits.Add(new Models.Visit { SessionToken = request.SessionToken, CreatedAt = now });
        await db.SaveChangesAsync();

        var today = now.Date;
        var visitsToday = await db.Visits.CountAsync(v => v.CreatedAt >= today);
        await mqttService.PublishAsync("visit", new { newDevice = isNewDevice, visitsToday, timestamp = now });

        return Ok(new { counted = true });
    }

    /// <summary>Donation goal, cheers, audience size and the recent activity feed.</summary>
    [HttpGet("community")]
    public async Task<IActionResult> GetCommunity() => Ok(await community.GetCommunityAsync());

    /// <summary>This season's supporters (names are opt-in, everyone else is Anonymous). 404 when switched off.</summary>
    [HttpGet("supporters")]
    public async Task<IActionResult> GetSupporters([FromQuery] int limit = 50) {
        var list = await community.GetSupportersAsync(limit);
        return list == null ? NotFound(new { error = "The supporters list is turned off." }) : Ok(list);
    }

    /// <summary>A free "cheer" from the audience. Pushed live to every phone and to MQTT.</summary>
    [EnableRateLimiting("cheer")]
    [HttpPost("cheer")]
    public async Task<IActionResult> Cheer([FromBody] CheerRequest request) {
        if (string.IsNullOrWhiteSpace(request.SessionToken) || request.SessionToken.Length > 64)
            return BadRequest(new { error = "Invalid session." });
        var total = await community.CheerAsync(request.SessionToken);
        return total == null ? StatusCode(429, new { error = "Easy there! One cheer at a time." }) : Ok(new { cheersToday = total });
    }

    [HttpGet("nowplaying")]
    public async Task<IActionResult> GetNowPlaying() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return NotFound();

        var diag = queueManager.GetDiagnostics();
        var fppStatus = await fppService.GetStatusCachedAsync(TimeSpan.FromSeconds(2));

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

        var publicConfig = PublicConfigBuilder.Build(config, configuration["Stripe:PublishableKey"] ?? string.Empty);

        return Ok(new { nowPlaying, config = publicConfig });
    }

    private static NowPlayingDto BuildFppNowPlaying(FppStatus? fppStatus, string state, int played, int remaining) {
        if (fppStatus == null || fppStatus.IsIdle || fppStatus.CurrentTrackName == null)
            return new NowPlayingDto(null, null, null, null, false, state, 0, 0);

        var trackName = Path.GetFileNameWithoutExtension(fppStatus.CurrentTrackName);
        return new NowPlayingDto(trackName, null, fppStatus.CurrentTrackName, null, true, state, played, remaining);
    }
}
