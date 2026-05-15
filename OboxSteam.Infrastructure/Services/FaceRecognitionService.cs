using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Infrastructure.Services;

public class FaceRecognitionService : IFaceRecognitionService
{
    private const string CollectionId = "oboxsteam-faces";
    private static bool _collectionEnsured;

    private readonly IAmazonRekognition _rekognition;
    private readonly ILogger<FaceRecognitionService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public FaceRecognitionService(IAmazonRekognition rekognition, IUnitOfWork unitOfWork, ILogger<FaceRecognitionService> logger)
    {
        _rekognition = rekognition;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> IndexFaceAsync(Guid userId, Stream imageStream)
    {
        _logger.LogInformation("IndexFaceAsync started for UserId: {UserId}", userId);

        await EnsureCollectionExistsAsync();

        var response = await _rekognition.IndexFacesAsync(new IndexFacesRequest
        {
            CollectionId = CollectionId,
            Image = new Image { Bytes = new MemoryStream(await ReadStreamAsync(imageStream)) },
            MaxFaces = 1,
            QualityFilter = QualityFilter.AUTO, // Tự lọc ảnh mờ/tối/chất lượng thấp
            ExternalImageId = userId.ToString()
        });

        var faceRecord = response.FaceRecords.FirstOrDefault();
        if (faceRecord is null)
        {
            _logger.LogWarning("No face detected in image for UserId: {UserId}", userId);
            throw new InvalidOperationException("No face detected in image.");
        }

        var faceId = faceRecord.Face.FaceId;
        _logger.LogInformation("Face indexed with FaceId: {FaceId} for UserId: {UserId}", faceId, userId);

        // Lưu mapping vào DB thông qua FaceEmbedding entity
        var existing = await _unitOfWork.FaceEmbeddings
            .FirstOrDefaultAsync(f => f.StudentId == userId);

        if (existing != null)
        {
            _logger.LogInformation("Replacing existing face {OldFaceId} with {NewFaceId} for UserId: {UserId}", existing.AwsFaceId, faceId, userId);
            await DeleteFaceAsync(existing.AwsFaceId);
            existing.AwsFaceId = faceId;
        }
        else
        {
            _logger.LogInformation("Creating new FaceEmbedding for UserId: {UserId}", userId);
            await _unitOfWork.FaceEmbeddings.AddAsync(new FaceEmbedding
            {
                StudentId = userId,
                AwsFaceId = faceId,
            });
        }

        // NOTE: SaveChangesAsync is NOT called here — the caller is responsible
        // for committing, so that all related changes (avatar URL + face embedding)
        // are saved atomically in a single transaction.
        _logger.LogInformation("IndexFaceAsync completed for UserId: {UserId}, FaceId: {FaceId} (pending save)", userId, faceId);
        return faceId;
    }

    /// <inheritdoc />
    public async Task<List<FaceMatchResult>> SearchFacesAsync(Stream imageStream, float minConfidence = 90f)
    {
        _logger.LogInformation("SearchFacesAsync started with MinConfidence: {MinConfidence}", minConfidence);

        var response = await _rekognition.SearchFacesByImageAsync(new SearchFacesByImageRequest
        {
            CollectionId = CollectionId,
            Image = new Image { Bytes = new MemoryStream(await ReadStreamAsync(imageStream)) },
            FaceMatchThreshold = minConfidence,
            MaxFaces = 10
        });

        var results = response.FaceMatches
            .Where(m => Guid.TryParse(m.Face.ExternalImageId, out _))
            .Select(m => new FaceMatchResult(
                Guid.Parse(m.Face.ExternalImageId),
                m.Face.FaceId,
                m.Similarity))
            .ToList();

        _logger.LogInformation("SearchFacesAsync completed with {MatchCount} match(es)", results.Count);
        return results;
    }

    /// <inheritdoc />
    public async Task DeleteFaceAsync(string faceId)
    {
        _logger.LogInformation("DeleteFaceAsync started for FaceId: {FaceId}", faceId);

        await _rekognition.DeleteFacesAsync(new DeleteFacesRequest
        {
            CollectionId = CollectionId,
            FaceIds = new List<string> { faceId }
        });

        _logger.LogInformation("DeleteFaceAsync completed for FaceId: {FaceId}", faceId);
    }

    // ── Helpers ──────────────────────────────────────

    /// <summary>
    /// Tạo collection nếu chưa tồn tại. Dùng static flag để chỉ gọi AWS API 1 lần
    /// trong lifetime của application.
    /// </summary>
    private async Task EnsureCollectionExistsAsync()
    {
        if (_collectionEnsured) return;

        try
        {
            await _rekognition.CreateCollectionAsync(new CreateCollectionRequest
            {
                CollectionId = CollectionId
            });
            _logger.LogInformation("Rekognition collection '{CollectionId}' created.", CollectionId);
        }
        catch (ResourceAlreadyExistsException)
        {
            _logger.LogDebug("Rekognition collection '{CollectionId}' already exists.", CollectionId);
        }

        _collectionEnsured = true;
    }

    private static async Task<byte[]> ReadStreamAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}

