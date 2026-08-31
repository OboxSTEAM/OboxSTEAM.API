using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class UpdateProgramRequestDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? SeriesName { get; set; }
    public string? Description { get; set; }
    public DifficultyLevel? Level { get; set; }
    public ProgramCategory? Category { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SkillsGained { get; set; }
    public string? ThumbnailUrl { get; set; }
    public ProgramStatus? Status { get; set; }
    public decimal? Price { get; set; }

    /// <summary>Assign a blueprint. Null is skipped (partial update). Use <see cref="ClearFramework"/> to unlink.</summary>
    public Guid? FrameworkId { get; set; }

    /// <summary>When true, clears <c>Program.FrameworkId</c>. Ignored when <see cref="FrameworkId"/> is set.</summary>
    public bool? ClearFramework { get; set; }
}
