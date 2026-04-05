namespace XlightsQueue.Models;

public class Playlist {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FppPlaylistName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public ICollection<Song> Songs { get; set; } = new List<Song>();
}
