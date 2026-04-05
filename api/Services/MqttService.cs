using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using XlightsQueue.Data;

namespace XlightsQueue.Services;

public class MqttService(IDbContextFactory<AppDbContext> dbFactory, ILogger<MqttService> logger) : IAsyncDisposable {
    private IMqttClient? _client;
    private string _lastBrokerHost = string.Empty;
    private bool _disposed;

    public async Task PublishAsync(string subtopic, object payload) {
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
                .WithRetainFlag(false)
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
        if (_client == null || !_client.IsConnected || _lastBrokerHost != config.MqttBrokerHost) {
            _client?.Dispose();
            _client = null;

            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(config.MqttBrokerHost, config.MqttBrokerPort)
                .WithClientId($"queuethemagic-{Environment.MachineName}")
                .WithCleanSession();

            if (!string.IsNullOrEmpty(config.MqttUsername))
                optionsBuilder = optionsBuilder.WithCredentials(config.MqttUsername, config.MqttPassword);

            try {
                await _client.ConnectAsync(optionsBuilder.Build());
                _lastBrokerHost = config.MqttBrokerHost;
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

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;
        if (_client?.IsConnected == true)
            await _client.DisconnectAsync();
        _client?.Dispose();
    }
}
