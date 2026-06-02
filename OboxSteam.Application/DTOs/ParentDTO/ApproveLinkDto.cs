using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ParentDTO;

public class ApproveLinkDto
{
    [Required]
    public string Token { get; set; } = null!;
}