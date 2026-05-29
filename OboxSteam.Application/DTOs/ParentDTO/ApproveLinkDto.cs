using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ParentDTO;

public class ApproveLinkDto
{
    [Required]
    public Guid StudentId { get; set; }

    [Required]
    public string Token { get; set; } = null!;
}