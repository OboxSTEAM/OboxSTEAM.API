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

    /// <summary>Replaces catalog skills when set. Null leaves the current links unchanged. Empty clears them.</summary>
    public List<Guid>? SkillIds { get; set; }
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Catalog toggle only: Active ↔ Inactive when already in one of those states.
    /// Draft and Approved cannot be set here.
    /// </summary>
    public ProgramStatus? Status { get; set; }
    public decimal? Price { get; set; }

    /// <summary>Locked after creation. Null or the current value is ignored; any other value returns 409 FRAMEWORK_LOCKED.</summary>
    public Guid? FrameworkId { get; set; }

    /// <summary>Locked after creation. Null or the current value is ignored; upgrade via POST framework-version.</summary>
    public Guid? FrameworkVersionId { get; set; }

    /// <summary>Locked after creation. True on a program with a framework returns 409 FRAMEWORK_LOCKED.</summary>
    public bool? ClearFramework { get; set; }
}
