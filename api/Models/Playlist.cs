using System.ComponentModel.DataAnnotations.Schema;

namespace XlightsQueue.Models;

public class Playlist {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FppPlaylistName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    /// <summary>Display order (lowest first). New playlists go to the end.</summary>
    public int SortOrder { get; set; }

    // Creation order within this process, so a batch of new rows keeps the order they were added in.
    [NotMapped] public long AddSeq { get; } = Interlocked.Increment(ref _seq);
    private static long _seq;
    public ICollection<Song> Songs { get; set; } = new List<Song>();
}
