using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string PasswordHash { get; set; } = null!;
    public bool Gender { get; set; } = false;
    public string? PaymentQrCodeUrl { get; set; }
    // JWT Token
    [MaxLength(128)] public string? RefreshToken { get; set; }

    [MaxLength(128)] public DateTime? RefreshTokenExpiryTime { get; set; }

    // Status check email đã được verify hay chưa
    public bool IsEmailVerified { get; set; }
}
