using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    CommunityService community,
    IWebHostEnvironment env,
    IDbContextFactory<AppDbContext> dbFactory) : ControllerBase {

    private const decimal MaxDonation = 1000m;

    [EnableRateLimiting("payment")]
    [HttpPost("create-intent")]
    public async Task<IActionResult> CreateIntent([FromBody] CreatePaymentIntentRequest request) {
        if (string.IsNullOrWhiteSpace(request.SessionToken) || request.SessionToken.Length > 64)
            return BadRequest(new { error = "Invalid session." });

        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        if (config == null) return StatusCode(500, new { error = "Configuration not found." });

        var metadata = new Dictionary<string, string> { ["type"] = request.Type, ["sessionToken"] = request.SessionToken };
        long amountCents;

        switch (request.Type) {
            case "bump":
                if (!request.SongId.HasValue) return BadRequest(new { error = "Missing queue item." });
                amountCents = (long)Math.Round(config.BumpCost * 100);
                metadata["queueItemId"] = request.SongId.Value.ToString();
                break;
            case "donate":
                var requested = request.Amount ?? config.DonateCost;
                if (requested > MaxDonation) return BadRequest(new { error = $"Maximum donation is ${MaxDonation:0}." });
                amountCents = (long)Math.Round(Math.Max(requested, config.DonateCost) * 100);
                break;
            case "song":
                if (!request.SongId.HasValue) return BadRequest(new { error = "Missing song." });
                if (!ScheduleHelper.IsSeasonActive(config))
                    return BadRequest(new { error = "The show is not running this season." });
                if (!ScheduleHelper.IsOpen(config))
                    return BadRequest(new { error = "The show is currently closed. Please come back during show hours." });
                var songError = await QueueService.ValidateRequestAsync(db, config, request.SongId.Value, request.SessionToken);
                if (songError != null) return BadRequest(new { error = songError });
                amountCents = (long)Math.Round(config.SongRequestCost * 100);
                metadata["songId"] = request.SongId.Value.ToString();
                break;
            default:
                return BadRequest(new { error = "Invalid payment type." });
        }

        if (amountCents < 50) return BadRequest(new { error = "Amount is below the minimum." });

        var intent = await paymentService.CreateIntentAsync(amountCents, metadata);
        return Ok(new { clientSecret = intent.ClientSecret });
    }

    [EnableRateLimiting("payment")]
    [HttpPost("donate")]
    public async Task<IActionResult> Donate([FromBody] DonateRequest request) {
        if (string.IsNullOrWhiteSpace(request.SessionToken) || request.SessionToken.Length > 64)
            return BadRequest(new { error = "Invalid session." });

        await paymentService.RedeemLock.WaitAsync();
        try {
            await using var db = await dbFactory.CreateDbContextAsync();
            var config = await db.ShowConfigs.FindAsync(1);
            if (config == null) return StatusCode(500);

            var verified = await paymentService.VerifyIntentAsync(request.PaymentIntentId, "donate", config.DonateCost);
            if (!verified.Ok) return BadRequest(new { error = verified.Error });

            // The amount comes from what Stripe actually charged, never from the client
            // (the dev bypass has no real charge, so it trusts the request instead).
            var amount = env.IsDevelopment() && request.PaymentIntentId == PaymentService.DevIntentId
                ? Math.Clamp(request.Amount, config.DonateCost, MaxDonation)
                : verified.Amount;

            db.Donations.Add(new Donation {
                StripePaymentIntentId = request.PaymentIntentId,
                Amount = amount,
                Type = DonationType.Donation,
                SessionToken = request.SessionToken,
                DisplayName = DisplayNameHelper.Clean(request.DisplayName),
            });
            await db.SaveChangesAsync();

            await mqttService.PublishAsync("donation", new { amount, type = "just_because" });
            await community.AddActivityAsync("donation", null, amount, DisplayNameHelper.Clean(request.DisplayName));

            return Ok(new { success = true });
        } finally {
            paymentService.RedeemLock.Release();
        }
    }
}
