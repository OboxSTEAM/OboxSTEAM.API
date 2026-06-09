using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Request body for POST /api/payments/checkout (direct student checkout).</summary>
public class CheckoutRequestDto
{
    public Guid ProgramId { get; set; }
    public PaymentGateway Gateway { get; set; }
}
