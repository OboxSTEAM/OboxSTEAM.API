using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

public interface IMediaService
{
    /// <summary>
    /// Upload ảnh/video lên S3, lưu MediaAsset record.
    /// Nếu là ảnh → auto face-tag ngay (SearchFaces).
    /// Nếu là video → start Rekognition Video job (async), lưu JobId.
    /// </summary>
    Task<MediaAssetDto> UploadMediaAsync(IFormFile file, Guid activityId);

    /// <summary>
    /// Lấy tất cả media của một activity (kèm tags).
    /// </summary>
    Task<List<MediaAssetDto>> GetMediaByActivityAsync(Guid activityId);

    /// <summary>
    /// Xử lý kết quả face-search cho video (poll Rekognition job).
    /// Gọi khi video processing hoàn tất.
    /// </summary>
    Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId);

    /// <summary>
    /// Soft-delete media + xóa file trên S3.
    /// </summary>
    Task DeleteMediaAsync(Guid mediaId);
}
