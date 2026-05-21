namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Submits and polls AWS MediaConvert transcoding jobs.
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
