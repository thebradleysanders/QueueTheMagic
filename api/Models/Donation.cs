namespace XlightsQueue.Models;

public enum DonationType { SongRequest, Bump, Donation }

public class Donation {
    public int Id { get; set; }
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DonationType Type { get; set; }
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>Name the supporter chose for the public supporters list. Null = anonymous.</summary>
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
