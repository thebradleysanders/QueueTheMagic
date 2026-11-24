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

    // New playlists and songs (e.g. from an FPP import) go to the end of the display order.
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
        var newPlaylists = ChangeTracker.Entries<Playlist>().Where(e => e.State == EntityState.Added && e.Entity.SortOrder == 0).Select(e => e.Entity).OrderBy(p => p.AddSeq).ToList();
        if (newPlaylists.Count > 0) {
            var next = (await Playlists.AsNoTracking().MaxAsync(p => (int?)p.SortOrder, cancellationToken) ?? 0) + 1;
            foreach (var p in newPlaylists) p.SortOrder = next++;
        }

        foreach (var group in ChangeTracker.Entries<Song>().Where(e => e.State == EntityState.Added && e.Entity.SortOrder == 0).Select(e => e.Entity).GroupBy(s => s.PlaylistId)) {
            var next = (await Songs.AsNoTracking().Where(s => s.PlaylistId == group.Key).MaxAsync(s => (int?)s.SortOrder, cancellationToken) ?? 0) + 1;
            foreach (var s in group.OrderBy(s => s.AddSeq)) s.SortOrder = next++;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

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
