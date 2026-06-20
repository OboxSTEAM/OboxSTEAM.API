using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MaterialDTO;

public class MaterialResponseDto
{
    public Guid Id { get; set; }

    public Guid ActivityId { get; set; }

    public string Title { get; set; } = null!;

    public MaterialType MaterialType { get; set; }

    public string? FileUrl { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>The uploader (CreatedBy from BaseEntity).</summary>
    public Guid UploaderId { get; set; }

    public DateTime UploadedAt { get; set; }
}
