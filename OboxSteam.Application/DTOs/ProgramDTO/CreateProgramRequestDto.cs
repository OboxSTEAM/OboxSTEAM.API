using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class CreateProgramRequestDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? SeriesName { get; set; }
    public string? Description { get; set; }
    public DifficultyLevel Level { get; set; } = DifficultyLevel.Beginner;
    public ProgramCategory Category { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SkillsGained { get; set; }
    public string? ThumbnailUrl { get; set; }
    public ProgramStatus? Status { get; set; }
    public decimal? Price { get; set; }

    /// <summary>Optional expert blueprint. Null means free-form review (no pre-check).</summary>
    public Guid? FrameworkId { get; set; }
}
