namespace OboxSteam.Application.DTOs.AuthDTO;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
