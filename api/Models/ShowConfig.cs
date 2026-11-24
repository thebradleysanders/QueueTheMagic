namespace XlightsQueue.Models;

public class ShowConfig {
    public int Id { get; set; } = 1;

    // Site
    public string SiteName { get; set; } = "Queue The Magic";
    public string FmRadioStation { get; set; } = string.Empty;
    public string SocialMediaJson { get; set; } = "[]";

    // Pricing
    public decimal SongRequestCost { get; set; } = 1.00m;
    public decimal BumpCost { get; set; } = 1.00m;
    public decimal DonateCost { get; set; } = 5.00m;

    // Season fundraising goal shown on the home page (0 = hidden)
    public decimal DonationGoal { get; set; } = 0m;
    public string DonationGoalLabel { get; set; } = string.Empty;

    // Public supporters list (names are opt-in; everyone else shows as Anonymous)
    public bool ShowSupporters { get; set; } = true;

    // "Listen live" phone audio (FPP Opus RTP → API → browsers over WebSocket)
    public bool ListenEnabled { get; set; } = false;

    // Friends & family passes: free song requests, any time
    public bool GuestPassesEnabled { get; set; } = true;
    public int GuestMaxSongsPerWindow { get; set; } = 10;
    public int ListenMaxListeners { get; set; } = 50;

    // FPP
    public string FppAddress { get; set; } = "192.168.1.100";
    public string DefaultPlaylistName { get; set; } = string.Empty;
    public bool AutoPlayDefault { get; set; } = true;
    public bool InterruptForUserSongs { get; set; } = true;
    public int FppPollingIntervalSeconds { get; set; } = 5;

    // Queue / Rate limiting
    public int MaxSongsPerWindow { get; set; } = 3;
    public int RateLimitWindowMinutes { get; set; } = 60;

    // UI
    public string DefaultTheme { get; set; } = "dark";

    // Legacy on/off flag, superseded by ShowMode. Kept only so the existing column stays valid.
    public bool IsSeasonActive { get; set; } = true;

    // Where the show is, so Dusk / Sunset / Dawn / Sunrise schedule times can be worked out.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Season window (server-local dates, inclusive). Null = no limit on that side.
    public DateOnly? SeasonStart { get; set; }
    public DateOnly? SeasonEnd { get; set; }

    // Manual override: "auto" follows the season dates + weekly hours, "on" forces the show open,
    // "off" forces it closed. Also controllable over MQTT.
    public string ShowMode { get; set; } = "auto";

    // Off-season message shown on the splash screen
    public string OffSeasonMessage { get; set; } = "The holiday light show is not running right now. Check back soon!";

    // Weekly show schedule (JSON: [{day,start,end,enabled}])
    public string ShowScheduleJson { get; set; } = "[]";

    // MQTT
    public bool MqttEnabled { get; set; } = false;
    public string MqttBrokerHost { get; set; } = string.Empty;
    public int MqttBrokerPort { get; set; } = 1883;
    public string MqttUsername { get; set; } = string.Empty;
    public string MqttPassword { get; set; } = string.Empty;
    public string MqttTopicPrefix { get; set; } = "qtm";
}
