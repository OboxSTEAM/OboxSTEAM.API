using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramsResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? SeriesName { get; set; }
    public string? Description { get; set; }
    public DifficultyLevel Level { get; set; }
    public ProgramCategory Category { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SkillsGained { get; set; }
    public decimal? Rating { get; set; }
    public int TotalReviews { get; set; }
    public string? ThumbnailUrl { get; set; }
    public ProgramStatus Status { get; set; }
    public decimal? Price { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ModulesResponseDto> Modules { get; set; } = new();
    public List<ProgramExpertSummaryDto> Experts { get; set; } = new();
}
