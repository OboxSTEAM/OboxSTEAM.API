using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MediaDTO;

public class HighlightVideoItemDto
{
    public Guid Id { get; set; }
    public Guid StackId { get; set; }
    public Guid? ParentItemId { get; set; }
    public HighlightVideoGenerationKind GenerationKind { get; set; }
    public string? VideoUrl { get; set; }
    public long? DurationMs { get; set; }
    public HighlightVideoStatus Status { get; set; }
    public DateTime? RequestedAt { get; set; }
    public string? FailureReason { get; set; }
    public string? TrimDescription { get; set; }
    public IReadOnlyList<TimeRangeDto>? TrimExcludeRanges { get; set; }
}

public class HighlightVideoStackDto
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid StudentId { get; set; }
    public string? StrengthDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<HighlightVideoItemDto> Items { get; set; } = Array.Empty<HighlightVideoItemDto>();
}

public class TimeRangeDto
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
}

public class CreateHighlightStackRequest
{
    public string? StrengthDescription { get; init; }
}

public class TrimHighlightVideoRequest
{
    public string? TrimDescription { get; init; }
    public IReadOnlyList<TimeRangeDto> ExcludeRanges { get; init; } = Array.Empty<TimeRangeDto>();
}
