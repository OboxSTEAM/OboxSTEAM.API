namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Response returned after a checkout session is created or a zero-total purchase is activated.</summary>
public class CheckoutResponseDto
{
    public Guid PaymentId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ClassId { get; set; }
    public DateTimeOffset HoldExpiresAt { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? AccessToken { get; set; }

    public Guid? BundleEnrollmentId { get; set; }

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>True when final amount was 0 and fulfillment ran without Stripe.</summary>
    public bool Activated { get; set; }
}
