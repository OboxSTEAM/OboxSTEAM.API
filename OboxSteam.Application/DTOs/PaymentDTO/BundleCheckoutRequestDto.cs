using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Request body for POST /api/payments/checkout/bundle (student pays).</summary>
public sealed class BundleCheckoutRequestDto
{
    public Guid BundleId { get; set; }

    public PaymentGateway Gateway { get; set; }

    /// <summary>Optional catalog voucher. Ignored on parent-pay and retake.</summary>
    [MaxLength(50)]
    public string? VoucherCode { get; set; }
}
