namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Rotating QR check-in credentials for an Offline session, shown by the mentor.
/// Both the token (embedded in the QR) and the 6-digit fallback code share the same expiry.
/// </summary>
public class ClassSessionCheckInTokenResponseDto
{
    public Guid ClassSessionId { get; set; }

    public Guid Token { get; set; }

    public string Code { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
}
