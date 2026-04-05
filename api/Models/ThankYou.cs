namespace XlightsQueue.Models;

public class ThankYou {
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? DisplayedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
