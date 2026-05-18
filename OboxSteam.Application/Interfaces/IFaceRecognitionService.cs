namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Service nhận diện khuôn mặt qua AWS Rekognition.
/// Luồng: IndexFace (đăng ký avatar) → SearchFaces (auto-tag) → DeleteFace (xóa tài khoản).
/// </summary>
public interface IFaceRecognitionService
{
    /// <summary>
    /// Đăng ký khuôn mặt user vào collection khi upload avatar.
    /// Does NOT call SaveChangesAsync — caller must commit the UnitOfWork.
    /// </summary>
    Task<string> IndexFaceAsync(Guid userId, Stream imageStream);

    /// <summary>Tìm users match trong một ảnh upload lên.</summary>
    Task<List<FaceMatchResult>> SearchFacesAsync(string s3Bucket, string s3Key, float minConfidence = 90f);

    /// <summary>Xóa face khi user xóa tài khoản.</summary>
    Task DeleteFaceAsync(string faceId);

    /// <summary>
    /// Start async face search trên video đã upload lên S3.
    /// Trả về JobId để poll kết quả sau.
    /// </summary>
    Task<string> StartVideoFaceSearchAsync(string s3Bucket, string s3Key, float minConfidence = 90f);

    /// <summary>
    /// Poll kết quả face search cho video job.
    /// Trả về null nếu job chưa hoàn tất (IN_PROGRESS).
    /// </summary>
    Task<VideoFaceSearchResult?> GetVideoFaceSearchResultsAsync(string jobId);
}

public record FaceMatchResult(Guid UserId, string FaceId, float Confidence);

public record VideoFaceSearchResult(string JobStatus, List<FaceMatchResult> Matches);
