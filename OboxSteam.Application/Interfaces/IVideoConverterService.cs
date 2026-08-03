namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Submits and polls AWS MediaConvert transcoding jobs.
/// Activity uploads and personal highlight videos share the same service; completion is
/// delivered via SNS/EventBridge to <c>AwsWebhookController</c>.
/// All transcoding runs in the cloud — no local FFmpeg required.
/// </summary>
public interface IVideoConverterService
{
    /// <summary>
    /// Submits an AWS MediaConvert job to transcode the video at
    /// <paramref name="inputS3Key"/> to H.264/AAC MP4 and write the
    /// output to <paramref name="outputDestinationPrefix"/> in the same S3 bucket.
    /// Returns immediately with the MediaConvert Job ID (non-blocking).
    /// </summary>
    /// <param name="inputS3Key">S3 object key of the raw source video (e.g. "raw/video.mov").</param>
    /// <param name="outputDestinationPrefix">S3 prefix for the output file (e.g. "media/").</param>
    /// <returns>The MediaConvert Job ID.</returns>
    Task<string> SubmitTranscodeJobAsync(string inputS3Key, string outputDestinationPrefix);

    /// <summary>
    /// Submits a MediaConvert job that stitches/clips multiple source videos into one
    /// personalised highlight reel. Each <see cref="ClipInput"/> represents one source
    /// video; if <c>Clips</c> is null or empty the entire video is included.
    /// </summary>
    /// <param name="clips">Ordered list of source videos with optional time-range clips.</param>
    /// <param name="outputS3Key">Full S3 key for the output MP4 (e.g. "personal-videos/student_ts.mp4").</param>
    /// <returns>The MediaConvert Job ID.</returns>
    Task<string> SubmitPersonalVideoJobAsync(List<ClipInput> clips, string outputS3Key);

    /// <summary>
    /// Returns output duration in milliseconds for a completed MediaConvert job, if reported.
    /// </summary>
    Task<long?> GetOutputDurationMsAsync(string jobId);

    /// <summary>
    /// Polls the status of a previously submitted MediaConvert job.
    /// </summary>
    /// <param name="jobId">The MediaConvert Job ID returned by <see cref="SubmitTranscodeJobAsync"/>.</param>
    /// <returns>
    /// <see cref="MediaConvertJobStatus.Complete"/> when the job succeeded,
    /// <see cref="MediaConvertJobStatus.InProgress"/> when still running,
    /// <see cref="MediaConvertJobStatus.Error"/> when the job failed.
    /// </returns>
    Task<MediaConvertJobStatus> GetJobStatusAsync(string jobId);

    /// <summary>
    /// Polls MediaConvert job status together with AWS <c>JobPercentComplete</c> (0–100).
    /// Use while the job is in progress to drive a progress UI; percent is 100 when complete.
    /// </summary>
    Task<MediaConvertJobProgress> GetJobProgressAsync(string jobId);

    /// <summary>
    /// Retrieves the S3 key of the transcoded output file for a completed MediaConvert job.
    /// The key is relative to the bucket root (e.g. "media/activityId_123.mp4").
    /// </summary>
    /// <param name="jobId">The MediaConvert Job ID (must be in COMPLETE status).</param>
    Task<string> GetOutputS3KeyAsync(string jobId);

    /// <summary>
    /// Retrieves the S3 key of the raw input video for a MediaConvert job.
    /// Used to clean up the raw source file after transcoding completes.
    /// The key is relative to the bucket root (e.g. "raw/activityId_123.mov").
    /// </summary>
    /// <param name="jobId">The MediaConvert Job ID.</param>
    Task<string> GetInputS3KeyAsync(string jobId);
}

/// <summary>
/// Represents the current state of an AWS MediaConvert transcoding job.
/// </summary>
public enum MediaConvertJobStatus
{
    InProgress,
    Complete,
    Error
}

/// <summary>
/// MediaConvert job status plus AWS-reported percent complete (0–100).
/// </summary>
public sealed record MediaConvertJobProgress(MediaConvertJobStatus Status, int PercentComplete);

// ── Personal Video DTOs ───────────────────────────────────────────────────────

/// <summary>
/// One source video for the personal highlight reel.
/// When <see cref="Clips"/> is empty the entire video is included.
/// </summary>
public record ClipInput(string S3Key, List<TimeClip> Clips);

/// <summary>
/// A single time-range clip within a source video, expressed as HH:MM:SS:mmm timecodes
/// as required by AWS MediaConvert InputClipping.
/// </summary>
public record TimeClip(string StartTimecode, string EndTimecode);

