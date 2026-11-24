using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.Hubs;
using XlightsQueue.Models;

namespace XlightsQueue.Services;

/// <summary>
/// The manual show override (auto / on / off). Shared by the admin switch and the MQTT control topic.
/// </summary>
public class ShowControlService(
    IDbContextFactory<AppDbContext> dbFactory,
    IHubContext<ShowHub> hub,
    MqttService mqtt,
    IQueueTrigger trigger,
    IConfiguration configuration,
    ILogger<ShowControlService> logger) {

    /// <summary>Sets the override. Returns the updated config, or null if the mode is invalid.</summary>
    public async Task<ShowConfig?> SetModeAsync(string? rawMode, string source) {
        var mode = ScheduleHelper.ParseMode(rawMode);
        if (mode == null) {
            logger.LogWarning("Ignoring unknown show mode '{Mode}' from {Source}", rawMode, source);
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return null;

        if (config.ShowMode != mode) {
            config.ShowMode = mode;
            await db.SaveChangesAsync();
            logger.LogInformation("Show mode set to {Mode} via {Source}", mode, source);
        }

        await AnnounceAsync(config);
        return config;
    }

    /// <summary>Pushes the current public config to every browser, publishes the MQTT status, and wakes the queue.</summary>
    public async Task AnnounceAsync(ShowConfig config) {
        var publicConfig = PublicConfigBuilder.Build(config, configuration["Stripe:PublishableKey"] ?? string.Empty);
        await hub.Clients.All.SendAsync("ShowConfigUpdated", publicConfig);
        await PublishStatusAsync(config);
        trigger.TriggerImmediateCheck();
    }

    /// <summary>Retained so a new subscriber (e.g. Home Assistant after a restart) learns the state immediately.</summary>
    public Task PublishStatusAsync(ShowConfig config) =>
        mqtt.PublishAsync("status", new {
            mode = config.ShowMode,
            seasonActive = ScheduleHelper.IsSeasonActive(config),
            open = ScheduleHelper.IsOpen(config),
            running = ScheduleHelper.IsRunning(config),
            seasonStart = config.SeasonStart?.ToString("yyyy-MM-dd"),
            seasonEnd = config.SeasonEnd?.ToString("yyyy-MM-dd"),
        }, retain: true);
}
