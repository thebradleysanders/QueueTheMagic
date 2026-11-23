namespace XlightsQueue.Models;

/// <summary>One time a device opened the site. Anonymous: only the random per-browser token is stored, never an IP.</summary>
public class Visit {
    public int Id { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
