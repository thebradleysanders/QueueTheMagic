using Microsoft.EntityFrameworkCore;
using XlightsQueue.Models;

namespace XlightsQueue.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) {
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<QueueItem> QueueItems => Set<QueueItem>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ShowConfig> ShowConfigs => Set<ShowConfig>();
    public DbSet<ThankYou> ThankYous => Set<ThankYou>();
    public DbSet<SongRating> SongRatings => Set<SongRating>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<ListenSession> ListenSessions => Set<ListenSession>();
    public DbSet<GuestPass> GuestPasses => Set<GuestPass>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ShowConfig>().HasData(new ShowConfig { Id = 1 });

        modelBuilder.Entity<Playlist>().HasData(
            new Playlist { Id = 1, Name = "Holiday Favorites", FppPlaylistName = "HolidayFavorites", IsEnabled = true }
        );

        modelBuilder.Entity<SongRating>()
            .HasIndex(r => new { r.SongId, r.SessionToken })
            .IsUnique();

        modelBuilder.Entity<ListenSession>().HasIndex(l => l.StartedAt);
        modelBuilder.Entity<GuestPass>().HasIndex(g => g.Code).IsUnique();
        modelBuilder.Entity<Visit>().HasIndex(v => v.CreatedAt);
        modelBuilder.Entity<Visit>().HasIndex(v => new { v.SessionToken, v.CreatedAt });
    }
}
