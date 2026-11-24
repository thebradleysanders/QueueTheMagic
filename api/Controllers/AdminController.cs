using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    ShowControlService showControl,
    CommunityService community,
    AudioRelayService relay,
    IDbContextFactory<AppDbContext> dbFactory,
    IHubContext<ShowHub> hub) : ControllerBase {
    [EnableRateLimiting("login")]
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

        // Stripe rejects charges under $0.50 and the UI is not designed for huge amounts.
        if (updated.SongRequestCost < 0.5m || updated.BumpCost < 0.5m || updated.DonateCost < 0.5m
            || updated.SongRequestCost > 1000m || updated.BumpCost > 1000m || updated.DonateCost > 1000m)
            return BadRequest(new { error = "Prices must be between $0.50 and $1000." });
        if (string.IsNullOrWhiteSpace(updated.SiteName) || updated.SiteName.Length > 100)
            return BadRequest(new { error = "Site name is required (max 100 characters)." });
        updated.SocialMediaJson = SanitizeSocialLinks(updated.SocialMediaJson);

        config.SiteName = updated.SiteName.Trim();
        config.FmRadioStation = updated.FmRadioStation;
        config.SocialMediaJson = updated.SocialMediaJson;
        config.SongRequestCost = updated.SongRequestCost;
        config.BumpCost = updated.BumpCost;
        config.DonateCost = updated.DonateCost;
        if (updated.DonationGoal < 0 || updated.DonationGoal > 1_000_000)
            return BadRequest(new { error = "Donation goal must be between $0 and $1,000,000." });
        config.DonationGoal = updated.DonationGoal;
        config.ShowSupporters = updated.ShowSupporters;
        config.ListenEnabled = updated.ListenEnabled;
        config.ListenMaxListeners = Math.Clamp(updated.ListenMaxListeners, 1, 500);
        config.DonationGoalLabel = (updated.DonationGoalLabel ?? string.Empty).Trim() is var gl && gl.Length > 120 ? gl[..120] : gl;
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
        if (updated.Latitude is < -90 or > 90 || updated.Longitude is < -180 or > 180)
            return BadRequest(new { error = "Latitude must be between -90 and 90 and longitude between -180 and 180." });
        config.Latitude = updated.Latitude;
        config.Longitude = updated.Longitude;
        if (updated.SeasonStart != null && updated.SeasonEnd != null && updated.SeasonEnd < updated.SeasonStart)
            return BadRequest(new { error = "The season end date must be on or after the start date." });
        // ShowMode (the manual override) is deliberately NOT saved here. It only changes through
        // PUT show-mode or MQTT, so a stale settings form can never flip it by accident.
        config.SeasonStart = updated.SeasonStart;
        config.SeasonEnd = updated.SeasonEnd;
        config.OffSeasonMessage = updated.OffSeasonMessage;

        await db.SaveChangesAsync();

        await showControl.AnnounceAsync(config);
        await community.BroadcastCommunityAsync();

        return Ok(config);
    }

    /// <summary>Is FPP's audio arriving, and how many people are listening?</summary>
    [Authorize]
    [HttpGet("listen/status")]
    public IActionResult ListenStatus() => Ok(new { live = relay.IsLive, listeners = relay.ListenerCount, port = relay.RtpPort });

    /// <summary>Every payment with its public name, for moderating the supporters list.</summary>
    [Authorize]
    [HttpGet("supporters")]
    public async Task<IActionResult> GetSupporters() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Donations.OrderByDescending(d => d.CreatedAt).Take(200)
            .Select(d => new { d.Id, d.DisplayName, d.Amount, d.Type, d.CreatedAt }).ToListAsync();
        return Ok(rows.Select(d => new AdminSupporterDto(d.Id, d.DisplayName, d.Amount, CommunityService.KindOf(d.Type),
            DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc))));
    }

    /// <summary>Hides a supporter's name (they become Anonymous). The payment itself is untouched.</summary>
    [Authorize]
    [HttpPut("supporters/{id}/hide-name")]
    public async Task<IActionResult> HideSupporterName(int id) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var d = await db.Donations.FindAsync(id);
        if (d == null) return NotFound();
        d.DisplayName = null;
        await db.SaveChangesAsync();
        await hub.Clients.All.SendAsync("SupportersChanged");
        return Ok();
    }

    /// <summary>Quick manual override from the dashboard: auto | on | off.</summary>
    [Authorize]
    [HttpPut("show-mode")]
    public async Task<IActionResult> SetShowMode([FromBody] ShowModeRequest request) {
        var config = await showControl.SetModeAsync(request.Mode, "admin");
        if (config == null) return BadRequest(new { error = "Show mode must be auto, on or off." });
        return Ok(new { mode = config.ShowMode });
    }

    // Only http(s) links are allowed so a stored "javascript:" URL can never run in a visitor's browser.
    private static string SanitizeSocialLinks(string? json) {
        try {
            var links = JsonSerializer.Deserialize<List<SocialLinkDto>>(json ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            var safe = links
                .Where(l => !string.IsNullOrWhiteSpace(l.Platform)
                    && Uri.TryCreate(l.Url, UriKind.Absolute, out var u)
                    && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                .Select(l => new { platform = l.Platform.Trim(), url = l.Url.Trim() })
                .ToList();
            return JsonSerializer.Serialize(safe);
        } catch {
            return "[]";
        }
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

        var visits = await db.Visits.Where(v => v.CreatedAt >= since)
            .Select(v => new { v.SessionToken, v.CreatedAt }).ToListAsync();

        var listens = await db.ListenSessions.Where(l => l.StartedAt >= since)
            .Select(l => new { l.SessionToken, l.StartedAt, l.Seconds, l.ListenersAtStart }).ToListAsync();

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
                queueItems.Count(q => q.CreatedAt.Date == date),
                visits.Count(v => v.CreatedAt.Date == date),
                visits.Where(v => v.CreatedAt.Date == date).Select(v => v.SessionToken).Distinct().Count(),
                listens.Count(l => l.StartedAt.Date == date)
            ))
            .OrderBy(d => d.Date)
            .ToList();

        return Ok(new ReportsDto(totalDonations, totalPlayed, totalQueued, highestDonation, topSong, dailyStats,
            visits.Count,
            visits.Select(v => v.SessionToken).Distinct().Count(),
            visits.Count(v => v.CreatedAt.Date == DateTime.UtcNow.Date),
            new ListenStatsDto(
                listens.Count,
                listens.Select(l => l.SessionToken).Distinct().Count(),
                listens.Count(l => l.StartedAt.Date == DateTime.UtcNow.Date),
                listens.Sum(l => (long)l.Seconds),
                listens.Count > 0 ? (int)listens.Average(l => l.Seconds) : 0,
                listens.Count > 0 ? listens.Max(l => l.ListenersAtStart) : 0,
                relay.ListenerCount)));
    }

    /// <summary>
    /// Clears the visitor and Listen-live statistics and the cheer counter, for a fresh start
    /// (for example after testing, or at the start of the season). Payments, donations and the
    /// supporters list are NOT touched.
    /// </summary>
    [Authorize]
    [HttpPost("stats/reset")]
    public async Task<IActionResult> ResetStats() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var visits = await db.Visits.ExecuteDeleteAsync();
        var listens = await db.ListenSessions.ExecuteDeleteAsync();
        community.ResetCheers();
        await community.BroadcastCommunityAsync();
        return Ok(new { visits, listens });
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
                result.Add(new { day = dayName, start = "17:00", end = "22:00", enabled = false, startOffset = 0, endOffset = 0 });
                continue;
            }

            // Prefer entries with a concrete HH:MM:SS start time; fall back to any
            var best = candidates.FirstOrDefault(e => TimeOnly.TryParse(e.startTime, out _))
                    ?? candidates.First();

            var startStr = FormatFppTime(best.startTime);
            var endStr = FormatFppTime(best.endTime);
            result.Add(new { day = dayName, start = startStr, end = endStr, enabled = true, startOffset = best.startTimeOffset, endOffset = best.endTimeOffset });
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return NotFound();
        config.ShowScheduleJson = JsonSerializer.Serialize(result);

        // Dusk/Sunset need a location. Pull it from FPP if we don't have one yet (best effort).
        if (config.Latitude == null || config.Longitude == null) {
            if (double.TryParse(await fppService.GetSettingAsync("Latitude"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(await fppService.GetSettingAsync("Longitude"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon)
                && (lat != 0 || lon != 0)) {
                config.Latitude = lat;
                config.Longitude = lon;
            }
        }
        await db.SaveChangesAsync();

        var enabled = result.Count(r => (bool)r.GetType().GetProperty("enabled")!.GetValue(r)!);
        return Ok(new { synced = true, daysEnabled = enabled, totalEntries = entries.Count, needsLocation = ScheduleHelper.NeedsLocation(config) });
    }

    private static string FormatFppTime(string fppTime) {
        // Try to parse as HH:MM:SS or HH:MM
        if (TimeOnly.TryParse(fppTime, out var t))
            return t.ToString("HH:mm");
        // Sun-relative labels, return as-is for display purposes; isOpen logic skips these
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
