using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.Services;

namespace XlightsQueue.Controllers;

/// <summary>"Listen live": show audio relayed from FPP to phones over a WebSocket.</summary>
[ApiController]
[Route("api/listen")]
public class ListenController(AudioRelayService relay, IDbContextFactory<AppDbContext> dbFactory) : ControllerBase {

    [HttpGet("status")]
    public async Task<IActionResult> Status() {
        var config = await GetConfigAsync();
        var enabled = config is { ListenEnabled: true };
        return Ok(new { enabled, live = enabled && relay.IsLive });
    }

    [EnableRateLimiting("listen")]
    [Route("ws")]
    public async Task Stream() {
        if (!HttpContext.WebSockets.IsWebSocketRequest) {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        var config = await GetConfigAsync();
        if (config is not { ListenEnabled: true }) {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await relay.ServeAsync(socket, Math.Max(1, config.ListenMaxListeners), HttpContext.RequestAborted);
    }

    private async Task<Models.ShowConfig?> GetConfigAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ShowConfigs.FindAsync(1);
    }
}
