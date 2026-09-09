using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class FrameworkCheckDto
{
    public Guid ProgramId { get; set; }

    public Guid? FrameworkVersionId { get; set; }

    public bool AllPassed { get; set; }

    public List<FrameworkCheckItemDto> Checks { get; set; } = [];
}

public sealed class FrameworkCheckItemDto
{
    public string Code { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string? Expected { get; set; }

    public string? Actual { get; set; }

    public bool Passed { get; set; }

    public List<AffectedCurriculumLinkDto> AffectedCurriculumLinks { get; set; } = [];
}

public sealed class AffectedCurriculumLinkDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid Id { get; set; }

    public string Label { get; set; } = null!;
}
