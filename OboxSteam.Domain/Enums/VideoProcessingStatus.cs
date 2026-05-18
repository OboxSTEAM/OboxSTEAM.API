namespace OboxSteam.Domain.Enums;

/// <summary>
/// Tracks the processing lifecycle of a video MediaAsset.
/// </summary>
public enum VideoProcessingStatus
{
    /// <summary>Not a video, or status not applicable.</summary>
    None,

    /// <summary>Raw file uploaded; AWS MediaConvert transcode job is running.</summary>
    Transcoding,

    /// <summary>Transcoding complete; AWS Rekognition face-search job is running.</summary>
    PendingTagging,

    /// <summary>Face-search complete and all tags have been persisted.</summary>
    TaggingComplete,

    /// <summary>Processing failed at some stage (transcoding or tagging).</summary>
    Failed
}
