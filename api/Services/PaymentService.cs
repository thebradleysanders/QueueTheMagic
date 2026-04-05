using Stripe;

namespace XlightsQueue.Services;

public class PaymentService(ILogger<PaymentService> logger, IWebHostEnvironment env) {
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

    public async Task<bool> VerifyIntentAsync(string paymentIntentId) {
        if (env.IsDevelopment() && paymentIntentId == "dev_test")
            return true;

        try {
            var service = new PaymentIntentService();
            var intent = await service.GetAsync(paymentIntentId);
            return intent.Status == "succeeded";
        } catch (Exception ex) {
            logger.LogWarning("Stripe verify failed for {Id}: {Message}", paymentIntentId, ex.Message);
            return false;
        }
    }
}
