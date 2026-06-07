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
    public string? ProgramStatus { get; set; }
    public decimal? Price { get; set; }
}
