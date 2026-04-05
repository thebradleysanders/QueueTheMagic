using Microsoft.AspNetCore.SignalR;

namespace XlightsQueue.Hubs;

public class ShowHub : Hub {
    // Clients subscribe; server pushes events:
    // QueueUpdated        — full queue list
    // NowPlayingChanged   — current track
    // ShowConfigUpdated   — public config fields
    // QueueItemAdded      — single new item
}
