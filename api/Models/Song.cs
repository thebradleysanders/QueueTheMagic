namespace XlightsQueue.Models;

public class Song {
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;
    public string Filename { get; set; } = string.Empty;
    public string MediaFilename { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<SongRating> Ratings { get; set; } = [];
}
