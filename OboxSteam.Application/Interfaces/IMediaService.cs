using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

public interface IMediaService
{
    /// <summary>
    /// Upload ảnh/video lên S3, lưu MediaAsset record.
    /// Nếu là ảnh → auto face-tag ngay (SearchFaces).
    /// Nếu là video → upload raw lên S3, enqueue mediaId để worker xử lý.
    /// Trả về ngay lập tức; VideoStatus = Transcoding cho đến khi worker hoàn thành.
    /// </summary>
    Task<MediaAssetDto> UploadMediaAsync(IFormFile file, Guid activityId);

    /// <summary>
    /// Lấy tất cả media của một activity (kèm tags).
    /// </summary>
    Task<List<MediaAssetDto>> GetMediaByActivityAsync(Guid activityId);

    /// <summary>
    /// Được gọi bởi VideoTagProcessingWorker.
    /// Submit MediaConvert job (non-blocking): đọc raw S3 key từ DB, gửi job lên MediaConvert,
    /// lưu MC job ID vào DB, giữ VideoStatus = Transcoding.
    /// </summary>
    Task StartVideoTranscodeAsync(Guid mediaId);

    /// <summary>
    /// Poll trạng thái MediaConvert job.
    /// - Trả về <c>true</c> khi job COMPLETE (đã lấy URL, start Rekognition, VideoStatus = PendingTagging).
    /// - Trả về <c>false</c> khi job vẫn đang chạy (IN_PROGRESS/SUBMITTED).
    /// - Ném exception khi job ERROR.
    /// </summary>
    Task<bool> TryCompleteTranscodeAsync(Guid mediaId);

    /// <summary>
    /// Xử lý kết quả face-search cho video (poll Rekognition job).
    /// Gọi khi video processing hoàn tất.
    /// </summary>
    Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId);

    /// <summary>
    /// Thử xử lý video tags, trả về true nếu thành công, false nếu đang in progress.
    /// Được gọi bởi VideoTagProcessingWorker trong vòng retry.
    /// </summary>
    Task<bool> TryProcessVideoTagsAsync(Guid mediaId);

    /// <summary>
    /// Soft-delete media + xóa file trên S3.
    /// </summary>
    Task DeleteMediaAsync(Guid mediaId);

    /// <summary>
    /// Trả về <c>true</c> nếu media đang ở trạng thái <see cref="Domain.Enums.VideoProcessingStatus.PendingTagging"/>
    /// (Rekognition job đã được submit, đang chờ kết quả).
    /// Dùng bởi <c>VideoTagProcessingWorker</c> để phát hiện recovery case khi server restart.
    /// </summary>
    Task<bool> IsAwaitingTaggingAsync(Guid mediaId);

    /// <summary>
    /// Handle MediaConvert job completion webhook (for raw videos).
    /// </summary>
    Task HandleMediaConvertWebhookAsync(string jobId, bool isSuccess);

    /// <summary>
    /// Handle Rekognition Face Search job completion webhook.
    /// </summary>
    Task HandleFaceSearchWebhookAsync(string jobId, bool isSuccess);

    /// <summary>
    /// Handle Rekognition Label Detection job completion webhook.
    /// </summary>
    Task HandleLabelDetectionWebhookAsync(string jobId, bool isSuccess);

    /// <summary>
    /// Handle AWS Transcribe speaker-diarization job completion webhook.
    /// Persists the speaker timeline and, once both face and speaker data are available,
    /// maps anonymous speakers to students (overlap analysis) so the personal-video pipeline
    /// can include "voice but no face" moments.
    /// </summary>
    Task HandleTranscribeWebhookAsync(string jobName, bool isSuccess);
}

