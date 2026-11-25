using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Hubs;

namespace XlightsQueue.Services;

/// <summary>
/// The audience side of the show: the live activity feed ("someone just requested…"),
/// cheers, and the season donation goal. Everything shown publicly is anonymous.
/// </summary>
public class CommunityService(
    IDbContextFactory<AppDbContext> dbFactory,
    IHubContext<ShowHub> hub,
    MqttService mqtt,
    ILogger<CommunityService> logger) {

    private const int FeedSize = 8;
    private static readonly TimeSpan CheerCooldown = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly LinkedList<ActivityDto> _feed = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastCheer = new();
    private DateOnly _cheerDay = DateOnly.FromDateTime(DateTime.Now);
    private int _cheersToday;

    public IReadOnlyList<ActivityDto> RecentActivity() {
        lock (_gate) return _feed.ToList();
    }

    public void ResetCheers() {
        lock (_gate) { _cheersToday = 0; _feed.Clear(); }
        _lastCheer.Clear();
    }

    public int CheersToday() {
        lock (_gate) { RollDay(); return _cheersToday; }
    }

    /// <summary>Adds an event to the feed and pushes it to every open browser.</summary>
    public async Task AddActivityAsync(string kind, string? title = null, decimal? amount = null, string? name = null) {
        // Names only appear in the live feed when the supporters list is switched on.
        if (name != null) {
            await using var db = await dbFactory.CreateDbContextAsync();
            if ((await db.ShowConfigs.FindAsync(1))?.ShowSupporters != true) name = null;
        }
        var a = new ActivityDto(kind, title, amount, DateTime.UtcNow, name);
        lock (_gate) {
            _feed.AddFirst(a);
            while (_feed.Count > FeedSize) _feed.RemoveLast();
        }
        try {
            await hub.Clients.All.SendAsync("Activity", a);
            if (amount is > 0) await BroadcastCommunityAsync();
        } catch (Exception ex) {
            logger.LogWarning("Activity broadcast failed: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// A free tap from the audience. Returns the new total, or null if this device is cheering too fast.
    /// Published to MQTT so the show itself can react (flash a prop, fire a strobe…).
    /// </summary>
    public async Task<int?> CheerAsync(string sessionToken) {
        var now = DateTime.UtcNow;
        if (_lastCheer.TryGetValue(sessionToken, out var last) && now - last < CheerCooldown) return null;
        _lastCheer[sessionToken] = now;

        int total;
        lock (_gate) { RollDay(); total = ++_cheersToday; }

        // Keep the cooldown table from growing forever on a busy night.
        if (_lastCheer.Count > 5000)
            foreach (var kv in _lastCheer.Where(kv => now - kv.Value > TimeSpan.FromMinutes(5)).ToList())
                _lastCheer.TryRemove(kv.Key, out _);

        await hub.Clients.All.SendAsync("Cheer", new { cheersToday = total });
        await mqtt.PublishAsync("cheer", new { cheersToday = total, timestamp = now });
        return total;
    }

    public async Task<CommunityDto> GetCommunityAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1) ?? new Models.ShowConfig();

        var since = SeasonWindowStart(config);
        var donations = await db.Donations.Where(d => d.CreatedAt >= since)
            .Select(d => new { d.Amount, d.SessionToken }).ToListAsync();

        var todayUtc = DateTime.Today.ToUniversalTime();
        var watchingTonight = await db.Visits.Where(v => v.CreatedAt >= todayUtc)
            .Select(v => v.SessionToken).Distinct().CountAsync();

        return new CommunityDto(
            Raised: donations.Sum(d => d.Amount),
            Supporters: donations.Select(d => d.SessionToken).Distinct().Count(),
            Goal: config.DonationGoal,
            GoalLabel: config.DonationGoalLabel,
            CheersToday: CheersToday(),
            WatchingTonight: watchingTonight,
            Recent: RecentActivity());
    }

    /// <summary>
    /// Raised / supporters count from 30 days before the season start (pre-season support counts),
    /// or the last 365 days if no start date is set.
    /// </summary>
    public static DateTime SeasonWindowStart(Models.ShowConfig config) =>
        config.SeasonStart is { } start
            ? start.AddDays(-30).ToDateTime(TimeOnly.MinValue).ToUniversalTime()
            : DateTime.UtcNow.AddDays(-365);

    /// <summary>The public supporters list for this season, newest first. Null when the feature is off.</summary>
    public async Task<List<SupporterDto>?> GetSupportersAsync(int limit) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null || !config.ShowSupporters) return null;

        var since = SeasonWindowStart(config);
        var rows = await db.Donations.Where(d => d.CreatedAt >= since)
            .OrderByDescending(d => d.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(d => new { d.DisplayName, d.Amount, d.Type, d.CreatedAt })
            .ToListAsync();

        return rows.Select(d => new SupporterDto(d.DisplayName, d.Amount, KindOf(d.Type), DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc))).ToList();
    }

    public static string KindOf(Models.DonationType t) => t switch {
        Models.DonationType.SongRequest => "request",
        Models.DonationType.Bump => "boost",
        _ => "donation"
    };

    public async Task BroadcastCommunityAsync() {
        try { await hub.Clients.All.SendAsync("CommunityUpdated", await GetCommunityAsync()); }
        catch (Exception ex) { logger.LogWarning("Community broadcast failed: {Message}", ex.Message); }
    }

    private void RollDay() {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != _cheerDay) { _cheerDay = today; _cheersToday = 0; }
    }
}
