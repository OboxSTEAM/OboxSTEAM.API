using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public class CreateProgramFrameworkRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? AcademicGuidance { get; set; }

    public ProgramCategory Category { get; set; }

    public int? MinModules { get; set; }

    public int? MinOfflineSessions { get; set; }

    public int? MinLiveSessions { get; set; }

    /// <summary>
    /// When true, submit-review requires ≥1 ResearchMilestone with IsCapstone.
    /// Null or false is not enforced.
    /// </summary>
    public bool? RequireCapstoneResearchMilestone { get; set; }

    public List<FrameworkRubricCriterionRequest>? Criteria { get; set; }
}
