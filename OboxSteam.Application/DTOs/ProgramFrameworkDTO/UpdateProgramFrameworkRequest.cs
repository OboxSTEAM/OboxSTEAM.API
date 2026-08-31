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

    public bool? RequireFinalAssessment { get; set; }

    /// <summary>
    /// When true, clears <c>RequireFinalAssessment</c> (null = not enforced).
    /// Ignored when <see cref="RequireFinalAssessment"/> is set.
    /// </summary>
    public bool? ClearRequireFinalAssessment { get; set; }

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
