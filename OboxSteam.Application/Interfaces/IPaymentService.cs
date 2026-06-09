using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IPaymentService
{
    // ── Flow 1: Student pays directly ──────────────────────────────────────
    Task<CheckoutResponseDto> CreateDirectCheckout(Guid programId, PaymentGateway gateway);

    // ── Flow 2: Student requests parent ────────────────────────────────────
    Task RequestParentPayment(Guid programId, Guid parentId);

    // ── Flow 2: Parent opens checkout from token ────────────────────────────
    Task<CheckoutResponseDto> CreateParentCheckout(string token, PaymentGateway gateway);

    // ── Webhooks ────────────────────────────────────────────────────────────
    Task HandleStripeWebhook(string json, string signature);
    Task HandleMomoCallback(Dictionary<string, string> parameters);

    // ── Query ───────────────────────────────────────────────────────────────
    Task<PaymentResponseDto> GetPaymentById(Guid id);
}
