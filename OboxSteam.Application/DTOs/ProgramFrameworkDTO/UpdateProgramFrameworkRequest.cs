using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public class UpdateProgramFrameworkRequest
{
    [MaxLength(255)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public ProgramCategory? Category { get; set; }

    public int? MinModules { get; set; }

    public int? MinOfflineSessions { get; set; }

    public int? MinLiveSessions { get; set; }

    /// <summary>
    /// When true, submit-review requires ≥1 ResearchMilestone with IsCapstone.
    /// Null or false is not enforced.
    /// </summary>
    public bool? RequireCapstoneResearchMilestone { get; set; }

    /// <summary>
    /// When true, clears <c>RequireCapstoneResearchMilestone</c> (null = not enforced).
    /// Ignored when <see cref="RequireCapstoneResearchMilestone"/> is set.
    /// </summary>
    public bool? ClearRequireCapstoneResearchMilestone { get; set; }

    /// <summary>
    /// When true, clears <c>MinModules</c>. Ignored when <see cref="MinModules"/> is set.
    /// </summary>
    public bool? ClearMinModules { get; set; }

    /// <summary>
    /// When true, clears <c>MinOfflineSessions</c>. Ignored when <see cref="MinOfflineSessions"/> is set.
    /// </summary>
    public bool? ClearMinOfflineSessions { get; set; }

    /// <summary>
    /// When true, clears <c>MinLiveSessions</c>. Ignored when <see cref="MinLiveSessions"/> is set.
    /// </summary>
    public bool? ClearMinLiveSessions { get; set; }
}
