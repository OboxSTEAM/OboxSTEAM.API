using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Infrastructure.Services;

public class FaceRecognitionService : IFaceRecognitionService
{
    private const string CollectionId = "oboxsteam-faces";
    private readonly IAmazonRekognition _rekognition;
    private readonly IUnitOfWork _unitOfWork;

    public FaceRecognitionService(IAmazonRekognition rekognition, IUnitOfWork unitOfWork)
    {
        _rekognition = rekognition;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<string> IndexFaceAsync(Guid userId, Stream imageStream)
    {
        // Đảm bảo collection tồn tại (idempotent)
        await EnsureCollectionExistsAsync();

        var response = await _rekognition.IndexFacesAsync(new IndexFacesRequest
        {
            CollectionId = CollectionId,
            Image = new Image { Bytes = new MemoryStream(ReadStream(imageStream)) },
            MaxFaces = 1,
            ExternalImageId = userId.ToString() // gắn UserId vào để tra cứu ngược
        });

        var faceRecord = response.FaceRecords.FirstOrDefault()
            ?? throw new InvalidOperationException("No face detected in image.");

        var faceId = faceRecord.Face.FaceId;

        // Lưu mapping vào DB thông qua FaceEmbedding entity đã có
        var existing = await _unitOfWork.FaceEmbeddings
            .FirstOrDefaultAsync(f => f.StudentId == userId);

        if (existing != null)
        {
            // Xóa face cũ trên Rekognition trước khi cập nhật
            await DeleteFaceAsync(existing.AwsFaceId);
            existing.AwsFaceId = faceId;
        }
        else
        {
            await _unitOfWork.FaceEmbeddings.AddAsync(new FaceEmbedding
            {
                StudentId = userId,
                AwsFaceId = faceId,
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return faceId;
    }

    /// <inheritdoc />
    public async Task<List<FaceMatchResult>> SearchFacesAsync(Stream imageStream, float minConfidence = 90f)
    {
        var response = await _rekognition.SearchFacesByImageAsync(new SearchFacesByImageRequest
        {
            CollectionId = CollectionId,
            Image = new Image { Bytes = new MemoryStream(ReadStream(imageStream)) },
            FaceMatchThreshold = minConfidence,
            MaxFaces = 10
        });

        return response.FaceMatches
            .Where(m => Guid.TryParse(m.Face.ExternalImageId, out _))
            .Select(m => new FaceMatchResult(
                Guid.Parse(m.Face.ExternalImageId),
                m.Face.FaceId,
                m.Similarity))
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeleteFaceAsync(string faceId)
    {
        await _rekognition.DeleteFacesAsync(new DeleteFacesRequest
        {
            CollectionId = CollectionId,
            FaceIds = new List<string> { faceId }
        });
    }

    // ── Helpers ──────────────────────────────────────

    /// <summary>
    /// Tạo collection nếu chưa tồn tại. Nếu đã có → bắt ResourceAlreadyExistsException và bỏ qua.
    /// </summary>
    private async Task EnsureCollectionExistsAsync()
    {
        try
        {
            await _rekognition.CreateCollectionAsync(new CreateCollectionRequest
            {
                CollectionId = CollectionId
            });
        }
        catch (ResourceAlreadyExistsException)
        {
            // Collection đã tồn tại → OK
        }
    }

    private static byte[] ReadStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
