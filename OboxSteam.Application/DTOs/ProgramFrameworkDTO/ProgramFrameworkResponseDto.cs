using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public class ProgramFrameworkResponseDto
{
    public Guid Id { get; set; }
    public Guid ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ProgramCategory Category { get; set; }
    public int? MinModules { get; set; }
    public int? MinOfflineSessions { get; set; }
    public int? MinLiveSessions { get; set; }
    public bool? RequireFinalAssessment { get; set; }

    /// <summary>
    /// True when the framework has at least one rubric criterion.
    /// Programs on a framework without criteria may submit without waiting for expert review
    /// (wired in the submit-review slice).
    /// </summary>
    public bool RequiresExpertReview { get; set; }

    public List<FrameworkRubricCriterionResponseDto> Criteria { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
