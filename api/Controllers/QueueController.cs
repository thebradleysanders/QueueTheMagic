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

    [HttpPost]
    public async Task<IActionResult> AddToQueue([FromBody] AddToQueueRequest request) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config != null && !config.IsSeasonActive)
            return BadRequest(new { error = "The show is not running this season." });
        if (config != null && !ScheduleHelper.IsOpen(config.ShowScheduleJson))
            return BadRequest(new { error = "The show is currently closed. Please come back during show hours." });

        if (!await paymentService.VerifyIntentAsync(request.PaymentIntentId))
            return BadRequest(new { error = "Payment not verified." });

        var (item, error) = await queueService.AddToQueueAsync(request.SongId, request.PaymentIntentId, request.SessionToken);
        if (error != null) return BadRequest(new { error });

        queueTrigger.TriggerImmediateCheck();
        var queue = await queueService.GetQueueAsync();
        await hub.Clients.All.SendAsync("QueueUpdated", queue);

        return Ok(new { queueItemId = item!.Id });
    }

    [HttpPut("{id}/bump")]
    public async Task<IActionResult> Bump(int id, [FromBody] BumpQueueRequest request) {
        if (!await paymentService.VerifyIntentAsync(request.PaymentIntentId))
            return BadRequest(new { error = "Payment not verified." });

        var (success, error) = await queueService.BumpAsync(id, request.PaymentIntentId, request.SessionToken);
        if (!success) return BadRequest(new { error });

        queueTrigger.TriggerImmediateCheck();
        var queue = await queueService.GetQueueAsync();
        await hub.Clients.All.SendAsync("QueueUpdated", queue);

        return Ok(new { success = true });
    }
}
