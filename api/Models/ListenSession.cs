namespace XlightsQueue.Models;

/// <summary>One person tapping Listen. Anonymous: only the random per-browser token is stored.</summary>
public class ListenSession {
    public int Id { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int Seconds { get; set; }

    /// <summary>How many people were listening (including this one) when the session started.</summary>
    public int ListenersAtStart { get; set; } = 1;
}
