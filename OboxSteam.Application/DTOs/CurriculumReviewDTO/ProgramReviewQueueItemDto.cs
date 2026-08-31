using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.CurriculumReviewDTO;

public sealed class ProgramReviewQueueItemDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ProgramStatus Status { get; set; }

    public Guid FrameworkId { get; set; }

    public string? FrameworkName { get; set; }

    public Guid ExpertId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
