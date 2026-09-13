using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

/// <summary>
/// Purchased pathway for "Lộ trình của tôi": ordered nodes, overall percent, optional certificate.
/// </summary>
public sealed class MyBundlePathwayDto
{
    public Guid BundleEnrollmentId { get; set; }

    public Guid BundleId { get; set; }

    public Guid StudentId { get; set; }

    public string BundleCode { get; set; } = null!;

    public string BundleName { get; set; } = null!;

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    public BundleEnrollmentStatus Status { get; set; }

    /// <summary>Mean of child program enrollments, including programs owned before purchase.</summary>
    public decimal ProgressPercent { get; set; }

    public IReadOnlyList<MyBundlePathwayItemDto> Items { get; set; } =
        Array.Empty<MyBundlePathwayItemDto>();

    /// <summary>Set when a pathway certificate has been issued.</summary>
    public MyBundlePathwayCertificateDto? Certificate { get; set; }

    public DateTime CreatedAt { get; set; }
}
