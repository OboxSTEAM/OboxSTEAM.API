using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Request body for POST /api/payments/parent-checkout (parent pays via token link).</summary>
public class ParentCheckoutRequestDto
{
    public string Token { get; set; } = null!;
    public PaymentGateway Gateway { get; set; }
}
