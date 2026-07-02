namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Face recognition via AWS Rekognition.
/// Flow: IndexFace (register avatar) → SearchFaces (sync image tagging) → StartVideoFaceSearch
/// (async video tagging) → DeleteFace (account removal).
/// Video jobs complete via SNS webhook (<see cref="IMediaService.HandleFaceSearchWebhookAsync"/>)
/// or manual <c>POST /api/media/{mediaId}/process-tags</c>.
/// </summary>
public interface IFaceRecognitionService
{
    /// <summary>
    /// Registers a user's face in the collection when uploading an avatar.
    /// Does NOT call SaveChangesAsync — caller must commit the UnitOfWork.
    /// </summary>
    Task<string> IndexFaceAsync(Guid userId, Stream imageStream);

    /// <summary>Finds matching users in an uploaded image (S3-backed).</summary>
    Task<List<FaceMatchResult>> SearchFacesAsync(string s3Bucket, string s3Key, float minConfidence = 90f);

    /// <summary>Removes a face when a user account is deleted.</summary>
    Task DeleteFaceAsync(string faceId);

    /// <summary>
    /// Starts async face search on a video already in S3. Returns a Rekognition job ID.
    /// When <c>AWS_SNS_TOPIC_ARN</c> and <c>AWS_REKOGNITION_ROLE_ARN</c> are configured,
    /// completion is delivered via SNS; otherwise callers must poll
    /// <see cref="GetVideoFaceSearchResultsAsync"/>.
    /// </summary>
    Task<string> StartVideoFaceSearchAsync(string s3Bucket, string s3Key, float minConfidence = 90f);

    /// <summary>
    /// Polls face-search results for a video job.
    /// Returns <c>null</c> while the job is IN_PROGRESS.
    /// </summary>
    Task<VideoFaceSearchResult?> GetVideoFaceSearchResultsAsync(string jobId);

    /// <summary>
    /// Extracts appearance timelines for all students in one pass over Rekognition face-search
    /// results. Persist at tagging time — Rekognition retains video job results for only 7 days.
    /// Returns <c>null</c> when the job is IN_PROGRESS or FAILED.
    /// </summary>
    /// <param name="jobId">Rekognition video job ID.</param>
    /// <returns>Map from StudentId to that student's segments and HasOtherFaces flag.</returns>
    Task<Dictionary<Guid, VideoFaceTimelineResult>?> GetAllFaceTimelinesAsync(string jobId);

    /// <summary>
    /// Starts a Rekognition Label Detection job on a video in S3 (async).
    /// Completion is handled by <see cref="IMediaService.HandleLabelDetectionWebhookAsync"/>.
    /// </summary>
    Task<string> StartLabelDetectionAsync(string s3Bucket, string s3Key, float minConfidence = 70f);

    /// <summary>
    /// Reads the label timeline from a completed Label Detection job.
    /// Returns <c>null</c> while IN_PROGRESS; an empty list on FAILED or no labels.
    /// </summary>
    Task<List<LabelDetectionEntry>?> GetLabelDetectionResultsAsync(string jobId);
}

public record FaceMatchResult(Guid UserId, string FaceId, float Confidence);

public record VideoFaceSearchResult(string JobStatus, List<FaceMatchResult> Matches);

/// <summary>Face timeline parsed from Rekognition video face-search results.</summary>
public record VideoFaceTimelineResult(bool HasOtherFaces, List<FaceTimestampSegment> Segments);

/// <summary>
/// A continuous time range (ms) where a student's face appears in a video.
/// </summary>
public record FaceTimestampSegment(long StartMs, long EndMs);

/// <summary>
/// One label detection data point at a specific timestamp in a video.
/// </summary>
public record LabelDetectionEntry(long TimestampMs, string LabelName, float Confidence);
