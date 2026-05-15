using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramUpdateDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? SeriesName { get; set; }
    public string? Description { get; set; }
    public DifficultyLevel? Level { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SkillsGained { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Status { get; set; }
    public decimal? Price { get; set; }
}
