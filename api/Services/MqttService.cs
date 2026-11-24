using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using XlightsQueue.Data;

namespace XlightsQueue.Services;

public class MqttService(IDbContextFactory<AppDbContext> dbFactory, ILogger<MqttService> logger) : IAsyncDisposable {
    private IMqttClient? _client;
    private string _lastConnectionKey = string.Empty;

    /// <summary>Raised when a message arrives on <c>&lt;prefix&gt;/control/show</c> (payload: on | off | auto).</summary>
    public event Func<string, Task>? ShowControlReceived;

    /// <summary>Keeps the broker connection (and the control subscription) alive. Called periodically by the worker.</summary>
    public Task EnsureConnectedAsync() => GetConnectedClientAsync();
    private bool _disposed;

    public async Task PublishAsync(string subtopic, object payload, bool retain = false) {
        try {
            var client = await GetConnectedClientAsync();
            if (client == null) return;

            await using var db = await dbFactory.CreateDbContextAsync();
            var config = await db.ShowConfigs.FindAsync(1);
            if (config == null || !config.MqttEnabled) return;

            var topic = $"{config.MqttTopicPrefix}/{subtopic}";
            var json = JsonSerializer.Serialize(payload);
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(json))
                .WithRetainFlag(retain)
                .Build();

            await client.PublishAsync(msg);
        } catch (Exception ex) {
            logger.LogWarning("MQTT publish failed: {Message}", ex.Message);
        }
    }

    private async Task<IMqttClient?> GetConnectedClientAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null || !config.MqttEnabled) return null;

        // Reconnect if broker changed or disconnected
        if (_client == null || !_client.IsConnected || _lastConnectionKey != ConnectionKey(config)) {
            _client?.Dispose();
            _client = null;

            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();
            _client.ApplicationMessageReceivedAsync += OnMessageAsync;

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(config.MqttBrokerHost, config.MqttBrokerPort)
                .WithClientId($"queuethemagic-{Environment.MachineName}")
                .WithCleanSession();

            if (!string.IsNullOrEmpty(config.MqttUsername))
                optionsBuilder = optionsBuilder.WithCredentials(config.MqttUsername, config.MqttPassword);

            try {
                await _client.ConnectAsync(optionsBuilder.Build());
                await _client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter($"{config.MqttTopicPrefix}/control/show").Build());
                _lastConnectionKey = ConnectionKey(config);
                logger.LogInformation("MQTT connected to {Host}:{Port}", config.MqttBrokerHost, config.MqttBrokerPort);
            } catch (Exception ex) {
                logger.LogWarning("MQTT connection failed: {Message}", ex.Message);
                _client.Dispose();
                _client = null;
                return null;
            }
        }

        return _client;
    }

    private static string ConnectionKey(Models.ShowConfig c) =>
        $"{c.MqttBrokerHost}|{c.MqttBrokerPort}|{c.MqttUsername}|{c.MqttPassword}|{c.MqttTopicPrefix}";

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs e) {
        try {
            var text = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment).Trim();
            // Accept a bare word ("on") or JSON ({"mode":"on"}).
            if (text.StartsWith('{')) {
                using var doc = JsonDocument.Parse(text);
                text = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";
            }
            text = text.Trim('"');
            var handler = ShowControlReceived;
            if (handler != null) await handler(text);
        } catch (Exception ex) {
            logger.LogWarning("MQTT control message failed: {Message}", ex.Message);
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;
        if (_client?.IsConnected == true)
            await _client.DisconnectAsync();
        _client?.Dispose();
    }
}
