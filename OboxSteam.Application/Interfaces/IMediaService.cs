using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

public interface IMediaService
{
    /// <summary>
    /// Uploads an image or video to S3 and creates a <c>MediaAsset</c> record.
    /// Images are face-tagged synchronously via Rekognition SearchFaces.
    /// Videos upload raw to S3, then <see cref="StartVideoTranscodeAsync"/> is invoked in the
    /// same request (MediaConvert submit only — AWS runs transcode asynchronously).
    /// Returns immediately with <c>VideoStatus = Transcoding</c> until AWS webhooks (or manual
    /// <c>POST /api/media/{mediaId}/process-tags</c>) advance processing.
    /// </summary>
    Task<MediaAssetDto> UploadMediaAsync(IFormFile file, Guid activityId);

    /// <summary>
    /// Returns all media for an activity, including face tags.
    /// </summary>
    Task<List<MediaAssetDto>> GetMediaByActivityAsync(Guid activityId);

    /// <summary>
    /// Called from <see cref="UploadMediaAsync"/> after the raw file is persisted.
    /// Submits a MediaConvert job (non-blocking): reads <c>RawVideoS3Key</c>, sends the job,
    /// stores <c>MediaConvertJobId</c>, keeps <c>VideoStatus = Transcoding</c>.
    /// Completion is handled by <see cref="HandleMediaConvertWebhookAsync"/> (SNS/EventBridge).
    /// </summary>
    Task StartVideoTranscodeAsync(Guid mediaId);

    /// <summary>
    /// Polls MediaConvert job status and, when complete, starts Rekognition / Transcribe jobs.
    /// Primary caller: <see cref="HandleMediaConvertWebhookAsync"/>.
    /// Returns <c>true</c> when the job completed (FileUrl set, VideoStatus = PendingTagging).
    /// Returns <c>false</c> while the job is still in progress.
    /// Throws when the job failed or output resolution fails.
    /// </summary>
    Task<bool> TryCompleteTranscodeAsync(Guid mediaId);

    /// <summary>
    /// Restarts Rekognition face search (and label detection) on the transcoded output,
    /// then polls once for immediate completion. Call again while
    /// <see cref="Domain.Enums.VideoProcessingStatus.PendingTagging"/> until tags are ready.
    /// </summary>
    Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId);

    /// <summary>
    /// Same core logic as <see cref="ProcessVideoTagsAsync"/> but non-throwing for in-progress jobs.
    /// Primary caller: <see cref="HandleFaceSearchWebhookAsync"/> (Rekognition SNS notification).
    /// Returns <c>true</c> when tags were saved; <c>false</c> when Rekognition is still running.
    /// </summary>
    Task<bool> TryProcessVideoTagsAsync(Guid mediaId);

    /// <summary>
    /// Soft-deletes media and removes the file from S3.
    /// </summary>
    Task DeleteMediaAsync(Guid mediaId);

    /// <summary>
    /// Returns <c>true</c> when Rekognition face-search results should still be polled and
    /// persisted (typically <see cref="Domain.Enums.VideoProcessingStatus.PendingTagging"/> or
    /// late-recovery cases). Useful for health checks or a future background poller; not used by
    /// any hosted service today — recovery is via AWS webhooks and
    /// <see cref="ProcessVideoTagsAsync"/>.
    /// </summary>
    Task<bool> IsAwaitingTaggingAsync(Guid mediaId);

    /// <summary>
    /// Handles MediaConvert job completion from SNS/EventBridge (activity media uploads).
    /// Returns <c>true</c> when a matching <c>MediaAsset</c> was found for <paramref name="jobId"/>.
    /// </summary>
    Task<bool> HandleMediaConvertWebhookAsync(string jobId, bool isSuccess);

    /// <summary>
    /// Handles Rekognition Face Search job completion from SNS.
    /// </summary>
    Task HandleFaceSearchWebhookAsync(string jobId, bool isSuccess);

    /// <summary>
    /// Handles Rekognition Label Detection job completion from SNS.
    /// </summary>
    Task HandleLabelDetectionWebhookAsync(string jobId, bool isSuccess);

    /// <summary>
    /// Legacy AWS Transcribe webhook handler. Voice diarization is disabled; this only unblocks
    /// videos stuck in <see cref="VideoProcessingStatus.PendingSpeakerMapping"/> from before the pipeline was removed.
    /// </summary>
    Task HandleTranscribeWebhookAsync(string jobName, bool isSuccess);
}
