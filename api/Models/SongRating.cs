namespace XlightsQueue.Models;

public class SongRating {
    public int Id { get; set; }
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
    public string SessionToken { get; set; } = string.Empty;
    public int Rating { get; set; } // 1–5
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
