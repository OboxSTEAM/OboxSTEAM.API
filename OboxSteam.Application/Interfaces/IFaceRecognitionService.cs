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
    Task<List<FaceMatchResult>> SearchFacesAsync(Stream imageStream, float minConfidence = 90f);

    /// <summary>Xóa face khi user xóa tài khoản.</summary>
    Task DeleteFaceAsync(string faceId);
}

public record FaceMatchResult(Guid UserId, string FaceId, float Confidence);
