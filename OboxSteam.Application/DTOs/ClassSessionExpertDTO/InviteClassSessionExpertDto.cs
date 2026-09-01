using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassSessionExpertDTO;

public sealed class InviteClassSessionExpertDto
{
    [Required]
    public Guid ClassSessionId { get; set; }

    [Required]
    public Guid ExpertId { get; set; }
}
