using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Hubs;
using XlightsQueue.Services;

namespace XlightsQueue.Controllers;

[ApiController]
[Route("api/queue")]
public class QueueController(
    QueueService queueService,
    PaymentService paymentService,
    IQueueTrigger queueTrigger,
    IHubContext<ShowHub> hub,
    IDbContextFactory<AppDbContext> dbFactory) : ControllerBase {
    [HttpGet]
    public async Task<IActionResult> GetQueue() =>
        Ok(await queueService.GetQueueAsync());

    // The customer already paid, so a rejected request must give the money back.
    private async Task<IActionResult> RefundAndFail(string paymentIntentId, string error) {
        var refunded = await paymentService.RefundAsync(paymentIntentId, error);
        return BadRequest(new {
            error = refunded
                ? $"{error} You have not been charged (your payment was refunded)."
                : $"{error} We could not refund you automatically. Please contact the show owner."
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddToQueue([FromBody] AddToQueueRequest request) {
        if (string.IsNullOrWhiteSpace(request.SessionToken) || request.SessionToken.Length > 64)
            return BadRequest(new { error = "Invalid session." });

        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config != null && !ScheduleHelper.IsSeasonActive(config))
            return BadRequest(new { error = "The show is not running this season." });
        if (config != null && !ScheduleHelper.IsOpen(config))
            return BadRequest(new { error = "The show is currently closed. Please come back during show hours." });

        // Hold the lock across verify + redeem so one payment can only ever buy one song.
        await paymentService.RedeemLock.WaitAsync();
        try {
            var verified = await paymentService.VerifyIntentAsync(
                request.PaymentIntentId, "song", config?.SongRequestCost ?? 1m,
                new Dictionary<string, string> { ["songId"] = request.SongId.ToString() });
            if (!verified.Ok) return BadRequest(new { error = verified.Error });

            var (item, error) = await queueService.AddToQueueAsync(request.SongId, request.PaymentIntentId, request.SessionToken, request.DisplayName);
            if (error != null) return await RefundAndFail(request.PaymentIntentId, error);

            queueTrigger.TriggerImmediateCheck();
            var queue = await queueService.GetQueueAsync();
            await hub.Clients.All.SendAsync("QueueUpdated", queue);

            return Ok(new { queueItemId = item!.Id });
        } finally {
            paymentService.RedeemLock.Release();
        }
    }

    [HttpPut("{id}/bump")]
    public async Task<IActionResult> Bump(int id, [FromBody] BumpQueueRequest request) {
        if (string.IsNullOrWhiteSpace(request.SessionToken) || request.SessionToken.Length > 64)
            return BadRequest(new { error = "Invalid session." });

        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);

        await paymentService.RedeemLock.WaitAsync();
        try {
            var verified = await paymentService.VerifyIntentAsync(
                request.PaymentIntentId, "bump", config?.BumpCost ?? 1m,
                new Dictionary<string, string> { ["queueItemId"] = id.ToString() });
            if (!verified.Ok) return BadRequest(new { error = verified.Error });

            var (success, error) = await queueService.BumpAsync(id, request.PaymentIntentId, request.SessionToken, request.DisplayName);
            if (!success) return await RefundAndFail(request.PaymentIntentId, error ?? "Boost failed.");

            queueTrigger.TriggerImmediateCheck();
            var queue = await queueService.GetQueueAsync();
            await hub.Clients.All.SendAsync("QueueUpdated", queue);

            return Ok(new { success = true });
        } finally {
            paymentService.RedeemLock.Release();
        }
    }
}
