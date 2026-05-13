namespace OboxSteam.Application.DTOs.UserDTO;

public class UpdateUserDto
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public bool? Gender { get; set; }
    public string? PaymentQrCodeUrl { get; set; }
}
