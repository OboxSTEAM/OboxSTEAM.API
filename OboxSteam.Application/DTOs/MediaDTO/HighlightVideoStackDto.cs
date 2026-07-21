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
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime? RequestedAt { get; set; }
    public string? FailureReason { get; set; }
    public string? TrimDescription { get; set; }
    public IReadOnlyList<TimeRangeDto>? TrimExcludeRanges { get; set; }
    public IReadOnlyList<HighlightSourceClipDto> SourceClips { get; set; } = Array.Empty<HighlightSourceClipDto>();
}

public class HighlightSourceSegmentDto
{
    public long StartMs { get; set; }
    public long? EndMs { get; set; }
    public long? OutputStartMs { get; set; }
    public long? OutputEndMs { get; set; }
}

public class HighlightSourceClipDto
{
    public Guid MediaId { get; set; }
    /// <summary>Class the source media belongs to (always set when media still exists).</summary>
    public Guid? ClassId { get; set; }
    /// <summary>Optional session the media was captured for.</summary>
    public Guid? ClassSessionId { get; set; }
    /// <summary>Activity linked via the class session, when present.</summary>
    public Guid? ActivityId { get; set; }
    public string? ActivityName { get; set; }
    public IReadOnlyList<HighlightSourceSegmentDto> Segments { get; set; } = Array.Empty<HighlightSourceSegmentDto>();
}

public class HighlightVideoStackDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public string? StrengthDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public int MaxItems { get; set; }
    public bool HasProcessingItem { get; set; }
    public bool CanCreateItem { get; set; }
    public IReadOnlyList<HighlightVideoItemDto> Items { get; set; } = Array.Empty<HighlightVideoItemDto>();
}

public class TimeRangeDto
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
}

public class CreateHighlightStackRequest
{
    public Guid ClassId { get; init; }
    public Guid? StudentId { get; init; }
    public string? StrengthDescription { get; init; }
}

public class TrimHighlightVideoRequest
{
    public string? TrimDescription { get; init; }
    public IReadOnlyList<TimeRangeDto> ExcludeRanges { get; init; } = Array.Empty<TimeRangeDto>();
}

public class AddHighlightSegmentRequest
{
    public Guid MediaId { get; init; }
    public string Start { get; init; } = string.Empty;
    public string End { get; init; } = string.Empty;
    public string? Description { get; init; }
}
