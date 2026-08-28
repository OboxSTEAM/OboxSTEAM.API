namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Response returned after a checkout session is created.</summary>
public class CheckoutResponseDto
{
    public Guid PaymentId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ClassId { get; set; }
    public DateTimeOffset HoldExpiresAt { get; set; }
    public string CheckoutUrl { get; set; } = null!;
    public string? AccessToken { get; set; }
}
