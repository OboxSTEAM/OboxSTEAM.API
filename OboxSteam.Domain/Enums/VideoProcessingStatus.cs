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

    /// <summary>
    /// Face-search complete and tags persisted; waiting for AWS Transcribe diarization and
    /// speaker-to-student mapping to finish. <see cref="TaggingComplete"/> is set only after
    /// that pipeline resolves (or was skipped because Transcribe was never submitted).
    /// </summary>
    PendingSpeakerMapping,

    /// <summary>
    /// All processing finished: face tags persisted and the voice/speaker pipeline has resolved
    /// (mapped or skipped). Safe to build personal highlight videos.
    /// </summary>
    TaggingComplete,

    /// <summary>Processing failed at some stage (transcoding or tagging).</summary>
    Failed
}
