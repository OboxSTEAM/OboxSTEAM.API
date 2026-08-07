using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MediaDTO;

/// <summary>
/// Progress snapshot for a highlight video item (poll while Processing).
/// </summary>
public class HighlightVideoProgressDto
{
    public Guid StackId { get; set; }
    public Guid ItemId { get; set; }
    public HighlightVideoStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;

    /// <summary>
    /// Queued | BuildingClips | Encoding | Completed | Failed | Cancelled
    /// </summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Set when Phase is Encoding and a MediaConvert job ref exists.</summary>
    public int? PercentComplete { get; set; }

    public string? FailureReason { get; set; }
    public string? VideoUrl { get; set; }
    public bool IsTerminal { get; set; }
}
