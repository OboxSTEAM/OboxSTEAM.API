namespace OboxSteam.Domain.Enums;

/// <summary>
/// Tracks the processing lifecycle of a video MediaAsset.
/// </summary>
public enum VideoProcessingStatus
{
    /// <summary>Not a video, or status not applicable.</summary>
    None = 0,

    /// <summary>Raw file uploaded; AWS MediaConvert transcode job is running.</summary>
    Transcoding = 1,

    /// <summary>Transcoding complete; AWS Rekognition face-search job is running.</summary>
    PendingTagging = 2,

    /// <summary>
    /// All processing finished: face tags persisted. Safe to build personal highlight videos.
    /// </summary>
    TaggingComplete = 4,

    /// <summary>Processing failed at some stage (transcoding or tagging).</summary>
    Failed = 5
}
