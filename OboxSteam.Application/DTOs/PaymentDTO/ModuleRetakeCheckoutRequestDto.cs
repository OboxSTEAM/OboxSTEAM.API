using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PaymentDTO;

public class ModuleRetakeCheckoutRequestDto
{
    public Guid ModuleEnrollmentId { get; set; }
    public PaymentGateway Gateway { get; set; }
}
