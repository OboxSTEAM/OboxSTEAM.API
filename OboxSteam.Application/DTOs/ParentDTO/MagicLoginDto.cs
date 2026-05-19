using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ParentDTO;

public class MagicLoginDto
{
    [Required]
    public string Token { get; set; } = null!;
}
