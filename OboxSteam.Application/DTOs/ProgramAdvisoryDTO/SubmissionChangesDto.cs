using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class SubmissionChangesDto
{
    public Guid SubmissionId { get; set; }

    public Guid? PreviousSubmissionId { get; set; }

    public List<SubmissionChangeItemDto> Added { get; set; } = [];

    public List<SubmissionChangeItemDto> Removed { get; set; } = [];

    public List<SubmissionChangeItemDto> Reordered { get; set; } = [];

    public List<SubmissionChangeItemDto> Modified { get; set; } = [];

    public int AddedCount => Added.Count;

    public int RemovedCount => Removed.Count;

    public int ReorderedCount => Reordered.Count;

    public int ModifiedCount => Modified.Count;
}

public sealed class SubmissionChangeItemDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid Id { get; set; }

    public string Label { get; set; } = null!;

    public string? Field { get; set; }

    public string? Before { get; set; }

    public string? After { get; set; }

    public string? Detail { get; set; }
}
