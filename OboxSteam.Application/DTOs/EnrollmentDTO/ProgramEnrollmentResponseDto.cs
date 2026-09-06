using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Program enrollment with the enrolled program's catalog information.
/// </summary>
public class ProgramEnrollmentResponseDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ProgramId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public decimal ProgressPercent { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ProgramPurchaseEndReason? EndReason { get; set; }
    public Guid? EndedModuleId { get; set; }
    public DateTime? EndedAt { get; set; }
    public Guid? SourceProgramEnrollmentId { get; set; }

    /// <summary>True when this row continues a prior closed purchase (<see cref="SourceProgramEnrollmentId"/> set).</summary>
    public bool IsRebuy { get; set; }

    /// <summary>1 for a first purchase; 2+ when linked through a rebuy source chain.</summary>
    public int AttemptNumber { get; set; }

    /// <summary>Status of the immediate source purchase, when this is a rebuy.</summary>
    public EnrollmentStatus? PriorStatus { get; set; }

    /// <summary>End reason of the immediate source purchase, when this is a rebuy.</summary>
    public ProgramPurchaseEndReason? PriorEndReason { get; set; }

    /// <summary>True when a later Active rebuy superseded this terminal row.</summary>
    public bool IsSuperseded { get; set; }

    public Guid? SupersededByEnrollmentId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? SeriesName { get; set; }
    public string? Description { get; set; }
    public DifficultyLevel Level { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SkillsGained { get; set; }
    public decimal? Rating { get; set; }
    public int TotalReviews { get; set; }
    public string? ThumbnailUrl { get; set; }
    public ProgramStatus ProgramStatus { get; set; }
    public decimal? Price { get; set; }
}
