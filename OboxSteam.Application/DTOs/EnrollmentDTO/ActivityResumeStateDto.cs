using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Polymorphic resume checkpoint for SelfPaced materials.
/// Use <c>kind</c>: video (positionSeconds), pdf (page, scrollRatio), doc (scrollRatio).
/// </summary>
public class ActivityResumeStateDto
{
    [Required]
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = null!;

    [JsonPropertyName("positionSeconds")]
    public int? PositionSeconds { get; set; }

    [JsonPropertyName("durationSeconds")]
    public int? DurationSeconds { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }

    [JsonPropertyName("scrollRatio")]
    public double? ScrollRatio { get; set; }
}

public class SaveActivityCheckpointRequestDto
{
    [Required]
    public ActivityResumeStateDto ResumeState { get; set; } = null!;
}

public class SaveActivityCheckpointResponseDto
{
    public Guid ActivityId { get; set; }

    public string ActivityStatus { get; set; } = null!;

    public ActivityResumeStateDto? ResumeState { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}

public class ActivityLearningProgressDto
{
    public string ActivityStatus { get; set; } = null!;

    public ActivityResumeStateDto? ResumeState { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CompletionSource { get; set; }
}
