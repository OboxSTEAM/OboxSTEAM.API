using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MediaDTO;

/// <summary>
/// Data transfer object for a HighlightVideo (personal video generation) record.
/// </summary>
public class HighlightVideoDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ProgramId { get; set; }

    /// <summary>Public URL of the finished personal video. Null while processing.</summary>
    public string? VideoUrl { get; set; }

    /// <summary>Current status of the generation job.</summary>
    public HighlightVideoStatus PersonalVideoStatus { get; set; }

    /// <summary>UTC timestamp when the generation was last triggered.</summary>
    public DateTime? PersonalVideoRequestedAt { get; set; }

    /// <summary>
    /// Reason the last generation failed (only set when <see cref="PersonalVideoStatus"/>
    /// is Failed). E.g. no segments matched the requested strengths.
    /// </summary>
    public string? PersonalVideoFailureReason { get; set; }
}
