using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ParentDTO;

public class MagicLoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Token { get; set; } = null!;
}
