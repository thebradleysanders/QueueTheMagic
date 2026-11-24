using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.Models;

namespace XlightsQueue.Services;

/// <summary>
/// "Listen live" without WebRTC or port forwarding.
///
/// FPP sends the show audio as Opus RTP to this service on the LAN (UDP, default 5004). We strip
/// the RTP header and fan the raw Opus packets out to listeners over a WebSocket on the site's
/// normal HTTP(S) port. The browser decodes Opus and plays it with a small buffer.
/// Nothing is re-encoded, and the UDP port never needs to be reachable from the internet.
/// </summary>
public class AudioRelayService(IConfiguration configuration, IDbContextFactory<AppDbContext> dbFactory, ILogger<AudioRelayService> logger) : BackgroundService {
    private readonly ConcurrentDictionary<Guid, Channel<ReadOnlyMemory<byte>>> _listeners = new();
    private long _lastPacketTicks;
    private long _packets;

    public int ListenerCount => _listeners.Count;

    /// <summary>True when FPP audio arrived in the last few seconds.</summary>
    public bool IsLive => Environment.TickCount64 - Interlocked.Read(ref _lastPacketTicks) < 3000 && Interlocked.Read(ref _packets) > 0;

    public int RtpPort => int.TryParse(configuration["Listen:RtpPort"], out var p) ? p : 5004;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        UdpClient udp;
        try {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, RtpPort));
        } catch (Exception ex) {
            logger.LogWarning("Listen live: could not open UDP {Port} for FPP audio ({Message}). Live audio is unavailable.", RtpPort, ex.Message);
            return;
        }
        logger.LogInformation("Listen live: waiting for FPP Opus RTP on UDP {Port}", RtpPort);

        using (udp) {
            EndPoint? lastSource = null;
            while (!stoppingToken.IsCancellationRequested) {
                UdpReceiveResult result;
                try { result = await udp.ReceiveAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { continue; }

                var payload = ExtractRtpPayload(result.Buffer);
                if (payload.IsEmpty) continue;

                if (!Equals(lastSource, result.RemoteEndPoint)) {
                    lastSource = result.RemoteEndPoint;
                    logger.LogInformation("Listen live: receiving audio from {Source}", result.RemoteEndPoint);
                }
                Interlocked.Exchange(ref _lastPacketTicks, Environment.TickCount64);
                Interlocked.Increment(ref _packets);

                // Fan out. Each listener has a small queue; a slow phone drops its oldest packets
                // instead of building up delay or slowing everyone else down.
                foreach (var ch in _listeners.Values) ch.Writer.TryWrite(payload);
            }
        }
    }

    /// <summary>Strips the RTP header (CSRCs, extension, padding). Returns empty if it isn't RTP v2.</summary>
    public static ReadOnlyMemory<byte> ExtractRtpPayload(byte[] p) {
        if (p.Length < 12 || (p[0] >> 6) != 2) return ReadOnlyMemory<byte>.Empty;
        var offset = 12 + (p[0] & 0x0F) * 4;
        if ((p[0] & 0x10) != 0) {
            if (p.Length < offset + 4) return ReadOnlyMemory<byte>.Empty;
            offset += 4 + ((p[offset + 2] << 8) | p[offset + 3]) * 4;
        }
        var end = p.Length;
        if ((p[0] & 0x20) != 0) end -= p[^1];
        return end > offset ? new ReadOnlyMemory<byte>(p, offset, end - offset) : ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>Streams audio to one WebSocket until it closes. Returns false if the listener cap is reached.</summary>
    public async Task ServeAsync(WebSocket socket, string sessionToken, int maxListeners, CancellationToken ct) {
        if (_listeners.Count >= maxListeners) {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "full", ct);
            return;
        }

        var id = Guid.NewGuid();
        // ~0.5 s of 20 ms packets; anything older is useless for a live show.
        var channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(25) {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true
        });
        _listeners[id] = channel;
        var sessionId = await StartSessionAsync(sessionToken, _listeners.Count);
        var started = DateTime.UtcNow;
        try {
            // Tell the browser what to expect before any audio.
            await socket.SendAsync("{\"codec\":\"opus\",\"sampleRate\":48000,\"channels\":2}"u8.ToArray(),
                WebSocketMessageType.Text, true, ct);

            // Notice the client closing the socket.
            var readTask = DrainAsync(socket, ct);

            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open) {
                var wait = channel.Reader.WaitToReadAsync(ct).AsTask();
                if (await Task.WhenAny(wait, readTask) == readTask || !await wait) break;
                while (channel.Reader.TryRead(out var packet))
                    await socket.SendAsync(packet, WebSocketMessageType.Binary, true, ct);
            }
        } catch (Exception ex) when (ex is OperationCanceledException or WebSocketException) {
            /* listener left */
        } finally {
            _listeners.TryRemove(id, out _);
            await EndSessionAsync(sessionId, (int)Math.Round((DateTime.UtcNow - started).TotalSeconds));
            if (socket.State == WebSocketState.Open)
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch { }
        }
    }

    // Stats are best-effort: a database hiccup must never interrupt the audio.
    private async Task<int?> StartSessionAsync(string token, int concurrent) {
        try {
            await using var db = await dbFactory.CreateDbContextAsync();
            var s = new ListenSession { SessionToken = token, ListenersAtStart = concurrent };
            db.ListenSessions.Add(s);
            await db.SaveChangesAsync();
            return s.Id;
        } catch (Exception ex) {
            logger.LogDebug("Listen stats start failed: {Message}", ex.Message);
            return null;
        }
    }

    private async Task EndSessionAsync(int? id, int seconds) {
        if (id == null) return;
        try {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.ListenSessions.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.Seconds, seconds));
        } catch (Exception ex) {
            logger.LogDebug("Listen stats end failed: {Message}", ex.Message);
        }
    }

    private static async Task DrainAsync(WebSocket socket, CancellationToken ct) {
        var buf = new byte[256];
        try {
            while (socket.State == WebSocketState.Open) {
                var r = await socket.ReceiveAsync(buf, ct);
                if (r.MessageType == WebSocketMessageType.Close) return;
            }
        } catch { /* closed */ }
    }
}
