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
    /// <summary>
    /// When true, submit-review requires ≥1 ResearchMilestone with IsCapstone.
    /// Null or false is not enforced.
    /// </summary>
    public bool? RequireCapstoneResearchMilestone { get; set; }

    /// <summary>
    /// Always true: submit still requires expert review (framework owner when
    /// attached; program-board experts when there is no framework).
    /// </summary>
    public bool RequiresExpertReview { get; set; }

    public List<FrameworkRubricCriterionResponseDto> Criteria { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
