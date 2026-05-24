using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.AuthDTO;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Token is required")]
    public string Token { get; set; } = null!;

    [Required(ErrorMessage = "New password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare("NewPassword", ErrorMessage = "New password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = null!;
}
