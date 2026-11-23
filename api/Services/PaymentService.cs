using Microsoft.EntityFrameworkCore;
using Stripe;
using XlightsQueue.Data;

namespace XlightsQueue.Services;

public record VerifiedPayment(bool Ok, decimal Amount, string? Error);

public class PaymentService(ILogger<PaymentService> logger, IWebHostEnvironment env, IDbContextFactory<AppDbContext> dbFactory) {
    public const string DevIntentId = "dev_test";

    /// <summary>
    /// Serialises "verify intent → record donation" so a single Stripe payment
    /// can never be redeemed twice by racing requests. Callers must hold it for the
    /// whole verify + redeem sequence.
    /// </summary>
    public SemaphoreSlim RedeemLock { get; } = new(1, 1);

    public async Task<PaymentIntent> CreateIntentAsync(long amountCents, Dictionary<string, string> metadata) {
        var options = new PaymentIntentCreateOptions {
            Amount = amountCents,
            Currency = "usd",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Metadata = metadata
        };
        var service = new PaymentIntentService();
        return await service.CreateAsync(options);
    }

    /// <summary>
    /// Confirms the intent succeeded, was created by this server for the expected purpose,
    /// covers at least <paramref name="minAmount"/>, and has never been redeemed before.
    /// Pass the expected metadata (e.g. songId) in <paramref name="expectedMetadata"/>.
    /// </summary>
    public async Task<VerifiedPayment> VerifyIntentAsync(
        string? paymentIntentId, string expectedType, decimal minAmount,
        IReadOnlyDictionary<string, string>? expectedMetadata = null) {

        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return new(false, 0, "Payment not verified.");

        if (env.IsDevelopment() && paymentIntentId == DevIntentId)
            return new(true, minAmount, null);

        try {
            var service = new PaymentIntentService();
            var intent = await service.GetAsync(paymentIntentId, new PaymentIntentGetOptions { Expand = ["latest_charge"] });

            if (intent.Status != "succeeded")
                return new(false, 0, "Payment not confirmed.");

            if (!intent.Metadata.TryGetValue("type", out var type) || type != expectedType)
                return new(false, 0, "Payment was not made for this action.");

            if (expectedMetadata != null) {
                foreach (var (key, value) in expectedMetadata) {
                    if (!intent.Metadata.TryGetValue(key, out var actual) || actual != value)
                        return new(false, 0, "Payment was made for a different item.");
                }
            }

            if (intent.LatestCharge is { AmountRefunded: > 0 })
                return new(false, 0, "This payment was refunded.");

            var paid = intent.AmountReceived / 100m;
            if (paid < minAmount)
                return new(false, 0, "Payment amount is too low.");

            await using var db = await dbFactory.CreateDbContextAsync();
            if (await db.Donations.AnyAsync(d => d.StripePaymentIntentId == paymentIntentId))
                return new(false, 0, "This payment has already been used.");

            return new(true, paid, null);
        } catch (Exception ex) {
            logger.LogWarning("Stripe verify failed for {Id}: {Message}", paymentIntentId, ex.Message);
            return new(false, 0, "Payment not verified.");
        }
    }

    /// <summary>Refunds a verified payment that could not be fulfilled. Returns false if the refund failed.</summary>
    public async Task<bool> RefundAsync(string paymentIntentId, string reason) {
        if (env.IsDevelopment() && paymentIntentId == DevIntentId) return true;
        try {
            await new RefundService().CreateAsync(new RefundCreateOptions {
                PaymentIntent = paymentIntentId,
                Metadata = new Dictionary<string, string> { ["reason"] = reason }
            });
            logger.LogWarning("Refunded {Id}: {Reason}", paymentIntentId, reason);
            return true;
        } catch (Exception ex) {
            logger.LogError(ex, "REFUND FAILED for {Id} ({Reason}) — refund manually in Stripe", paymentIntentId, reason);
            return false;
        }
    }
}
