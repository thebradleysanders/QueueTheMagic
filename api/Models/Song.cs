using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>Display order within its playlist (lowest first). New songs go to the end.</summary>
    public int SortOrder { get; set; }

    // Creation order within this process, so a batch of new rows keeps the order they were added in.
    [NotMapped] public long AddSeq { get; } = Interlocked.Increment(ref _seq);
    private static long _seq;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<SongRating> Ratings { get; set; } = [];
}
