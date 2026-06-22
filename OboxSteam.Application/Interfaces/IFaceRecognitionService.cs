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

    /// <summary>
    /// Trích xuất timeline cho TẤT CẢ student xuất hiện trong video chỉ với một lần duyệt
    /// kết quả Rekognition video face-search. Mỗi student có danh sách FaceTimestampSegment
    /// (StartMs, EndMs) và cờ HasOtherFaces.
    /// Dùng tại thời điểm tagging để lưu timeline vào DB khi job còn tươi mới
    /// (Rekognition chỉ giữ kết quả video job 7 ngày).
    /// Trả về <c>null</c> nếu job còn IN_PROGRESS hoặc FAILED.
    /// </summary>
    /// <param name="jobId">Rekognition video job ID.</param>
    /// <returns>Map từ StudentId → timeline (segments + HasOtherFaces) của student đó.</returns>
    Task<Dictionary<Guid, VideoFaceTimelineResult>?> GetAllFaceTimelinesAsync(string jobId);

    /// <summary>
    /// Start Rekognition Label Detection job trên video đã có trong S3 (async).
    /// Trả về JobId để poll kết quả sau.
    /// </summary>
    Task<string> StartLabelDetectionAsync(string s3Bucket, string s3Key, float minConfidence = 70f);

    /// <summary>
    /// Truy xuất toàn bộ label timeline từ một completed Label Detection job.
    /// Trả về <c>null</c> nếu job còn IN_PROGRESS.
    /// Trả về danh sách rỗng nếu job FAILED hoặc không có label nào.
    /// </summary>
    Task<List<LabelDetectionEntry>?> GetLabelDetectionResultsAsync(string jobId);
}

public record FaceMatchResult(Guid UserId, string FaceId, float Confidence);

public record VideoFaceSearchResult(string JobStatus, List<FaceMatchResult> Matches);

/// <summary>
/// Kết quả trả về sau khi parse timeline từ Rekognition
/// </summary>
public record VideoFaceTimelineResult(bool HasOtherFaces, List<FaceTimestampSegment> Segments);

/// <summary>
/// Một đoạn thời gian (ms) mà khuôn mặt sinh viên xuất hiện liên tục trong video.
/// </summary>
public record FaceTimestampSegment(long StartMs, long EndMs);

/// <summary>
/// Một điểm dữ liệu trong label timeline trả về từ Rekognition Label Detection.
/// Mỗi entry tương ứng với một nhãn được phát hiện tại một thời điểm cụ thể.
/// </summary>
public record LabelDetectionEntry(long TimestampMs, string LabelName, float Confidence);

