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

    /// <summary>
    /// When collapsing raw Rekognition timestamps into segments, detections within
    /// this window are treated as part of the same continuous appearance.
    /// Rekognition samples roughly every 1 s; 500 ms avoids splitting a single
    /// appearance across two segments due to minor sampling jitter.
    /// </summary>
    private const long CollapseGapMs = 500;

    // Double-check lock to safely initialize the Rekognition collection once,
    // even under concurrent requests.
    private static volatile bool _collectionEnsured;
    private static readonly SemaphoreSlim _collectionInitLock = new(1, 1);

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
            // Return empty list — callers decide what to do with "no match".
            // Throwing here would force every caller into try/catch for a normal
            // (non-exceptional) business outcome.
            _logger.LogInformation("SearchFacesAsync found no matches.");
            return new List<FaceMatchResult>();
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

        var snsTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_ARN");
        var roleArn = Environment.GetEnvironmentVariable("AWS_REKOGNITION_ROLE_ARN");

        var request = new StartFaceSearchRequest
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
        };

        if (!string.IsNullOrEmpty(snsTopicArn) && !string.IsNullOrEmpty(roleArn))
        {
            request.NotificationChannel = new NotificationChannel
            {
                SNSTopicArn = snsTopicArn,
                RoleArn = roleArn
            };
        }

        var response = await _rekognition.StartFaceSearchAsync(request);

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

    /// <inheritdoc />
    public async Task<VideoFaceTimelineResult?> GetVideoFaceTimelineAsync(string jobId, Guid studentId)
    {
        _logger.LogInformation(
            "GetVideoFaceTimelineAsync: JobId={JobId}, StudentId={StudentId}", jobId, studentId);

        // Collect all raw detection timestamps for the target student across pages.
        var rawTimestamps = new List<long>();
        string? nextToken = null;
        bool hasOtherFaces = false;

        do
        {
            var request = new GetFaceSearchRequest
            {
                JobId      = jobId,
                MaxResults = 1000,
                SortBy     = FaceSearchSortBy.TIMESTAMP,
            };
            if (nextToken != null) request.NextToken = nextToken;

            GetFaceSearchResponse response;
            try
            {
                response = await _rekognition.GetFaceSearchAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFaceSearchAsync paging failed for JobId={JobId}", jobId);
                throw;
            }

            // Job not ready yet — caller should retry
            if (response.JobStatus == VideoJobStatus.IN_PROGRESS)
            {
                _logger.LogInformation("GetVideoFaceTimelineAsync: job {JobId} still IN_PROGRESS.", jobId);
                return null;
            }

            if (response.JobStatus == VideoJobStatus.FAILED)
            {
                _logger.LogError("GetVideoFaceTimelineAsync: job {JobId} FAILED.", jobId);
                return null;
            }

            foreach (var person in response.Persons)
            {
                bool isStudent = false;
                if (person.FaceMatches != null && person.FaceMatches.Count > 0)
                {
                    isStudent = person.FaceMatches.Any(m =>
                        Guid.TryParse(m.Face.ExternalImageId, out var uid) && uid == studentId);
                }

                if (isStudent)
                {
                    rawTimestamps.Add(person.Timestamp);
                }
                else
                {
                    hasOtherFaces = true;
                }
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        if (rawTimestamps.Count == 0)
        {
            _logger.LogInformation(
                "GetVideoFaceTimelineAsync: No detections for StudentId={StudentId} in JobId={JobId}",
                studentId, jobId);
            return new VideoFaceTimelineResult(hasOtherFaces, new List<FaceTimestampSegment>());
        }

        // Collapse contiguous timestamps into segments.
        // Rekognition samples every ~1 second; treat detections within CollapseGapMs
        // as the same continuous appearance (see class-level constant).
        rawTimestamps.Sort();
        var segments = new List<FaceTimestampSegment>();
        var segStart = rawTimestamps[0];
        var segEnd   = rawTimestamps[0];

        for (int i = 1; i < rawTimestamps.Count; i++)
        {
            if (rawTimestamps[i] - segEnd <= CollapseGapMs)
            {
                segEnd = rawTimestamps[i];
            }
            else
            {
                segments.Add(new FaceTimestampSegment(segStart, segEnd));
                segStart = rawTimestamps[i];
                segEnd   = rawTimestamps[i];
            }
        }
        segments.Add(new FaceTimestampSegment(segStart, segEnd));

        _logger.LogInformation(
            "GetVideoFaceTimelineAsync: {Raw} raw ts → {Segs} segment(s) for StudentId={StudentId}",
            rawTimestamps.Count, segments.Count, studentId);

        return new VideoFaceTimelineResult(hasOtherFaces, segments);
    }

    // ── Helpers ──────────────────────────────────────────

    private async Task EnsureCollectionExistsAsync()
    {
        // Fast path — no lock needed once initialized.
        if (_collectionEnsured) return;

        await _collectionInitLock.WaitAsync();
        try
        {
            // Double-check inside the lock in case another thread just finished.
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
        finally
        {
            _collectionInitLock.Release();
        }
    }

    private static async Task<byte[]> ReadStreamAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}