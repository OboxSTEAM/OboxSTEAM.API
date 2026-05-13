using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.AuthDTO;

public class TokenRefreshRequestDto
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = null!;
}
