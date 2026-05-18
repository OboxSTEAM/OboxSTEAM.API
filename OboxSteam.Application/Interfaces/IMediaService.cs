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
    /// Thực hiện: MediaConvert transcode → xóa raw S3 → start Rekognition job.
    /// Cập nhật VideoStatus = PendingTagging sau khi hoàn tất.
    /// </summary>
    Task StartVideoTranscodeAsync(Guid mediaId);

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
}

