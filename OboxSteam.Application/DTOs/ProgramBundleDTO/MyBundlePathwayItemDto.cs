using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

/// <summary>One program node on the student's purchased pathway.</summary>
public sealed class MyBundlePathwayItemDto
{
    public Guid ItemId { get; set; }

    public Guid ProgramId { get; set; }

    public Guid? ProgramEnrollmentId { get; set; }

    public string ProgramName { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    public int SortOrder { get; set; }

    public bool RequiresPreviousCompletion { get; set; }

    public BundlePathwayItemStatus Status { get; set; }

    public decimal ProgressPercent { get; set; }
}
