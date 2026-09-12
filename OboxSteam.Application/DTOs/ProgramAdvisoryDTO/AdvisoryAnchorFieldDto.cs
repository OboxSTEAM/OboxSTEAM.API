using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

/// <summary>One allowed advisory field-key for FE anchor wiring.</summary>
public sealed class AdvisoryAnchorFieldDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }

    public string FieldKey { get; set; } = null!;

    public string? Label { get; set; }
}
