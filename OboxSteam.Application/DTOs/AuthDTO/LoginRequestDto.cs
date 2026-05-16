using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.AuthDTO;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [DefaultValue("superadmin@oboxsteam.com")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [DefaultValue("Admin@123")]
    public string Password { get; set; } = null!;
}
