namespace XlightsQueue.Services;

/// <summary>
/// Keeps the MQTT connection open so the show can be switched on/off remotely, and routes
/// <c>&lt;prefix&gt;/control/show</c> messages to the show override.
/// </summary>
public class MqttControlWorker(MqttService mqtt, ShowControlService showControl, ILogger<MqttControlWorker> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        mqtt.ShowControlReceived += mode => showControl.SetModeAsync(mode, "mqtt");

        while (!stoppingToken.IsCancellationRequested) {
            try { await mqtt.EnsureConnectedAsync(); }
            catch (Exception ex) { logger.LogDebug("MQTT keep-alive: {Message}", ex.Message); }

            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) { }
        }
    }
}
