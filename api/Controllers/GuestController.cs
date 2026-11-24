using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.Hubs;
using XlightsQueue.Services;

namespace XlightsQueue.Controllers;

public record GuestSignInRequest(string Code);
public record GuestQueueRequest(int SongId);

/// <summary>Friends &amp; family: sign in with a pass code, then request songs for free at any time.</summary>
[ApiController]
[Route("api/guest")]
public class GuestController(
    GuestService guests,
    QueueService queueService,
    IQueueTrigger queueTrigger,
    IHubContext<ShowHub> hub,
    IDbContextFactory<AppDbContext> dbFactory) : ControllerBase {

    [EnableRateLimiting("login")]
    [HttpPost("signin")]
    public async Task<IActionResult> SignIn([FromBody] GuestSignInRequest request) {
        if (!await Enabled()) return NotFound(new { error = "Guest sign-in is turned off." });
        var result = await guests.SignInAsync(request.Code);
        if (result == null) return Unauthorized(new { error = "That code isn't right. Check it and try again." });
        return Ok(new { token = result.Value.Token, name = result.Value.Pass.Name });
    }

    /// <summary>Is this token still good? (a revoked pass returns 401)</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me() {
        var pass = await Authenticate();
        return pass == null ? Unauthorized() : Ok(new { name = pass.Name });
    }

    [EnableRateLimiting("payment")]
    [HttpPost("queue")]
    public async Task<IActionResult> Queue([FromBody] GuestQueueRequest request) {
        var pass = await Authenticate();
        if (pass == null) return Unauthorized(new { error = "Your guest sign-in expired. Please sign in again." });

        var (item, error) = await queueService.AddGuestToQueueAsync(request.SongId, pass);
        if (error != null) return BadRequest(new { error });

        await guests.RecordRequestAsync(pass.Id);
        queueTrigger.TriggerImmediateCheck();
        await hub.Clients.All.SendAsync("QueueUpdated", await queueService.GetQueueAsync());
        return Ok(new { queueItemId = item!.Id });
    }

    private async Task<Models.GuestPass?> Authenticate() {
        if (!await Enabled()) return null;
        return await guests.ValidateAsync(Request.Headers["X-Guest-Token"].ToString());
    }

    private async Task<bool> Enabled() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.ShowConfigs.FindAsync(1))?.GuestPassesEnabled == true;
    }
}
