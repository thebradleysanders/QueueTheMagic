namespace XlightsQueue.DTOs;

public record AddToQueueRequest(int SongId, string PaymentIntentId, string SessionToken, string? DisplayName = null);
public record DonateRequest(string PaymentIntentId, string SessionToken, decimal Amount, string? DisplayName = null);
public record BumpQueueRequest(string PaymentIntentId, string SessionToken, string? DisplayName = null);
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
    string StripePublishableKey,
    string ShowMode = "auto",
    string? SeasonStart = null,
    string? SeasonEnd = null,
    bool ShowSupporters = true,
    bool ListenEnabled = false
);

public record ShowModeRequest(string Mode);
public record CheerRequest(string SessionToken);
public record ActivityDto(string Kind, string? Title, decimal? Amount, DateTime At, string? Name = null);
public record SupporterDto(string? Name, decimal Amount, string Kind, DateTime At);
public record AdminSupporterDto(int Id, string? Name, decimal Amount, string Kind, DateTime At);
public record CommunityDto(decimal Raised, int Supporters, decimal Goal, string GoalLabel, int CheersToday, int WatchingTonight, IReadOnlyList<ActivityDto> Recent);
public record SocialLinkDto(string Platform, string Url);
public record ShowScheduleEntryDto(string Day, string Start, string End, bool Enabled, int StartOffset = 0, int EndOffset = 0);

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
    List<DailyStatDto> DailyStats,
    int TotalVisits,
    int UniqueDevices,
    int VisitsToday,
    ListenStatsDto Listen
);

/// <summary>How popular "Listen live" is (last 30 days).</summary>
public record ListenStatsDto(int Sessions, int Devices, int SessionsToday, long TotalSeconds, int AverageSeconds, int PeakConcurrent, int ListeningNow);

public record DailyStatDto(string Date, decimal Donations, int SongsQueued, int Visits, int Devices, int Listens);
public record VisitRequest(string SessionToken);

public record SongAdminDto(int Id, string Title, string Artist, string Filename, int DurationSeconds);
public record UpdateSongRequest(string Title, string Artist);

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
