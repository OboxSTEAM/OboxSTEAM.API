namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Service nhận diện giọng nói (speaker diarization) qua AWS Transcribe.
/// Luồng: StartSpeakerDiarization (sau khi transcode xong) → EventBridge/SNS webhook báo
/// hoàn thành → GetSpeakerSegments (đọc transcript JSON từ S3) → map speaker với student.
/// </summary>
public interface ITranscribeService
{
    /// <summary>
    /// Start một Transcribe job có bật speaker diarization trên video/audio đã có trong S3.
    /// Dùng IdentifyLanguage (vi-VN / en-US) vì nội dung có thể lẫn Việt + Anh.
    /// Trả về job name để tra cứu kết quả sau qua webhook.
    /// </summary>
    Task<string> StartSpeakerDiarizationAsync(string s3Bucket, string s3Key, Guid mediaId);

    /// <summary>
    /// Đọc kết quả speaker diarization của một job đã hoàn thành.
    /// Trả về <c>null</c> nếu job còn QUEUED/IN_PROGRESS.
    /// Trả về danh sách rỗng nếu job FAILED hoặc không có speaker label nào.
    /// </summary>
    Task<List<SpeakerSegment>?> GetSpeakerSegmentsAsync(string jobName);
}

/// <summary>
/// Một đoạn thời gian (ms) mà một speaker (ẩn danh: "spk_0", "spk_1", ...) đang nói.
/// </summary>
public record SpeakerSegment(string SpeakerLabel, long StartMs, long EndMs);
