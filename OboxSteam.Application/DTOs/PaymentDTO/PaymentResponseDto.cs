using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Response DTO for payment detail queries.</summary>
public class PaymentResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Guid PaidById { get; set; }
    public Guid? ProgramEnrollmentId { get; set; }
    public Guid? ModuleEnrollmentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public PaymentGateway Gateway { get; set; }
    public string? TransactionId { get; set; }
    public string? CheckoutSessionId { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
