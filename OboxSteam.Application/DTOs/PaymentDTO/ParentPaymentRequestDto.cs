namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Request body for POST /api/payments/request-parent (student asks parent to pay).</summary>
public class ParentPaymentRequestDto
{
    public Guid ProgramId { get; set; }
    public Guid ClassId { get; set; }
    public Guid ParentId { get; set; }
}
