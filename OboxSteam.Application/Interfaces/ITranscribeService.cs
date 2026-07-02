namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Speaker diarization via AWS Transcribe.
/// Flow: <see cref="StartSpeakerDiarizationAsync"/> (after transcode) → EventBridge
/// "Transcribe Job State Change" → SNS → <see cref="IMediaService.HandleTranscribeWebhookAsync"/>
/// → <see cref="GetSpeakerSegmentsAsync"/> reads transcript JSON from S3.
/// </summary>
public interface ITranscribeService
{
    /// <summary>
    /// Starts a Transcribe job with speaker diarization on media already in S3.
    /// Uses IdentifyLanguage (vi-VN / en-US) for mixed Vietnamese/English content.
    /// Returns the job name for webhook correlation and result lookup.
    /// </summary>
    Task<string> StartSpeakerDiarizationAsync(string s3Bucket, string s3Key, Guid mediaId);

    /// <summary>
    /// Reads speaker diarization results for a completed job.
    /// Returns <c>null</c> while QUEUED/IN_PROGRESS; an empty list on FAILED or no speakers.
    /// </summary>
    Task<List<SpeakerSegment>?> GetSpeakerSegmentsAsync(string jobName);
}

/// <summary>
/// A time range (ms) where an anonymous speaker (e.g. "spk_0") is talking.
/// </summary>
public record SpeakerSegment(string SpeakerLabel, long StartMs, long EndMs);
