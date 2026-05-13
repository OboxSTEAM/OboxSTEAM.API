using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.AuthDTO;

public class VerifyOtpDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "OTP is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 characters")]
    public string Otp { get; set; } = null!;
}
