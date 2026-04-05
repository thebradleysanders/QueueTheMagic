using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Models;
using XlightsQueue.Services;

namespace XlightsQueue.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController(
    PaymentService paymentService,
    MqttService mqttService,
    IDbContextFactory<AppDbContext> dbFactory) : ControllerBase {
    [HttpPost("create-intent")]
    public async Task<IActionResult> CreateIntent([FromBody] CreatePaymentIntentRequest request) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return StatusCode(500, new { error = "Configuration not found." });

        var metadata = new Dictionary<string, string> { ["type"] = request.Type, ["sessionToken"] = request.SessionToken };
        long amountCents;

        if (request.Type == "bump") {
            amountCents = (long)(config.BumpCost * 100);
            if (request.SongId.HasValue) metadata["queueItemId"] = request.SongId.Value.ToString();
        } else if (request.Type == "donate") {
            var requested = request.Amount ?? config.DonateCost;
            amountCents = (long)(Math.Max(requested, config.DonateCost) * 100);
        } else {
            amountCents = (long)(config.SongRequestCost * 100);
            if (request.SongId.HasValue) metadata["songId"] = request.SongId.Value.ToString();
        }

        var intent = await paymentService.CreateIntentAsync(amountCents, metadata);
        return Ok(new { clientSecret = intent.ClientSecret });
    }

    [HttpPost("donate")]
    public async Task<IActionResult> Donate([FromBody] DonateRequest request) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return StatusCode(500);

        var isDev = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        decimal amount;

        if (isDev && request.PaymentIntentId == "dev_test") {
            amount = request.Amount;
        } else {
            var verified = await paymentService.VerifyIntentAsync(request.PaymentIntentId);
            if (!verified) return BadRequest(new { error = "Payment not confirmed." });
            amount = request.Amount;
        }

        amount = Math.Max(amount, config.DonateCost);

        db.Donations.Add(new Donation {
            StripePaymentIntentId = request.PaymentIntentId,
            Amount = amount,
            Type = DonationType.Donation,
            SessionToken = request.SessionToken,
        });
        await db.SaveChangesAsync();

        await mqttService.PublishAsync("donation", new { amount, type = "just_because" });

        return Ok(new { success = true });
    }
}
