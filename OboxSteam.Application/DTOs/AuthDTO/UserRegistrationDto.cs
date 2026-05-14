using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.AuthDTO;

public class UserRegistrationDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Full name is required")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters long")]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    public string? Phone { get; set; }
}
