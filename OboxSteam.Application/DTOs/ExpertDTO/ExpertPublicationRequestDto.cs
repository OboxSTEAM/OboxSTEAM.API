using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertPublicationRequestDto
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = null!;

    [MaxLength(255)]
    public string? Venue { get; set; }

    public int Year { get; set; }

    [MaxLength(2048)]
    public string? Url { get; set; }
}
