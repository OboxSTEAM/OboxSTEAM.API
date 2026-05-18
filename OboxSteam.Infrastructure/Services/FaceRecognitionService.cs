using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
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
            QualityFilter = QualityFilter.AUTO,
            ExternalImageId = userId.ToString()
        });

        var faceRecord = response.FaceRecords.FirstOrDefault();
        if (faceRecord is null)
        {
            _logger.LogWarning("No face detected in image for UserId: {UserId}", userId);
            throw ErrorHelper.BadRequest("No face detected in image. Please upload a clear photo of your face.");
        }

        var faceId = faceRecord.Face.FaceId;
        _logger.LogInformation("Face indexed with FaceId: {FaceId} for UserId: {UserId}", faceId, userId);

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

        _logger.LogInformation("IndexFaceAsync completed for UserId: {UserId}, FaceId: {FaceId} (pending save)", userId, faceId);
        return faceId;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Nhận s3Bucket + s3Key thay vì Stream để tránh giới hạn 5MB của Rekognition Image.Bytes.
    /// Rekognition đọc ảnh trực tiếp từ S3, hỗ trợ ảnh tối đa 15MB.
    /// </remarks>
    public async Task<List<FaceMatchResult>> SearchFacesAsync(string s3Bucket, string s3Key, float minConfidence = 90f)
    {
        _logger.LogInformation("SearchFacesAsync started. Bucket={Bucket}, Key={Key}, MinConfidence={MinConfidence}",
            s3Bucket, s3Key, minConfidence);

        var response = await _rekognition.SearchFacesByImageAsync(new SearchFacesByImageRequest
        {
            CollectionId = CollectionId,
            Image = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = s3Bucket,
                    Name = s3Key
                }
            },
            FaceMatchThreshold = minConfidence,
            MaxFaces = 10
        });

        if (response.FaceMatches == null || !response.FaceMatches.Any())
        {
            _logger.LogInformation("SearchFacesAsync found no matches.");
            throw ErrorHelper.BadRequest("No matching face found. Please ensure your face is registered and the photo is clear.");
        }

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

    /// <inheritdoc />
    public async Task<string> StartVideoFaceSearchAsync(string s3Bucket, string s3Key, float minConfidence = 90f)
    {
        _logger.LogInformation("StartVideoFaceSearchAsync: Bucket={Bucket}, Key={Key}", s3Bucket, s3Key);

        await EnsureCollectionExistsAsync();

        var response = await _rekognition.StartFaceSearchAsync(new StartFaceSearchRequest
        {
            CollectionId = CollectionId,
            Video = new Video
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = s3Bucket,
                    Name = s3Key
                }
            },
            FaceMatchThreshold = minConfidence
        });

        _logger.LogInformation("Video face search started. JobId: {JobId}", response.JobId);
        return response.JobId;
    }

    /// <inheritdoc />
    public async Task<VideoFaceSearchResult?> GetVideoFaceSearchResultsAsync(string jobId)
    {
        _logger.LogInformation("GetVideoFaceSearchResultsAsync: JobId={JobId}", jobId);

        var response = await _rekognition.GetFaceSearchAsync(new GetFaceSearchRequest
        {
            JobId = jobId
        });

        if (response.JobStatus == VideoJobStatus.IN_PROGRESS)
        {
            _logger.LogInformation("Video job {JobId} still in progress.", jobId);
            return null;
        }

        if (response.JobStatus == VideoJobStatus.FAILED)
        {
            _logger.LogError("Video job {JobId} failed: {StatusMessage}", jobId, response.StatusMessage);
            return new VideoFaceSearchResult("FAILED", new List<FaceMatchResult>());
        }

        var allMatches = new Dictionary<Guid, FaceMatchResult>();

        foreach (var person in response.Persons)
        {
            if (person.FaceMatches == null) continue;

            foreach (var match in person.FaceMatches)
            {
                if (!Guid.TryParse(match.Face.ExternalImageId, out var userId))
                    continue;

                if (!allMatches.ContainsKey(userId) || match.Similarity > allMatches[userId].Confidence)
                {
                    allMatches[userId] = new FaceMatchResult(userId, match.Face.FaceId, match.Similarity);
                }
            }
        }

        _logger.LogInformation("Video job {JobId} completed with {Count} unique face(s).", jobId, allMatches.Count);
        return new VideoFaceSearchResult("SUCCEEDED", allMatches.Values.ToList());
    }

    // ── Helpers ──────────────────────────────────────

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