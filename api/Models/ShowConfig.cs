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

    // Season toggle — when false the public site shows an "off-season" splash
    public bool IsSeasonActive { get; set; } = true;

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
