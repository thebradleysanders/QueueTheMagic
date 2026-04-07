using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Models;

namespace XlightsQueue.Services;

public class QueueService(IDbContextFactory<AppDbContext> dbFactory, ILogger<QueueService> logger, MqttService mqttService) {
    public async Task<List<QueueItemDto>> GetQueueAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.QueueItems
            .Include(q => q.Song).ThenInclude(s => s.Playlist)
            .Where(q => q.Status == QueueItemStatus.Pending || q.Status == QueueItemStatus.Playing)
            .OrderBy(q => q.Position)
            .Select(q => new QueueItemDto(
                q.Id, q.SongId, q.Song.Title, q.Song.Artist,
                q.Song.Playlist.Name, q.Position, q.Status.ToString(),
                q.CreatedAt, q.StartedAt, q.Song.DurationSeconds))
            .ToListAsync();
    }

    public async Task<(QueueItem? item, string? error)> AddToQueueAsync(int songId, string paymentIntentId, string sessionToken) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1) ?? new ShowConfig();

        // Rate limiting
        var windowStart = DateTime.UtcNow.AddMinutes(-config.RateLimitWindowMinutes);
        var recentCount = await db.QueueItems
            .Where(q => q.SessionToken == sessionToken && q.CreatedAt >= windowStart)
            .CountAsync();

        if (recentCount >= config.MaxSongsPerWindow)
            return (null, $"Rate limit: max {config.MaxSongsPerWindow} songs per {config.RateLimitWindowMinutes} minutes.");

        // Consecutive duplicate prevention
        var lastPending = await db.QueueItems
            .Where(q => q.Status == QueueItemStatus.Pending || q.Status == QueueItemStatus.Playing)
            .OrderByDescending(q => q.Position)
            .FirstOrDefaultAsync();

        if (lastPending?.SongId == songId)
            return (null, "That song is already at the end of the queue.");

        var song = await db.Songs.FindAsync(songId);
        if (song == null) return (null, "Song not found.");

        var maxPosition = await db.QueueItems
            .Where(q => q.Status == QueueItemStatus.Pending || q.Status == QueueItemStatus.Playing)
            .Select(q => (int?)q.Position)
            .MaxAsync() ?? 0;

        var donation = new Donation {
            StripePaymentIntentId = paymentIntentId,
            Amount = config.SongRequestCost,
            Type = DonationType.SongRequest,
            SessionToken = sessionToken
        };
        db.Donations.Add(donation);
        await db.SaveChangesAsync();

        var item = new QueueItem {
            SongId = songId,
            DonationId = donation.Id,
            SessionToken = sessionToken,
            Position = maxPosition + 1,
            Status = QueueItemStatus.Pending
        };
        db.QueueItems.Add(item);
        await db.SaveChangesAsync();
        await mqttService.PublishAsync("songqueued", new { song = item, donation = donation, timestamp = DateTime.UtcNow });

        logger.LogInformation("Added '{Title}' to queue at position {Position}", song.Title, item.Position);
        return (item, null);
    }

    public async Task<(bool success, string? error)> BumpAsync(int queueItemId, string paymentIntentId, string sessionToken) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1) ?? new ShowConfig();

        var item = await db.QueueItems.Include(q => q.Song)
            .FirstOrDefaultAsync(q => q.Id == queueItemId && q.Status == QueueItemStatus.Pending);

        if (item == null) return (false, "Queue item not found or not bumpable.");

        var above = await db.QueueItems
            .Where(q => q.Position < item.Position && q.Status == QueueItemStatus.Pending)
            .OrderByDescending(q => q.Position)
            .FirstOrDefaultAsync();

        if (above == null) return (false, "Already at the top of the queue.");

        var donation = new Donation {
            StripePaymentIntentId = paymentIntentId,
            Amount = config.BumpCost,
            Type = DonationType.Bump,
            SessionToken = sessionToken
        };
        db.Donations.Add(donation);

        (item.Position, above.Position) = (above.Position, item.Position);
        await db.SaveChangesAsync();

        logger.LogInformation("Bumped '{Title}' up to position {Position}", item.Song.Title, item.Position);
        return (true, null);
    }

    public async Task<QueueItem?> GetNextPendingAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.QueueItems
            .Include(q => q.Song)
            .Where(q => q.Status == QueueItemStatus.Pending)
            .OrderBy(q => q.Position)
            .FirstOrDefaultAsync();
    }

    public async Task<QueueItem?> GetPlayingItemAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.QueueItems
            .Include(q => q.Song)
            .FirstOrDefaultAsync(q => q.Status == QueueItemStatus.Playing);
    }

    public async Task MarkPlayingAsync(int queueItemId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.QueueItems.FindAsync(queueItemId);
        if (item == null) return;
        item.Status = QueueItemStatus.Playing;
        item.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task MarkPlayedAsync(int queueItemId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.QueueItems.FindAsync(queueItemId);
        if (item == null) return;
        item.Status = QueueItemStatus.Played;
        item.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<bool> HasPendingItemsAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.QueueItems.AnyAsync(q => q.Status == QueueItemStatus.Pending);
    }
}
