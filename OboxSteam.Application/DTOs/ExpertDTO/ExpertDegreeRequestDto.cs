using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertDegreeRequestDto
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string Institution { get; set; } = null!;

    public int Year { get; set; }
}
