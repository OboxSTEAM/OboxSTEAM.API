namespace OboxSteam.Domain.Enums;

/// <summary>
/// Tracks the lifecycle of a personal highlight video generation job.
/// </summary>
public enum HighlightVideoStatus
{
    /// <summary>No job has been triggered yet.</summary>
    None,

    /// <summary>MediaConvert stitching job is in progress.</summary>
    Processing,

    /// <summary>Job completed successfully; VideoUrl is available.</summary>
    Completed,

    /// <summary>Job failed at some stage.</summary>
    Failed
}
