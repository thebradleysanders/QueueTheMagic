namespace XlightsQueue.Models;

public enum QueueItemStatus { Pending, Playing, Played, Skipped }

public class QueueItem {
    public int Id { get; set; }
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
    public int? DonationId { get; set; }
    public Donation? Donation { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public int Position { get; set; }
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
