namespace XlightsQueue.Models;

/// <summary>A "friends &amp; family" sign-in. Deleting it revokes access immediately.</summary>
public class GuestPass {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public int RequestCount { get; set; }
}
