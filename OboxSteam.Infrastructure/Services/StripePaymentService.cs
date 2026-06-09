using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using Stripe;
using Stripe.Checkout;

namespace OboxSteam.Infrastructure.Services;

public class StripePaymentService : IStripePaymentService
{
    private readonly StripeSettings _settings;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(IOptions<StripeSettings> settings, ILogger<StripePaymentService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<(string url, string sessionId)> CreateCheckoutSession(
        Payment payment,
        string programName,
        string? description,
        string? thumbnailUrl,
        string successUrl,
        string cancelUrl)
    {
        _logger.LogInformation(
            "[Stripe] Creating checkout session for payment {PaymentId}, program: {Program}",
            payment.Id, programName);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            Currency = payment.Currency.ToLower(),
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = payment.Currency.ToLower(),
                        UnitAmount = (long)(payment.Amount), // Stripe expects cents/smallest unit
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = programName,
                            Description = string.IsNullOrWhiteSpace(description) ? $"Program enrollment — {programName}" : description,
                            Images = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : new List<string> { thumbnailUrl }
                        }
                    }
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                { "paymentId", payment.Id.ToString() }
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        _logger.LogInformation("[Stripe] Session created: {SessionId}", session.Id);

        return (session.Url, session.Id);
    }

    public Task<(string eventType, string sessionId, string? transactionId)> ParseWebhookEvent(
        string json,
        string stripeSignature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                stripeSignature,
                _settings.WebhookSecret);

            _logger.LogInformation("[Stripe] Webhook event type: {Type}", stripeEvent.Type);

            if (stripeEvent.Data.Object is Session session)
            {
                var transactionId = session.PaymentIntentId;
                return Task.FromResult((stripeEvent.Type, session.Id, (string?)transactionId));
            }

            return Task.FromResult((stripeEvent.Type, string.Empty, (string?)null));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "[Stripe] Webhook signature validation failed.");
            throw ErrorHelper.BadRequest($"Invalid Stripe webhook signature: {ex.Message}");
        }
    }
}
