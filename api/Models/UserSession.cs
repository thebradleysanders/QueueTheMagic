namespace XlightsQueue.Models;

public class UserSession {
    public int Id { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
