using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Hubs;
using XlightsQueue.Models;
using XlightsQueue.Services;

namespace XlightsQueue.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(
    AdminAuthService authService,
    QueueManagerService queueManager,
    QueueService queueService,
    FppService fppService,
    MqttService mqttService,
    IDbContextFactory<AppDbContext> dbFactory,
    IHubContext<ShowHub> hub,
    IConfiguration configuration) : ControllerBase {
    [HttpPost("login")]
    public IActionResult Login([FromBody] AdminLoginRequest request) {
        if (!authService.ValidatePassword(request.Password))
            return Unauthorized(new { error = "Invalid password." });

        return Ok(new { token = authService.GenerateToken() });
    }

    [Authorize]
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        return Ok(config);
    }

    [Authorize]
    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] ShowConfig updated) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return NotFound();

        config.SiteName = updated.SiteName;
        config.FmRadioStation = updated.FmRadioStation;
        config.SocialMediaJson = updated.SocialMediaJson;
        config.SongRequestCost = updated.SongRequestCost;
        config.BumpCost = updated.BumpCost;
        config.DonateCost = updated.DonateCost;
        config.FppAddress = updated.FppAddress;
        config.DefaultPlaylistName = updated.DefaultPlaylistName;
        config.AutoPlayDefault = updated.AutoPlayDefault;
        config.InterruptForUserSongs = updated.InterruptForUserSongs;
        config.FppPollingIntervalSeconds = Math.Max(2, updated.FppPollingIntervalSeconds);
        config.MaxSongsPerWindow = updated.MaxSongsPerWindow;
        config.RateLimitWindowMinutes = updated.RateLimitWindowMinutes;
        config.DefaultTheme = updated.DefaultTheme;
        config.MqttEnabled = updated.MqttEnabled;
        config.MqttBrokerHost = updated.MqttBrokerHost;
        config.MqttBrokerPort = updated.MqttBrokerPort;
        config.MqttUsername = updated.MqttUsername;
        config.MqttPassword = updated.MqttPassword;
        config.MqttTopicPrefix = updated.MqttTopicPrefix;
        config.ShowScheduleJson = updated.ShowScheduleJson;
        config.IsSeasonActive = updated.IsSeasonActive;
        config.OffSeasonMessage = updated.OffSeasonMessage;

        await db.SaveChangesAsync();

        // Broadcast public config change
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var socialLinks = new List<SocialLinkDto>();
        try { socialLinks = JsonSerializer.Deserialize<List<SocialLinkDto>>(config.SocialMediaJson, jsonOpts) ?? []; } catch { }
        var schedule = new List<ShowScheduleEntryDto>();
        try { schedule = JsonSerializer.Deserialize<List<ShowScheduleEntryDto>>(config.ShowScheduleJson, jsonOpts) ?? []; } catch { }
        var publicConfig = new PublicConfigDto(
            config.SiteName, config.FmRadioStation, socialLinks, config.DefaultTheme,
            config.SongRequestCost, config.BumpCost, config.DonateCost,
            ScheduleHelper.IsOpen(config.ShowScheduleJson), schedule,
            config.IsSeasonActive, config.OffSeasonMessage,
            configuration["Stripe:PublishableKey"] ?? string.Empty);
        await hub.Clients.All.SendAsync("ShowConfigUpdated", publicConfig);

        return Ok(config);
    }

    [Authorize]
    [HttpGet("diagnostics")]
    public IActionResult GetDiagnostics() => Ok(queueManager.GetDiagnostics());

    [Authorize]
    [HttpGet("reports")]
    public async Task<IActionResult> GetReports() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-30);

        var donations = await db.Donations.Where(d => d.CreatedAt >= since).ToListAsync();
        var queueItems = await db.QueueItems.Include(q => q.Song)
            .Where(q => q.CreatedAt >= since).ToListAsync();

        var totalDonations = donations.Sum(d => d.Amount);
        var highestDonation = donations.Any() ? donations.Max(d => d.Amount) : 0;
        var totalPlayed = queueItems.Count(q => q.Status == QueueItemStatus.Played);
        var totalQueued = queueItems.Count;

        var topSong = queueItems
            .GroupBy(q => q.Song.Title)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var dailyStats = Enumerable.Range(0, 30)
            .Select(i => DateTime.UtcNow.Date.AddDays(-i))
            .Select(date => new DailyStatDto(
                date.ToString("yyyy-MM-dd"),
                donations.Where(d => d.CreatedAt.Date == date).Sum(d => d.Amount),
                queueItems.Count(q => q.CreatedAt.Date == date)
            ))
            .OrderBy(d => d.Date)
            .ToList();

        return Ok(new ReportsDto(totalDonations, totalPlayed, totalQueued, highestDonation, topSong, dailyStats));
    }

    [Authorize]
    [HttpGet("ratings")]
    public async Task<IActionResult> GetRatings() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var ratings = await db.SongRatings
            .Include(r => r.Song).ThenInclude(s => s.Playlist)
            .ToListAsync();

        var report = ratings
            .GroupBy(r => r.SongId)
            .Select(g => new SongRatingReportDto(
                g.Key,
                g.First().Song.Title,
                g.First().Song.Artist,
                g.First().Song.Playlist.Name,
                Math.Round(g.Average(r => r.Rating), 1),
                g.Count(),
                g.Count(r => r.Rating == 5),
                g.Count(r => r.Rating == 4),
                g.Count(r => r.Rating == 3),
                g.Count(r => r.Rating == 2),
                g.Count(r => r.Rating == 1)
            ))
            .OrderByDescending(r => r.AverageRating)
            .ThenByDescending(r => r.TotalRatings)
            .ToList();

        return Ok(report);
    }

    [Authorize]
    [HttpPost("sync-songs")]
    public async Task<IActionResult> SyncSongs() {
        var sequences = await fppService.GetSequencesAsync();
        if (sequences.Count == 0)
            return Ok(new { synced = 0, message = "No sequences returned from FPP." });

        await using var db = await dbFactory.CreateDbContextAsync();
        var defaultPlaylist = await db.Playlists.FirstOrDefaultAsync()
            ?? new Playlist { Name = "Default", FppPlaylistName = "Default" };

        if (defaultPlaylist.Id == 0) {
            db.Playlists.Add(defaultPlaylist);
            await db.SaveChangesAsync();
        }

        int synced = 0;
        foreach (var seq in sequences) {
            var existing = await db.Songs.FirstOrDefaultAsync(s => s.Filename == seq);
            if (existing == null) {
                var title = Path.GetFileNameWithoutExtension(seq).Replace("_", " ").Replace("-", " ");
                db.Songs.Add(new Song {
                    Title = title,
                    Artist = string.Empty,
                    Filename = seq,
                    PlaylistId = defaultPlaylist.Id
                });
                synced++;
            }
        }

        await db.SaveChangesAsync();
        return Ok(new { synced, total = sequences.Count });
    }

    [Authorize]
    [HttpPost("sync-playlists")]
    public async Task<IActionResult> SyncPlaylists() {
        var fppPlaylists = await fppService.GetPlaylistsAsync();
        if (fppPlaylists.Count == 0)
            return Ok(new { synced = 0, message = "No playlists returned from FPP." });

        await using var db = await dbFactory.CreateDbContextAsync();
        int newPlaylists = 0, newSongs = 0;

        foreach (var fppName in fppPlaylists) {
            // Upsert playlist record
            var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.FppPlaylistName == fppName);
            if (playlist == null) {
                playlist = new Playlist { Name = fppName, FppPlaylistName = fppName, IsEnabled = true };
                db.Playlists.Add(playlist);
                await db.SaveChangesAsync();
                newPlaylists++;
            }

            // Fetch and upsert songs
            var entries = await fppService.GetPlaylistSequencesAsync(fppName);
            foreach (var (seqName, mediaName, duration) in entries) {
                var existing = await db.Songs.FirstOrDefaultAsync(s => s.Filename == seqName && s.PlaylistId == playlist.Id);
                if (existing == null) {
                    var title = Path.GetFileNameWithoutExtension(seqName);
                    db.Songs.Add(new Song {
                        Title = title,
                        Artist = string.Empty,
                        Filename = seqName,
                        MediaFilename = mediaName,
                        DurationSeconds = (int)duration,
                        PlaylistId = playlist.Id
                    });
                    newSongs++;
                } else {
                    if (!string.IsNullOrEmpty(mediaName)) existing.MediaFilename = mediaName;
                    if (duration > 0 && existing.DurationSeconds == 0) existing.DurationSeconds = (int)duration;
                }
            }
            await db.SaveChangesAsync();
        }

        return Ok(new { newPlaylists, newSongs, totalFppPlaylists = fppPlaylists.Count });
    }

    [Authorize]
    [HttpGet("playlists")]
    public async Task<IActionResult> GetPlaylists() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var playlists = await db.Playlists
            .Include(p => p.Songs)
            .Select(p => new PlaylistAdminDto(p.Id, p.Name, p.FppPlaylistName, p.IsEnabled, p.Songs.Count))
            .ToListAsync();
        return Ok(playlists);
    }

    [Authorize]
    [HttpPut("playlists/{id}")]
    public async Task<IActionResult> UpdatePlaylist(int id, [FromBody] UpdatePlaylistRequest request) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var playlist = await db.Playlists.FindAsync(id);
        if (playlist == null) return NotFound();
        playlist.Name = request.Name;
        playlist.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync();
        return Ok(new PlaylistAdminDto(playlist.Id, playlist.Name, playlist.FppPlaylistName, playlist.IsEnabled, 0));
    }

    [Authorize]
    [HttpGet("playlists/{id}/songs")]
    public async Task<IActionResult> GetPlaylistSongs(int id) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var songs = await db.Songs
            .Where(s => s.PlaylistId == id)
            .OrderBy(s => s.Title)
            .Select(s => new SongAdminDto(s.Id, s.Title, s.Artist, s.Filename, s.DurationSeconds))
            .ToListAsync();
        return Ok(songs);
    }

    [Authorize]
    [HttpPut("songs/{id}")]
    public async Task<IActionResult> UpdateSong(int id, [FromBody] UpdateSongRequest request) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var song = await db.Songs.FindAsync(id);
        if (song == null) return NotFound();
        song.Title = request.Title;
        song.Artist = request.Artist;
        await db.SaveChangesAsync();
        return Ok(new SongAdminDto(song.Id, song.Title, song.Artist, song.Filename, song.DurationSeconds));
    }

    [Authorize]
    [HttpPost("sync-schedule")]
    public async Task<IActionResult> SyncSchedule() {
        var entries = await fppService.GetScheduleAsync();
        if (entries.Count == 0)
            return Ok(new { synced = false, message = "No schedule entries returned from FPP." });

        // For each weekday, find the best (earliest-starting, enabled) entry
        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var result = new List<object>();

        foreach (var dayName in days) {
            var candidates = entries
                .Where(e => e.enabled == 1 && FppService.FppDayToDayNames(e.day).Contains(dayName))
                .ToList();

            if (candidates.Count == 0) {
                result.Add(new { day = dayName, start = "17:00", end = "22:00", enabled = false });
                continue;
            }

            // Prefer entries with a concrete HH:MM:SS start time; fall back to any
            var best = candidates.FirstOrDefault(e => TimeOnly.TryParse(e.startTime, out _))
                    ?? candidates.First();

            var startStr = FormatFppTime(best.startTime);
            var endStr = FormatFppTime(best.endTime);
            result.Add(new { day = dayName, start = startStr, end = endStr, enabled = true });
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return NotFound();
        config.ShowScheduleJson = JsonSerializer.Serialize(result);
        await db.SaveChangesAsync();

        var enabled = result.Count(r => (bool)r.GetType().GetProperty("enabled")!.GetValue(r)!);
        return Ok(new { synced = true, daysEnabled = enabled, totalEntries = entries.Count });
    }

    private static string FormatFppTime(string fppTime) {
        // Try to parse as HH:MM:SS or HH:MM
        if (TimeOnly.TryParse(fppTime, out var t))
            return t.ToString("HH:mm");
        // Sun-relative labels — return as-is for display purposes; isOpen logic skips these
        return fppTime; // "Dusk", "SunSet", "SunRise", etc.
    }

    [Authorize]
    [HttpGet("fpp/playlists")]
    public async Task<IActionResult> GetFppPlaylists() =>
        Ok(await fppService.GetPlaylistsAsync());

    [Authorize]
    [HttpGet("fpp/sequences")]
    public async Task<IActionResult> GetFppSequences() =>
        Ok(await fppService.GetSequencesAsync());

    [Authorize]
    [HttpPost("fpp/test-play")]
    public async Task<IActionResult> TestPlay([FromBody] string filename) {
        // Look up the media filename from DB if available
        await using var db = await dbFactory.CreateDbContextAsync();
        var song = await db.Songs.FirstOrDefaultAsync(s => s.Filename == filename);
        var ok = await fppService.StartSequenceAsync(filename, song?.MediaFilename ?? "");
        return Ok(new { success = ok, filename, mediaFilename = song?.MediaFilename });
    }

    [Authorize]
    [HttpPost("mqtt/test")]
    public async Task<IActionResult> TestMqtt() {
        await mqttService.PublishAsync("test", new { message = "QueueTheMagic MQTT test", timestamp = DateTime.UtcNow });
        return Ok(new { sent = true });
    }

    [Authorize]
    [HttpPost("queue/{id}/skip")]
    public async Task<IActionResult> SkipQueueItem(int id) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.QueueItems.FindAsync(id);
        if (item == null) return NotFound();
        if (item.Status == QueueItemStatus.Playing)
            await fppService.StopPlaylistAsync();
        item.Status = QueueItemStatus.Skipped;
        item.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var queue = await queueService.GetQueueAsync();
        await hub.Clients.All.SendAsync("QueueUpdated", queue);
        return Ok(new { success = true });
    }
}
