using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public class FrameworkRubricCriterionRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxScore { get; set; }

    public int? DisplayOrder { get; set; }
}
