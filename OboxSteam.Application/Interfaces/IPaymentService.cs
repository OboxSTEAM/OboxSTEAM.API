using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IPaymentService
{
    // ── Flow 1: Student pays directly ──────────────────────────────────────
    Task<CheckoutResponseDto> CreateDirectCheckout(
        Guid programId,
        Guid classId,
        PaymentGateway gateway,
        string? voucherCode = null);
    Task<CheckoutResponseDto> CreateBundleCheckout(Guid bundleId, PaymentGateway gateway, string? voucherCode = null);
    Task<CheckoutResponseDto> CreateModuleRetakeCheckout(Guid moduleEnrollmentId, PaymentGateway gateway);

    // ── Flow 2: Student requests parent ────────────────────────────────────
    Task RequestParentPayment(Guid programId, Guid classId, Guid parentId);
    Task RequestParentBundlePayment(Guid bundleId, Guid parentId);
    Task RequestParentModulePayment(Guid moduleEnrollmentId, Guid parentId);

    // ── Flow 2: Parent opens checkout from token ────────────────────────────
    Task<CheckoutResponseDto> CreateParentCheckout(string token, PaymentGateway gateway);

    // ── Webhooks ────────────────────────────────────────────────────────────
    Task HandleStripeWebhook(string json, string signature);

    // ── Cancel (FE gọi khi redirect về cancelUrl) ────────────────────────────
    Task CancelPayment(Guid paymentId);

    // ── Query ───────────────────────────────────────────────────────────────
    Task<PaymentResponseDto> GetPaymentById(Guid id);
}
