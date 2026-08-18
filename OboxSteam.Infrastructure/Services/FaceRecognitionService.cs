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
    private const float DefaultFaceMatchThreshold = 70f;

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

        var imageBytes = await ReadStreamAsync(imageStream);
        var detectResponse = await _rekognition.DetectFacesAsync(new DetectFacesRequest
        {
            Image = new Image { Bytes = new MemoryStream(imageBytes) }
        });

        var detectedFaceCount = detectResponse.FaceDetails?.Count ?? 0;
        if (detectedFaceCount == 0)
        {
            _logger.LogWarning("No face detected in image for UserId: {UserId}", userId);
            throw ErrorHelper.BadRequest("No face detected in image. Please upload a clear photo of your face.");
        }

        if (detectedFaceCount > 1)
        {
            _logger.LogWarning(
                "Multiple faces detected in image for UserId: {UserId}. FaceCount: {FaceCount}",
                userId,
                detectedFaceCount);
            throw ErrorHelper.BadRequest(
                "Multiple faces detected in image. Please upload a photo containing only one face.");
        }

        await EnsureCollectionExistsAsync();

        var response = await _rekognition.IndexFacesAsync(new IndexFacesRequest
        {
            CollectionId = CollectionId,
            Image = new Image { Bytes = new MemoryStream(imageBytes) },
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
    public async Task<List<FaceMatchResult>> SearchFacesAsync(
        string s3Bucket,
        string s3Key,
        float minConfidence = DefaultFaceMatchThreshold)
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
            QualityFilter = QualityFilter.AUTO,
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
    public async Task<string> StartVideoFaceSearchAsync(
        string s3Bucket,
        string s3Key,
        float minConfidence = DefaultFaceMatchThreshold)
    {
        _logger.LogInformation(
            "StartVideoFaceSearchAsync: Bucket={Bucket}, Key={Key}, MinConfidence={MinConfidence}",
            s3Bucket, s3Key, minConfidence);

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
    public async Task<Dictionary<Guid, VideoFaceTimelineResult>?> GetAllFaceTimelinesAsync(string jobId)
    {
        _logger.LogInformation("GetAllFaceTimelinesAsync: JobId={JobId}", jobId);

        // Per-student raw detection timestamps.
        var rawByStudent = new Dictionary<Guid, List<long>>();
        // Students that appeared anywhere in the video.
        var allStudents = new HashSet<Guid>();
        // True if any detected person could NOT be matched to a registered student.
        var hasUnknownPerson = false;
        string? nextToken = null;

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
                _logger.LogError(ex, "GetAllFaceTimelinesAsync paging failed for JobId={JobId}", jobId);
                throw;
            }

            if (response.JobStatus == VideoJobStatus.IN_PROGRESS)
            {
                _logger.LogInformation("GetAllFaceTimelinesAsync: job {JobId} still IN_PROGRESS.", jobId);
                return null;
            }

            if (response.JobStatus == VideoJobStatus.FAILED)
            {
                _logger.LogError("GetAllFaceTimelinesAsync: job {JobId} FAILED.", jobId);
                return null;
            }

            foreach (var person in response.Persons)
            {
                var matchedStudents = person.FaceMatches?
                    .Select(m => Guid.TryParse(m.Face.ExternalImageId, out var uid) ? uid : (Guid?)null)
                    .Where(uid => uid.HasValue)
                    .Select(uid => uid!.Value)
                    .Distinct()
                    .ToList() ?? new List<Guid>();

                if (matchedStudents.Count == 0)
                {
                    hasUnknownPerson = true;
                    continue;
                }

                foreach (var sid in matchedStudents)
                {
                    allStudents.Add(sid);
                    if (!rawByStudent.TryGetValue(sid, out var list))
                    {
                        list = new List<long>();
                        rawByStudent[sid] = list;
                    }
                    list.Add(person.Timestamp);
                }
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        var result = new Dictionary<Guid, VideoFaceTimelineResult>();
        foreach (var sid in allStudents)
        {
            // "Other faces" for this student = any unknown person OR any other tagged student.
            var hasOtherFaces = hasUnknownPerson || allStudents.Any(s => s != sid);
            var segments = CollapseToSegments(rawByStudent[sid]);
            result[sid] = new VideoFaceTimelineResult(hasOtherFaces, segments);
        }

        _logger.LogInformation(
            "GetAllFaceTimelinesAsync: {Students} student(s), hasUnknownPerson={HasUnknown} for JobId={JobId}",
            result.Count, hasUnknownPerson, jobId);

        return result;
    }

    /// <summary>
    /// Collapses sorted/unsorted raw detection timestamps into continuous segments,
    /// merging detections within <see cref="CollapseGapMs"/> of each other.
    /// </summary>
    private static List<FaceTimestampSegment> CollapseToSegments(List<long> rawTimestamps)
    {
        var segments = new List<FaceTimestampSegment>();
        if (rawTimestamps.Count == 0)
            return segments;

        rawTimestamps.Sort();
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

        return segments;
    }

    /// <inheritdoc />
    public async Task<string> StartLabelDetectionAsync(string s3Bucket, string s3Key, float minConfidence = 70f)
    {
        _logger.LogInformation("StartLabelDetectionAsync: Bucket={Bucket}, Key={Key}", s3Bucket, s3Key);

        var snsTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_ARN");
        var roleArn     = Environment.GetEnvironmentVariable("AWS_REKOGNITION_ROLE_ARN");

        var request = new StartLabelDetectionRequest
        {
            Video = new Video
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = s3Bucket,
                    Name   = s3Key
                }
            },
            MinConfidence = minConfidence
        };

        if (!string.IsNullOrEmpty(snsTopicArn) && !string.IsNullOrEmpty(roleArn))
        {
            request.NotificationChannel = new NotificationChannel
            {
                SNSTopicArn = snsTopicArn,
                RoleArn     = roleArn
            };
        }

        var response = await _rekognition.StartLabelDetectionAsync(request);
        _logger.LogInformation("Label Detection job started. JobId: {JobId}", response.JobId);
        return response.JobId;
    }

    /// <inheritdoc />
    public async Task<List<LabelDetectionEntry>?> GetLabelDetectionResultsAsync(string jobId)
    {
        _logger.LogInformation("GetLabelDetectionResultsAsync: JobId={JobId}", jobId);

        var entries   = new List<LabelDetectionEntry>();
        // Dedup (timestamp, labelName) pairs — parent labels can repeat across
        // multiple child labels at the same timestamp, inflating token usage.
        var seen     = new HashSet<(long Ts, string Name)>();
        string? nextToken = null;

        do
        {
            var request = new GetLabelDetectionRequest
            {
                JobId      = jobId,
                MaxResults = 1000,
                SortBy     = LabelDetectionSortBy.TIMESTAMP
            };
            if (nextToken != null) request.NextToken = nextToken;

            GetLabelDetectionResponse response;
            try
            {
                response = await _rekognition.GetLabelDetectionAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLabelDetectionAsync paging failed for JobId={JobId}", jobId);
                throw;
            }

            if (response.JobStatus == VideoJobStatus.IN_PROGRESS)
            {
                _logger.LogInformation("GetLabelDetectionResultsAsync: job {JobId} still IN_PROGRESS.", jobId);
                return null; // caller should retry later
            }

            if (response.JobStatus == VideoJobStatus.FAILED)
            {
                _logger.LogError("GetLabelDetectionResultsAsync: job {JobId} FAILED.", jobId);
                return new List<LabelDetectionEntry>(); // empty — caller falls back
            }

            foreach (var item in response.Labels)
            {
                if (seen.Add((item.Timestamp, item.Label.Name)))
                    entries.Add(new LabelDetectionEntry(
                        item.Timestamp,
                        item.Label.Name,
                        item.Label.Confidence));

                // Flatten parent labels so Claude sees richer context.
                // E.g. "Soccer" has parents ["Sports", "Football"]
                foreach (var parent in item.Label.Parents)
                {
                    if (seen.Add((item.Timestamp, parent.Name)))
                        entries.Add(new LabelDetectionEntry(
                            item.Timestamp,
                            parent.Name,
                            item.Label.Confidence));
                }

                // Flatten aliases — Rekognition often exposes the more descriptive name
                // as an alias rather than the primary label.
                // E.g. "Presentation" → alias "Public Speaking" (critical for strengths matching).
                foreach (var alias in item.Label.Aliases)
                {
                    if (seen.Add((item.Timestamp, alias.Name)))
                        entries.Add(new LabelDetectionEntry(
                            item.Timestamp,
                            alias.Name,
                            item.Label.Confidence));
                }
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        _logger.LogInformation(
            "GetLabelDetectionResultsAsync: {Count} label entry/entries for JobId={JobId}",
            entries.Count, jobId);

        return entries;
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