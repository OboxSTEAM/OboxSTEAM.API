using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Interfaces;

public interface IStripePaymentService
{
    /// <summary>Creates a Stripe Checkout Session and returns the hosted URL.</summary>
    Task<(string url, string sessionId)> CreateCheckoutSession(Payment payment, string programName, string? description, string? thumbnailUrl, string successUrl, string cancelUrl);

    /// <summary>
    /// Parses an incoming Stripe webhook event and returns
    /// (eventType, sessionId, transactionId).
    /// </summary>
    Task<(string eventType, string sessionId, string? transactionId)> ParseWebhookEvent(
        string json, string stripeSignature);
}
