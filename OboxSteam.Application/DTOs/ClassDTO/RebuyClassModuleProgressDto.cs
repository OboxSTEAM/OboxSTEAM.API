using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>How far one cohort has gotten through a program module (session status, not student grades).</summary>
public sealed class RebuyClassModuleProgressDto
{
    public Guid ModuleId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public ClassModuleProgressStatus Progress { get; set; }

    /// <summary>
    /// True when this module is the student's stop module or later and the class has started it.
    /// </summary>
    public bool BlocksRebuy { get; set; }
}
