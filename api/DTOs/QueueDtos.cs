namespace XlightsQueue.DTOs;

public record AddToQueueRequest(int SongId, string PaymentIntentId, string SessionToken);
public record DonateRequest(string PaymentIntentId, string SessionToken, decimal Amount);
public record BumpQueueRequest(string PaymentIntentId, string SessionToken);
public record CreatePaymentIntentRequest(string Type, int? SongId, string SessionToken, decimal? Amount = null);
public record AdminLoginRequest(string Password);

public record QueueItemDto(
    int Id,
    int SongId,
    string Title,
    string Artist,
    string PlaylistName,
    int Position,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    int DurationSeconds
);

public record NowPlayingDto(
    string? Title,
    string? Artist,
    string? Filename,
    DateTime? StartedAt,
    bool IsPlaying,
    string State,
    int SecondsPlayed,
    int SecondsRemaining,
    int? SongId = null
);

public record RateSongRequest(int Rating, string SessionToken);
public record RatingDto(double AverageRating, int TotalRatings, int? YourRating);

public record PublicConfigDto(
    string SiteName,
    string FmRadioStation,
    List<SocialLinkDto> SocialLinks,
    string Theme,
    decimal SongRequestCost,
    decimal BumpCost,
    decimal DonateCost,
    bool IsOpen,
    List<ShowScheduleEntryDto> ShowSchedule,
    bool IsSeasonActive,
    string OffSeasonMessage,
    string StripePublishableKey
);

public record SocialLinkDto(string Platform, string Url);
public record ShowScheduleEntryDto(string Day, string Start, string End, bool Enabled);

public record SongDto(int Id, string Title, string Artist, int DurationSeconds, double AverageRating = 0, int TotalRatings = 0);
public record PlaylistDto(int Id, string Name, string FppPlaylistName, List<SongDto> Songs);
public record PlaylistAdminDto(int Id, string Name, string FppPlaylistName, bool IsEnabled, int SongCount);
public record UpdatePlaylistRequest(string Name, bool IsEnabled);

public record DiagnosticsDto(
    string State,
    string SyncStatus,
    int? ActiveQueueItemId,
    List<FppCommandLogEntry> CommandLog
);

public record FppCommandLogEntry(DateTime Timestamp, string Url, int StatusCode, long DurationMs);

public record ReportsDto(
    decimal TotalDonations,
    int TotalSongsPlayed,
    int TotalSongsQueued,
    decimal HighestDonation,
    string? TopSong,
    List<DailyStatDto> DailyStats
);

public record DailyStatDto(string Date, decimal Donations, int SongsQueued);

public record SongRatingReportDto(
    int SongId,
    string Title,
    string Artist,
    string PlaylistName,
    double AverageRating,
    int TotalRatings,
    int FiveStars,
    int FourStars,
    int ThreeStars,
    int TwoStars,
    int OneStar
);
