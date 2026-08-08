using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class MediaService : IMediaService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov" };

    private const long MaxImageSize = 10 * 1024 * 1024;  // 10 MB
    private const long MaxVideoSize = 3L * 1024 * 1024 * 1024;  // 3 GB
    private const string MediaFolder = "media";
    private const string RawFolder = "raw";



    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<MediaService> _logger;
    private readonly IVideoConverterService _videoConverterService;

    public MediaService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        IFaceRecognitionService faceRecognitionService,
        INotificationPublisher notificationPublisher,
        ILogger<MediaService> logger,
        IVideoConverterService videoConverterService)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _faceRecognitionService = faceRecognitionService;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
        _videoConverterService = videoConverterService;
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> UploadMediaAsync(IFormFile file, Guid classId, Guid? classSessionId = null)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "UploadMediaAsync started by UserId: {UserId} for Class: {ClassId}, ClassSession: {ClassSessionId}",
            userId, classId, classSessionId);

        // ── Validate ─────────────────────────────────────────────────────────
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = ImageExtensions.Contains(extension);
        var isVideo = VideoExtensions.Contains(extension);

        if (!isImage && !isVideo)
            throw ErrorHelper.BadRequest("Only image (.jpg, .jpeg, .png) and video (.mp4, .mov) files are allowed.");

        if (isImage && file.Length > MaxImageSize)
            throw ErrorHelper.BadRequest("Image file size must not exceed 10 MB.");

        if (isVideo && file.Length > MaxVideoSize)
            throw ErrorHelper.BadRequest("Video file size must not exceed 3 GB.");

        var resolvedSessionId = await ValidateClassAndOptionalSessionAsync(classId, classSessionId);

        var fileName = $"{classId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";

        // ── Handle upload ─────────────────────────────────────────────────────
        string? fileUrl = null;
        string? videoLocalPath = null;
        var tags = new List<MediaTag>();
        List<FaceMatchResult> prevalidatedMatches = new();

        if (isImage)
        {
            var path = $"{MediaFolder}/{fileName}";
            await using var uploadStream = file.OpenReadStream();
            await _blobService.UploadFileAsync(fileName, uploadStream, MediaFolder);
            fileUrl = await _blobService.GetPreviewUrlAsync(path);

            prevalidatedMatches = await _faceRecognitionService.SearchFacesAsync(_blobService.BucketName, path);

            if (prevalidatedMatches.Count == 0)
            {
                _logger.LogWarning("No recognizable face found in uploaded image. Removing S3 object: {Path}", path);
                await _blobService.DeleteByKeyAsync(path);
                throw ErrorHelper.BadRequest(
                    "No recognizable face found in the uploaded image. " +
                    "Please upload a clear photo where a registered student's face is visible.");
            }
        }
        else
        {
            var rawS3Key = $"{RawFolder}/{fileName}";
            _logger.LogInformation("Uploading raw video to S3: {RawKey}", rawS3Key);
            await using var rawStream = file.OpenReadStream();
            await _blobService.UploadFileAsync(fileName, rawStream, RawFolder);
            videoLocalPath = rawS3Key;
        }

        var media = new MediaAsset
        {
            UploaderId = userId,
            ClassId = classId,
            ClassSessionId = resolvedSessionId,
            FileUrl = fileUrl,
            FileType = isImage ? "image" : "video",
            VideoStatus = isVideo ? VideoProcessingStatus.Transcoding : VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.MediaAssets.AddAsync(media);
        await _unitOfWork.SaveChangesAsync();

        if (isImage)
        {
            tags = await SaveFaceTagsAsync(media.Id, classId, prevalidatedMatches);
        }
        else
        {
            media.RawVideoS3Key = videoLocalPath;
            await _unitOfWork.SaveChangesAsync();

            await StartVideoTranscodeAsync(media.Id);
            _logger.LogInformation(
                "Video submitted to MediaConvert. MediaId: {MediaId}, RawKey: {Key}",
                media.Id, videoLocalPath);
        }

        _logger.LogInformation(
            "UploadMediaAsync completed. MediaId: {MediaId}, ClassId: {ClassId}, ClassSessionId: {ClassSessionId}",
            media.Id, media.ClassId, media.ClassSessionId);
        return await MapToDto(media, tags);
    }

    /// <inheritdoc />
    public async Task<List<MediaAssetDto>> GetMediaByClassSessionAsync(Guid classSessionId)
    {
        _logger.LogInformation("GetMediaByClassSessionAsync: ClassSessionId={ClassSessionId}", classSessionId);

        var session = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        if (session == null || session.IsDeleted)
            throw ErrorHelper.NotFound("Class session not found.");

        var currentUser = await GetCurrentUserOrThrowAsync();
        await EnsureCanAccessClassMediaAsync(currentUser, session.ClassId);

        var mediaList = await _unitOfWork.MediaAssets
            .GetAllAsync(m => m.ClassSessionId == classSessionId && !m.IsDeleted, m => m.MediaTags);

        if (currentUser.Role == RoleType.Student)
        {
            mediaList = mediaList
                .Where(IsReadyMedia)
                .Where(m => m.MediaTags.Any(t => !t.IsDeleted && t.StudentId == currentUser.Id))
                .ToList();
        }
        else if (currentUser.Role == RoleType.Parent)
        {
            var linkedStudentIds = await GetLinkedStudentIdsAsync(currentUser.Id);
            mediaList = mediaList
                .Where(IsReadyMedia)
                .Where(m => m.MediaTags.Any(t => !t.IsDeleted && linkedStudentIds.Contains(t.StudentId)))
                .ToList();
        }

        return await MapMediaListToDtos(mediaList);
    }

    /// <inheritdoc />
    public async Task<Pagination<MediaAssetDto>> GetMediaAsync(
        Guid? classId,
        Guid? studentId,
        Guid? classSessionId = null,
        string? fileType = null,
        VideoProcessingStatus? videoStatus = null,
        string? sortBy = null,
        bool isDescending = true,
        int page = 1,
        int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            throw ErrorHelper.BadRequest("Invalid pagination parameters.");

        if (pageSize > 100)
            throw ErrorHelper.BadRequest("pageSize must not exceed 100.");

        var currentUser = await GetCurrentUserOrThrowAsync();
        var resolved = await ResolveMediaQueryScopeAsync(currentUser, classId, studentId);
        var restrictToReady = currentUser.Role is RoleType.Student or RoleType.Parent;

        _logger.LogInformation(
            "GetMediaAsync: Role={Role}, ClassId={ClassId}, StudentId={StudentId}, ClassSessionId={ClassSessionId}, " +
            "FileType={FileType}, VideoStatus={VideoStatus}, ClassFilterCount={ClassCount}",
            currentUser.Role, resolved.ClassId, resolved.StudentId, classSessionId,
            fileType, videoStatus, resolved.ClassIds?.Count);

        if (resolved.ClassIds is { Count: 0 })
            return new Pagination<MediaAssetDto>([], 0, page, pageSize);

        List<MediaAsset> mediaList;
        if (resolved.ClassIds != null)
        {
            mediaList = await _unitOfWork.MediaAssets.GetAllAsync(
                m => !m.IsDeleted && resolved.ClassIds.Contains(m.ClassId),
                m => m.MediaTags);
        }
        else if (resolved.ClassId.HasValue)
        {
            mediaList = await _unitOfWork.MediaAssets.GetAllAsync(
                m => !m.IsDeleted && m.ClassId == resolved.ClassId.Value,
                m => m.MediaTags);
        }
        else
        {
            mediaList = await _unitOfWork.MediaAssets.GetAllAsync(
                m => !m.IsDeleted,
                m => m.MediaTags);
        }

        if (restrictToReady)
            mediaList = mediaList.Where(IsReadyMedia).ToList();

        if (classSessionId.HasValue)
            mediaList = mediaList.Where(m => m.ClassSessionId == classSessionId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            mediaList = mediaList
                .Where(m => string.Equals(m.FileType, fileType.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (videoStatus.HasValue)
            mediaList = mediaList.Where(m => m.VideoStatus == videoStatus.Value).ToList();

        if (resolved.StudentId.HasValue)
        {
            mediaList = mediaList
                .Where(m => m.MediaTags.Any(t => !t.IsDeleted && t.StudentId == resolved.StudentId.Value))
                .ToList();
        }
        else if (resolved.AllowedStudentIds != null)
        {
            mediaList = mediaList
                .Where(m => m.MediaTags.Any(t => !t.IsDeleted && resolved.AllowedStudentIds.Contains(t.StudentId)))
                .ToList();
        }

        mediaList = SortMediaList(mediaList, sortBy, isDescending);

        var totalCount = mediaList.Count;
        var pageItems = mediaList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = await MapMediaListToDtos(pageItems);
        return new Pagination<MediaAssetDto>(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<Pagination<ClassGalleryMediaDto>> GetClassGalleryAsync(
        Guid classId,
        Guid? classSessionId = null,
        string? fileType = null,
        VideoProcessingStatus? videoStatus = null,
        string? sortBy = null,
        bool isDescending = true,
        int page = 1,
        int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            throw ErrorHelper.BadRequest("Invalid pagination parameters.");

        if (pageSize > 100)
            throw ErrorHelper.BadRequest("pageSize must not exceed 100.");

        var currentUser = await GetCurrentUserOrThrowAsync();
        if (currentUser.Role != RoleType.Student)
            throw ErrorHelper.Forbidden("Only students can view the class gallery.");

        await EnsureClassExistsAsync(classId);

        var enrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == currentUser.Id
                  && ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
        if (enrollment == null)
            throw ErrorHelper.Forbidden("You must be actively enrolled in this class to view its gallery.");

        if (classSessionId.HasValue)
        {
            var session = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId.Value);
            if (session == null || session.IsDeleted)
                throw ErrorHelper.NotFound($"Class session '{classSessionId}' not found.");
            if (session.ClassId != classId)
                throw ErrorHelper.BadRequest("Class session does not belong to the specified class.");
        }

        _logger.LogInformation(
            "GetClassGalleryAsync: StudentId={StudentId}, ClassId={ClassId}, ClassSessionId={ClassSessionId}, " +
            "FileType={FileType}, VideoStatus={VideoStatus}",
            currentUser.Id, classId, classSessionId, fileType, videoStatus);

        var mediaList = await _unitOfWork.MediaAssets.GetAllAsync(
            m => !m.IsDeleted && m.ClassId == classId);

        mediaList = ApplyGalleryFilters(mediaList, classSessionId, fileType, videoStatus, sortBy, isDescending);

        var totalCount = mediaList.Count;
        var pageItems = mediaList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = await MapGalleryDtosAsync(pageItems);
        return new Pagination<ClassGalleryMediaDto>(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<Pagination<ClassGalleryMediaDto>> GetMyGalleryAsync(
        Guid? programId = null,
        Guid? classId = null,
        Guid? classSessionId = null,
        string? fileType = null,
        VideoProcessingStatus? videoStatus = null,
        string? sortBy = null,
        bool isDescending = true,
        int page = 1,
        int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            throw ErrorHelper.BadRequest("Invalid pagination parameters.");

        if (pageSize > 100)
            throw ErrorHelper.BadRequest("pageSize must not exceed 100.");

        var currentUser = await GetCurrentUserOrThrowAsync();
        if (currentUser.Role != RoleType.Student)
            throw ErrorHelper.Forbidden("Only students can view the enrollment gallery.");

        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == currentUser.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var enrolledClassIds = enrollments.Select(ce => ce.ClassId).Distinct().ToHashSet();

        if (classId.HasValue && !enrolledClassIds.Contains(classId.Value))
            throw ErrorHelper.Forbidden("You must be actively enrolled in this class to view its gallery.");

        if (enrolledClassIds.Count == 0)
            return new Pagination<ClassGalleryMediaDto>([], 0, page, pageSize);

        var enrolledClasses = await _unitOfWork.Classes.GetAllAsync(
            c => enrolledClassIds.Contains(c.Id) && !c.IsDeleted);

        if (programId.HasValue)
        {
            enrolledClasses = enrolledClasses
                .Where(c => c.ProgramId == programId.Value)
                .ToList();
            enrolledClassIds = enrolledClasses.Select(c => c.Id).ToHashSet();
            if (enrolledClassIds.Count == 0)
                return new Pagination<ClassGalleryMediaDto>([], 0, page, pageSize);
        }

        if (classId.HasValue)
            enrolledClassIds = [classId.Value];

        if (classSessionId.HasValue)
        {
            var session = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId.Value);
            if (session == null || session.IsDeleted)
                throw ErrorHelper.NotFound($"Class session '{classSessionId}' not found.");
            if (!enrolledClassIds.Contains(session.ClassId))
                throw ErrorHelper.Forbidden("You must be actively enrolled in the session's class.");
        }

        _logger.LogInformation(
            "GetMyGalleryAsync: StudentId={StudentId}, ProgramId={ProgramId}, ClassId={ClassId}, " +
            "ClassSessionId={ClassSessionId}, FileType={FileType}, VideoStatus={VideoStatus}, ClassCount={ClassCount}",
            currentUser.Id, programId, classId, classSessionId, fileType, videoStatus, enrolledClassIds.Count);

        var mediaList = await _unitOfWork.MediaAssets.GetAllAsync(
            m => !m.IsDeleted && enrolledClassIds.Contains(m.ClassId));

        mediaList = ApplyGalleryFilters(mediaList, classSessionId, fileType, videoStatus, sortBy, isDescending);

        var totalCount = mediaList.Count;
        var pageItems = mediaList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = await MapGalleryDtosAsync(pageItems);
        return new Pagination<ClassGalleryMediaDto>(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> GetMediaByIdAsync(Guid mediaId)
    {
        var currentUser = await GetCurrentUserOrThrowAsync();
        _logger.LogInformation(
            "GetMediaByIdAsync: MediaId={MediaId}, UserId={UserId}, Role={Role}",
            mediaId, currentUser.Id, currentUser.Role);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        await EnsureCanAccessMediaAsync(currentUser, media);

        return await MapToDto(media, media.MediaTags.Where(t => !t.IsDeleted).ToList());
    }

    /// <inheritdoc />
    public async Task<MediaProcessingProgressDto> GetProcessingProgressAsync(Guid mediaId)
    {
        var currentUser = await GetCurrentUserOrThrowAsync();
        _logger.LogInformation(
            "GetProcessingProgressAsync: MediaId={MediaId}, UserId={UserId}",
            mediaId, currentUser.Id);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        await EnsureCanAccessMediaAsync(currentUser, media);

        var isVideo = string.Equals(media.FileType, "video", StringComparison.OrdinalIgnoreCase);
        var isReady = !isVideo || media.VideoStatus == VideoProcessingStatus.TaggingComplete;
        var isFailed = media.VideoStatus == VideoProcessingStatus.Failed;

        int? percentComplete = null;
        if (isVideo && media.VideoStatus == VideoProcessingStatus.Transcoding)
        {
            if (!string.IsNullOrEmpty(media.MediaConvertJobId))
            {
                var progress = await _videoConverterService.GetJobProgressAsync(media.MediaConvertJobId);
                percentComplete = progress.PercentComplete;
            }
            else
            {
                percentComplete = 0;
            }
        }
        else if (isVideo && media.VideoStatus == VideoProcessingStatus.TaggingComplete)
        {
            percentComplete = 100;
        }

        return new MediaProcessingProgressDto
        {
            MediaId = media.Id,
            VideoStatus = media.VideoStatus,
            StatusLabel = GetVideoStatusLabel(media.FileType, media.VideoStatus),
            PercentComplete = percentComplete,
            IsReady = isReady,
            IsFailed = isFailed,
            FileUrl = media.FileUrl
        };
    }

    /// <inheritdoc />
    public async Task<MediaTagDto> AddMediaTagAsync(Guid mediaId, Guid studentId)
    {
        var currentUser = await GetCurrentUserOrThrowAsync();
        EnsureCanManageTags(currentUser);

        var media = await LoadReadyMediaOrThrowAsync(mediaId);
        var student = await LoadStudentOrThrowAsync(studentId);
        await EnsureCanManageMediaStudentAsync(currentUser, media, studentId);

        var existingList = await _unitOfWork.MediaTags.GetAllIncludingDeletedAsync(
            t => t.MediaId == mediaId && t.StudentId == studentId);
        var existing = existingList.FirstOrDefault();

        if (existing != null && !existing.IsDeleted)
            throw ErrorHelper.Conflict("Student is already tagged on this media.");

        // Mentor late tags are participation / project credit: empty face timeline (Case 1 /
        // scene-only) so highlight generation can use full video or label-only strength
        // filtering without inventing face windows or rewriting other students' timelines.
        var isVideo = string.Equals(media.FileType, "video", StringComparison.OrdinalIgnoreCase);
        var hasOtherActiveTags = media.MediaTags.Any(t =>
            !t.IsDeleted && t.StudentId != studentId);
        var emptyFaceTimelineJson = isVideo ? "[]" : null;

        MediaTag tag;
        if (existing != null)
        {
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.IsVerified = true;
            existing.ConfidenceScore = 100m;
            if (isVideo)
            {
                existing.FaceSegmentsJson = emptyFaceTimelineJson;
                existing.HasOtherFaces = hasOtherActiveTags;
            }

            tag = existing;
        }
        else
        {
            tag = new MediaTag
            {
                MediaId = mediaId,
                StudentId = studentId,
                ConfidenceScore = 100m,
                IsVerified = true,
                FaceSegmentsJson = emptyFaceTimelineJson,
                HasOtherFaces = isVideo && hasOtherActiveTags,
            };
            await _unitOfWork.MediaTags.AddAsync(tag);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "AddMediaTagAsync: MediaId={MediaId}, StudentId={StudentId}, By={UserId}",
            mediaId, studentId, currentUser.Id);

        return MapTagToDto(tag, new Dictionary<Guid, User> { [student.Id] = student });
    }

    /// <inheritdoc />
    public async Task RemoveMediaTagAsync(Guid mediaId, Guid studentId)
    {
        var currentUser = await GetCurrentUserOrThrowAsync();
        EnsureCanManageTags(currentUser);

        var media = await LoadReadyMediaOrThrowAsync(mediaId);
        await EnsureCanManageMediaStudentAsync(currentUser, media, studentId);

        var tag = await _unitOfWork.MediaTags.FirstOrDefaultAsync(
            t => t.MediaId == mediaId && t.StudentId == studentId && !t.IsDeleted)
            ?? throw ErrorHelper.NotFound("Media tag not found.");

        await _unitOfWork.MediaTags.SoftRemove(tag);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "RemoveMediaTagAsync: MediaId={MediaId}, StudentId={StudentId}, By={UserId}",
            mediaId, studentId, currentUser.Id);
    }

    /// <inheritdoc />
    public async Task<MediaTagDto> SetMediaTagVerificationAsync(Guid mediaId, Guid studentId, bool isVerified)
    {
        var currentUser = await GetCurrentUserOrThrowAsync();
        EnsureCanManageTags(currentUser);

        var media = await LoadReadyMediaOrThrowAsync(mediaId);
        var student = await LoadStudentOrThrowAsync(studentId);
        await EnsureCanManageMediaStudentAsync(currentUser, media, studentId);

        var tag = await _unitOfWork.MediaTags.FirstOrDefaultAsync(
            t => t.MediaId == mediaId && t.StudentId == studentId && !t.IsDeleted)
            ?? throw ErrorHelper.NotFound("Media tag not found.");

        tag.IsVerified = isVerified;
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "SetMediaTagVerificationAsync: MediaId={MediaId}, StudentId={StudentId}, IsVerified={IsVerified}, By={UserId}",
            mediaId, studentId, isVerified, currentUser.Id);

        return MapTagToDto(tag, new Dictionary<Guid, User> { [student.Id] = student });
    }

    /// <inheritdoc />
    /// Submits a MediaConvert job (non-blocking). After this returns, VideoStatus
    /// is still <see cref="VideoProcessingStatus.Transcoding"/> and
    /// <c>MediaConvertJobId</c> holds the MC job ID.
    public async Task StartVideoTranscodeAsync(Guid mediaId)
    {
        _logger.LogInformation("StartVideoTranscodeAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        if (media == null || media.IsDeleted)
        {
            _logger.LogWarning("StartVideoTranscodeAsync: MediaId={MediaId} not found or deleted.", mediaId);
            return;
        }

        if (media.VideoStatus != VideoProcessingStatus.Transcoding)
        {
            _logger.LogInformation("StartVideoTranscodeAsync: skipping, VideoStatus={Status}", media.VideoStatus);
            return;
        }

        // If a MC job was already submitted (e.g. recovery after crash), skip re-submission.
        if (!string.IsNullOrEmpty(media.MediaConvertJobId))
        {
            _logger.LogInformation(
                "StartVideoTranscodeAsync: MC job already submitted for MediaId={MediaId}. Skipping.",
                mediaId);
            return;
        }

        var rawS3Key = media.RawVideoS3Key;
        if (string.IsNullOrEmpty(rawS3Key))
            throw new InvalidOperationException(
                $"MediaId={mediaId}: expected RawVideoS3Key to be set.");

        try
        {
            // ── Submit MediaConvert job (returns immediately) ─────────────────
            _logger.LogInformation(
                "Submitting MediaConvert job: raw={RawKey} → {Folder}/ for MediaId={MediaId}",
                rawS3Key, MediaFolder, mediaId);

            var mcJobId = await _videoConverterService.SubmitTranscodeJobAsync(
                rawS3Key, $"{MediaFolder}/");

            // ── Persist MC job ID for HandleMediaConvertWebhookAsync correlation ─
            media.MediaConvertJobId = mcJobId;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "MediaConvert job submitted: {McJobId} for MediaId={MediaId}",
                mcJobId, mediaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartVideoTranscodeAsync failed for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.MediaProcessingFailed(media.UploaderId, media.Id));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryCompleteTranscodeAsync(Guid mediaId)
    {
        _logger.LogInformation("TryCompleteTranscodeAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        if (media == null || media.IsDeleted)
        {
            _logger.LogWarning("TryCompleteTranscodeAsync: MediaId={MediaId} not found or deleted.", mediaId);
            return false;
        }

        if (media.VideoStatus != VideoProcessingStatus.Transcoding)
        {
            _logger.LogInformation(
                "TryCompleteTranscodeAsync: unexpected VideoStatus={Status} for MediaId={MediaId}",
                media.VideoStatus, mediaId);
            return false;
        }

        var mcJobId = media.MediaConvertJobId;
        if (string.IsNullOrEmpty(mcJobId))
            throw new InvalidOperationException(
                $"MediaId={mediaId}: expected MediaConvertJobId to be set.");

        // ── Poll MediaConvert ─────────────────────────────────────────────────
        var status = await _videoConverterService.GetJobStatusAsync(mcJobId);

        if (status == MediaConvertJobStatus.InProgress)
        {
            _logger.LogInformation(
                "MediaConvert job {McJobId} still in progress for MediaId={MediaId}",
                mcJobId, mediaId);
            return false;
        }

        if (status == MediaConvertJobStatus.Error)
        {
            _logger.LogError(
                "MediaConvert job {McJobId} failed for MediaId={MediaId}",
                mcJobId, mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.MediaProcessingFailed(media.UploaderId, media.Id));
            throw new InvalidOperationException(
                $"MediaConvert job {mcJobId} failed for MediaId={mediaId}.");
        }

        // ── status == Complete ────────────────────────────────────────────────
        _logger.LogInformation(
            "MediaConvert job {McJobId} completed for MediaId={MediaId}",
            mcJobId, mediaId);

        string outputS3Key;
        string fileUrl;
        string rekJobId;

        try
        {
            outputS3Key = await _videoConverterService.GetOutputS3KeyAsync(mcJobId);
            fileUrl = await _blobService.GetPreviewUrlAsync(outputS3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryCompleteTranscodeAsync: failed to resolve output for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.MediaProcessingFailed(media.UploaderId, media.Id));
            throw;
        }

        // ── Start Rekognition face-search ───────────────────────────────────────────
        try
        {
            _logger.LogInformation(
                "Starting Rekognition video face-search for MediaId={MediaId}, S3Key={Key}",
                mediaId, outputS3Key);
            rekJobId = await _faceRecognitionService.StartVideoFaceSearchAsync(
                _blobService.BucketName, outputS3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryCompleteTranscodeAsync: failed to start Rekognition for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.MediaProcessingFailed(media.UploaderId, media.Id));
            throw;
        }

        // ── Start Rekognition Label Detection (for strengths-based filtering) ──────
        // Fire-and-forget style: failures are non-fatal — the strengths pipeline simply
        // falls back to face-only clipping when LabelJobRef is null or the job fails.
        try
        {
            _logger.LogInformation(
                "Starting Rekognition Label Detection for MediaId={MediaId}, S3Key={Key}",
                mediaId, outputS3Key);
            var labelJobId = await _faceRecognitionService.StartLabelDetectionAsync(
                _blobService.BucketName, outputS3Key);
            media.LabelJobRef = labelJobId;
            _logger.LogInformation(
                "Label Detection job started: {LabelJobId} for MediaId={MediaId}", labelJobId, mediaId);
        }
        catch (Exception ex)
        {
            // Non-fatal: strengths filtering will fall back gracefully.
            _logger.LogWarning(ex,
                "TryCompleteTranscodeAsync: failed to start Label Detection for MediaId={MediaId}. " +
                "Strengths-based filtering will be unavailable for this video.", mediaId);
        }

        // ── Persist state transition ──────────────────────────────────────────────
        media.FileUrl = fileUrl;
        media.FaceSearchJobId = rekJobId;
        media.VideoStatus = VideoProcessingStatus.PendingTagging;
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Rekognition job started: {RekJobId} for MediaId={MediaId}",
            rekJobId, mediaId);

        // ── Delete raw source file from S3 (no longer needed) ──────────────────────
        // The transcoded output is in media/; keeping raw/ wastes storage.
        // Failure to delete is logged but non-fatal.
        try
        {
            var rawS3Key = await _videoConverterService.GetInputS3KeyAsync(mcJobId);
            _logger.LogInformation(
                "Deleting raw S3 file after transcode. Key={RawKey}, MediaId={MediaId}",
                rawS3Key, mediaId);
            await _blobService.DeleteByKeyAsync(rawS3Key);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx,
                "Failed to delete raw S3 file for MediaId={MediaId}. Manual cleanup may be needed.",
                mediaId);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId)
    {
        _logger.LogInformation("ProcessVideoTagsAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        if (!CanRestartVideoFaceSearch(media))
        {
            throw ErrorHelper.BadRequest(
                media.FileType != "video"
                    ? "Face tag processing applies to video media only."
                    : media.VideoStatus == VideoProcessingStatus.Transcoding
                        ? "Video is still transcoding. Try again after transcoding completes."
                        : "Transcoded video output is not available yet.");
        }

        await RestartVideoFaceSearchAsync(media);

        var (success, newTags) = await DoProcessVideoTagsAsync(media);
        if (success)
            return await MapToDto(media, media.MediaTags.Concat(newTags).ToList());

        return await MapToDto(media, media.MediaTags.Where(t => !t.IsDeleted).ToList());
    }

    /// <inheritdoc />
    public async Task<bool> TryProcessVideoTagsAsync(Guid mediaId)
    {
        _logger.LogInformation("TryProcessVideoTagsAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted) return false;

        if (!ShouldProcessVideoFaceTags(media))
        {
            _logger.LogInformation(
                "TryProcessVideoTagsAsync skipped: MediaId={MediaId}, VideoStatus={Status}, ActiveTagCount={TagCount}",
                mediaId, media.VideoStatus, media.MediaTags.Count(t => !t.IsDeleted));
            return false;
        }

        if (media.VideoStatus == VideoProcessingStatus.TaggingComplete)
        {
            _logger.LogInformation(
                "TryProcessVideoTagsAsync: late face-webhook recovery for MediaId={MediaId}.",
                mediaId);
        }

        var (success, _) = await DoProcessVideoTagsAsync(media);
        return success;
    }

    /// <inheritdoc />
    public async Task DeleteMediaAsync(Guid mediaId)
    {
        _logger.LogInformation("DeleteMediaAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        // Delete the file from S3 using the bucket-relative key.
        // FileUrl is a presigned/CDN URL — extract the key before deleting.
        if (!string.IsNullOrWhiteSpace(media.FileUrl))
        {
            try
            {
                var uri = new Uri(media.FileUrl);
                var s3Key = uri.AbsolutePath.TrimStart('/');
                // Strip bucket prefix if present (path-style URL: /{bucket}/{key})
                var bucketPrefix = $"{_blobService.BucketName}/";
                if (s3Key.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
                    s3Key = s3Key[bucketPrefix.Length..];

                await _blobService.DeleteByKeyAsync(s3Key);
            }
            catch (UriFormatException)
            {
                // FileUrl is already a raw key (older records)
                await _blobService.DeleteByKeyAsync(media.FileUrl);
            }
        }

        await _unitOfWork.MediaAssets.SoftRemove(media);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Media deleted: {MediaId}", mediaId);
    }

    /// <inheritdoc />
    public async Task<bool> IsAwaitingTaggingAsync(Guid mediaId)
    {
        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        return media != null && ShouldProcessVideoFaceTags(media);
    }

    /// <inheritdoc />
    public async Task<bool> HandleMediaConvertWebhookAsync(string jobId, bool isSuccess)
    {
        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.MediaConvertJobId == jobId && !m.IsDeleted);

        if (mediaAsset == null)
            return false;

        if (isSuccess)
        {
            try
            {
                await TryCompleteTranscodeAsync(mediaAsset.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TryCompleteTranscodeAsync failed for MediaId {Id}", mediaAsset.Id);
            }
        }
        else
        {
            mediaAsset.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
        }

        return true;
    }

    /// <inheritdoc />
    public async Task HandleFaceSearchWebhookAsync(string jobId, bool isSuccess)
    {
        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.FaceSearchJobId == jobId && !m.IsDeleted);

        if (mediaAsset != null)
        {
            _logger.LogInformation(
                "HandleFaceSearchWebhookAsync: JobId={JobId}, MediaId={MediaId}, VideoStatus={Status}, IsSuccess={Success}",
                jobId, mediaAsset.Id, mediaAsset.VideoStatus, isSuccess);

            if (isSuccess)
            {
                try
                {
                    await TryProcessVideoTagsAsync(mediaAsset.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TryProcessVideoTagsAsync failed for MediaId {Id}", mediaAsset.Id);
                }
            }
            else
            {
                mediaAsset.VideoStatus = VideoProcessingStatus.Failed;
                await _unitOfWork.SaveChangesAsync();
            }
        }
        else
        {
            _logger.LogWarning("HandleFaceSearchWebhookAsync: no MediaAsset found for FaceSearch JobId={JobId}", jobId);
        }
    }

    /// <inheritdoc />
    public async Task HandleLabelDetectionWebhookAsync(string jobId, bool isSuccess)
    {
        if (!isSuccess)
        {
            // Mark the asset so strengths filtering knows the label job failed.
            var labelAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
                m => m.LabelJobRef == jobId && !m.IsDeleted);

            if (labelAsset != null)
            {
                _logger.LogWarning(
                    "Label Detection job FAILED for MediaId={MediaId}, JobId={JobId}. " +
                    "Clearing LabelJobRef so strengths filter falls back gracefully.",
                    labelAsset.Id, jobId);
                labelAsset.LabelJobRef = null;
                await _unitOfWork.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning(
                    "Label Detection FAILED for unknown JobId={JobId} (no matching MediaAsset).", jobId);
            }
        }
        else
        {
            // Capture the label timeline NOW, while the Rekognition job results are still
            // available (Rekognition retains video job results for only 7 days). Persisting
            // them lets the strengths-filtering pipeline run indefinitely without re-querying.
            var labelAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
                m => m.LabelJobRef == jobId && !m.IsDeleted);

            if (labelAsset == null)
            {
                _logger.LogWarning(
                    "Label Detection SUCCEEDED for unknown JobId={JobId} (no matching MediaAsset).", jobId);
                return;
            }

            try
            {
                var labelTimeline = await _faceRecognitionService.GetLabelDetectionResultsAsync(jobId);

                if (labelTimeline == null)
                {
                    // Job reported SUCCEEDED via webhook but the query says IN_PROGRESS —
                    // a rare eventual-consistency race. Leave LabelTimelineJson null; the
                    // strengths filter will fall back to face-only for this video.
                    _logger.LogWarning(
                        "HandleLabelDetectionWebhookAsync: results not ready yet for MediaId={MediaId}, JobId={JobId}.",
                        labelAsset.Id, jobId);
                    return;
                }

                labelAsset.LabelTimelineJson = JsonSerializer.Serialize(labelTimeline);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Label Detection captured: {Count} entry/entries persisted for MediaId={MediaId}, JobId={JobId}.",
                    labelTimeline.Count, labelAsset.Id, jobId);
            }
            catch (Exception ex)
            {
                // Non-fatal: strengths filtering will fall back to face-only for this video.
                _logger.LogWarning(ex,
                    "HandleLabelDetectionWebhookAsync: failed to capture label timeline for MediaId={MediaId}, JobId={JobId}.",
                    labelAsset.Id, jobId);
            }
        }
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private sealed record MediaQueryScope(
        Guid? ClassId,
        Guid? StudentId,
        HashSet<Guid>? ClassIds,
        HashSet<Guid>? AllowedStudentIds);

    private async Task<User> GetCurrentUserOrThrowAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
            throw ErrorHelper.Unauthorized("Authentication required.");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.Unauthorized("Authenticated user was not found.");

        return user;
    }

    private static void EnsureCanManageTags(User currentUser)
    {
        if (currentUser.Role is not (RoleType.Mentor or RoleType.Manager or RoleType.SuperAdmin))
            throw ErrorHelper.Forbidden("Only mentors and managers can manage media tags.");
    }

    private static bool IsReadyMedia(MediaAsset media) =>
        !string.Equals(media.FileType, "video", StringComparison.OrdinalIgnoreCase)
        || media.VideoStatus == VideoProcessingStatus.TaggingComplete;

    private async Task<MediaAsset> LoadReadyMediaOrThrowAsync(Guid mediaId)
    {
        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        if (!IsReadyMedia(media))
            throw ErrorHelper.BadRequest("Media must be ready before tags can be managed.");

        return media;
    }

    private async Task<User> LoadStudentOrThrowAsync(Guid studentId)
    {
        var student = await _unitOfWork.Users.GetByIdAsync(studentId);
        if (student == null || student.IsDeleted || student.Role != RoleType.Student)
            throw ErrorHelper.NotFound($"Student '{studentId}' not found.");

        return student;
    }

    private async Task EnsureCanAccessClassMediaAsync(User currentUser, Guid classId)
    {
        switch (currentUser.Role)
        {
            case RoleType.SuperAdmin:
            case RoleType.Manager:
                return;
            case RoleType.Mentor:
                await MentorScopeValidator.EnsureMentorOwnsClassAsync(_unitOfWork, currentUser.Id, classId);
                return;
            case RoleType.Student:
            case RoleType.Parent:
                return;
            default:
                throw ErrorHelper.Forbidden("You are not allowed to view media.");
        }
    }

    private async Task EnsureCanAccessMediaAsync(User currentUser, MediaAsset media)
    {
        switch (currentUser.Role)
        {
            case RoleType.SuperAdmin:
            case RoleType.Manager:
                return;
            case RoleType.Mentor:
                await MentorScopeValidator.EnsureMentorOwnsClassAsync(
                    _unitOfWork, currentUser.Id, media.ClassId);
                return;
            case RoleType.Student:
            {
                if (!IsReadyMedia(media))
                    throw ErrorHelper.NotFound("Media not found.");

                if (!media.MediaTags.Any(t => !t.IsDeleted && t.StudentId == currentUser.Id))
                    throw ErrorHelper.Forbidden("You can only view media you are tagged in.");
                return;
            }
            case RoleType.Parent:
            {
                if (!IsReadyMedia(media))
                    throw ErrorHelper.NotFound("Media not found.");

                var linkedStudentIds = await GetLinkedStudentIdsAsync(currentUser.Id);
                if (!media.MediaTags.Any(t => !t.IsDeleted && linkedStudentIds.Contains(t.StudentId)))
                    throw ErrorHelper.Forbidden("You can only view media tagged for your linked students.");
                return;
            }
            default:
                throw ErrorHelper.Forbidden("You are not allowed to view media.");
        }
    }

    private async Task<MediaQueryScope> ResolveMediaQueryScopeAsync(
        User currentUser,
        Guid? classId,
        Guid? studentId)
    {
        switch (currentUser.Role)
        {
            case RoleType.SuperAdmin:
            case RoleType.Manager:
            {
                if (classId.HasValue)
                    await EnsureClassExistsAsync(classId.Value);

                if (studentId.HasValue)
                    await LoadStudentOrThrowAsync(studentId.Value);

                return new MediaQueryScope(classId, studentId, null, null);
            }
            case RoleType.Mentor:
            {
                HashSet<Guid> classIds;
                if (classId.HasValue)
                {
                    await MentorScopeValidator.EnsureMentorOwnsClassAsync(
                        _unitOfWork, currentUser.Id, classId.Value);
                    classIds = new HashSet<Guid> { classId.Value };
                }
                else
                {
                    classIds = await GetMentorClassIdsAsync(currentUser.Id);
                }

                if (studentId.HasValue)
                {
                    await LoadStudentOrThrowAsync(studentId.Value);
                    if (classId.HasValue)
                        await EnsureStudentActiveInClassAsync(studentId.Value, classId.Value);
                    else
                        await EnsureStudentInMentorClassesAsync(currentUser.Id, studentId.Value);
                }

                return new MediaQueryScope(classId, studentId, classIds, null);
            }
            case RoleType.Student:
            {
                if (classId.HasValue)
                    await EnsureClassExistsAsync(classId.Value);

                return new MediaQueryScope(classId, currentUser.Id, null, null);
            }
            case RoleType.Parent:
            {
                if (!studentId.HasValue)
                    throw ErrorHelper.BadRequest("studentId is required for parent media queries.");

                await EnsureParentLinkedToStudentAsync(currentUser.Id, studentId.Value);

                if (classId.HasValue)
                    await EnsureClassExistsAsync(classId.Value);

                return new MediaQueryScope(classId, studentId, null, null);
            }
            default:
                throw ErrorHelper.Forbidden("You are not allowed to view media.");
        }
    }

    private async Task EnsureCanManageMediaStudentAsync(User currentUser, MediaAsset media, Guid studentId)
    {
        if (currentUser.Role is RoleType.Manager or RoleType.SuperAdmin)
            return;

        if (currentUser.Role != RoleType.Mentor)
            throw ErrorHelper.Forbidden("Only mentors and managers can manage media tags.");

        await MentorScopeValidator.EnsureMentorOwnsClassAsync(_unitOfWork, currentUser.Id, media.ClassId);

        var enrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == studentId
                  && ce.ClassId == media.ClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (enrollment == null)
            throw ErrorHelper.Forbidden(
                "Student must be actively enrolled in the class this media belongs to.");
    }

    /// <summary>
    /// Ensures the class exists and, when provided, that <paramref name="classSessionId"/>
    /// belongs to that class. Returns the validated session id (or null).
    /// </summary>
    private async Task<Guid?> ValidateClassAndOptionalSessionAsync(Guid classId, Guid? classSessionId)
    {
        await EnsureClassExistsAsync(classId);

        if (!classSessionId.HasValue)
            return null;

        var session = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId.Value);
        if (session == null || session.IsDeleted)
            throw ErrorHelper.NotFound($"Class session '{classSessionId}' not found.");

        if (session.ClassId != classId)
            throw ErrorHelper.BadRequest("Class session does not belong to the specified class.");

        return session.Id;
    }

    private async Task EnsureClassExistsAsync(Guid classId)
    {
        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
        if (classEntity == null || classEntity.IsDeleted)
            throw ErrorHelper.NotFound($"Class '{classId}' not found.");
    }

    private async Task EnsureStudentActiveInClassAsync(Guid studentId, Guid classId)
    {
        var enrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == studentId
                  && ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (enrollment == null)
            throw ErrorHelper.BadRequest("Student is not actively enrolled in the specified class.");
    }

    private async Task EnsureStudentInMentorClassesAsync(Guid mentorId, Guid studentId)
    {
        var classes = await _unitOfWork.Classes.GetAllAsync(c => c.MentorId == mentorId && !c.IsDeleted);
        var classIds = classes.Select(c => c.Id).ToList();
        if (classIds.Count == 0)
            throw ErrorHelper.Forbidden(MentorScopeValidator.StudentNotInMentorClassMessage);

        var enrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == studentId
                  && classIds.Contains(ce.ClassId)
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (enrollment == null)
            throw ErrorHelper.Forbidden(MentorScopeValidator.StudentNotInMentorClassMessage);
    }

    private async Task EnsureParentLinkedToStudentAsync(Guid parentId, Guid studentId)
    {
        await LoadStudentOrThrowAsync(studentId);

        var link = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(
            ps => ps.ParentId == parentId
                  && ps.StudentId == studentId
                  && ps.IsVerified
                  && !ps.IsDeleted);

        if (link == null)
            throw ErrorHelper.Forbidden("You can only view media for your linked students.");
    }

    private async Task<HashSet<Guid>> GetLinkedStudentIdsAsync(Guid parentId)
    {
        var links = await _unitOfWork.ParentStudents.GetAllAsync(
            ps => ps.ParentId == parentId && ps.IsVerified && !ps.IsDeleted);
        return links.Select(l => l.StudentId).ToHashSet();
    }

    private async Task<HashSet<Guid>> GetMentorClassIdsAsync(Guid mentorId)
    {
        var classes = await _unitOfWork.Classes.GetAllAsync(c => c.MentorId == mentorId && !c.IsDeleted);
        return classes.Select(c => c.Id).ToHashSet();
    }

    private async Task<List<MediaAssetDto>> MapMediaListToDtos(IReadOnlyList<MediaAsset> mediaList)
    {
        var allStudentIds = mediaList
            .SelectMany(m => m.MediaTags.Where(t => !t.IsDeleted))
            .Select(t => t.StudentId)
            .Distinct()
            .ToList();

        var studentMap = allStudentIds.Count > 0
            ? (await _unitOfWork.Users.GetAllAsync(u => allStudentIds.Contains(u.Id)))
              .ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();

        return mediaList
            .Select(m => MapAssetToDto(m, m.MediaTags.Where(t => !t.IsDeleted), studentMap))
            .ToList();
    }

    private static List<MediaAsset> SortMediaList(
        List<MediaAsset> mediaList,
        string? sortBy,
        bool isDescending)
    {
        return (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "createdat" => isDescending
                ? mediaList.OrderByDescending(m => m.CreatedAt).ToList()
                : mediaList.OrderBy(m => m.CreatedAt).ToList(),
            "filetype" => isDescending
                ? mediaList.OrderByDescending(m => m.FileType).ToList()
                : mediaList.OrderBy(m => m.FileType).ToList(),
            "videostatus" => isDescending
                ? mediaList.OrderByDescending(m => m.VideoStatus).ToList()
                : mediaList.OrderBy(m => m.VideoStatus).ToList(),
            "uploadedat" or _ => isDescending
                ? mediaList.OrderByDescending(m => m.UploadedAt ?? m.CreatedAt).ToList()
                : mediaList.OrderBy(m => m.UploadedAt ?? m.CreatedAt).ToList(),
        };
    }

    /// <summary>
    /// True when the manual <c>process-tags</c> endpoint may submit a new Rekognition job
    /// against the transcoded output in S3.
    /// </summary>
    private bool CanRestartVideoFaceSearch(MediaAsset media)
    {
        if (media.IsDeleted || !string.Equals(media.FileType, "video", StringComparison.OrdinalIgnoreCase))
            return false;

        if (media.VideoStatus == VideoProcessingStatus.Transcoding)
            return false;

        return !string.IsNullOrWhiteSpace(ExtractS3KeyFromFileUrl(media.FileUrl));
    }

    /// <summary>
    /// Submits fresh Rekognition face-search and label-detection jobs for the transcoded MP4.
    /// </summary>
    private async Task RestartVideoFaceSearchAsync(MediaAsset media)
    {
        var s3Key = ExtractS3KeyFromFileUrl(media.FileUrl);
        if (string.IsNullOrWhiteSpace(s3Key))
            throw ErrorHelper.BadRequest("Transcoded video output is not available.");

        _logger.LogInformation(
            "RestartVideoFaceSearchAsync: MediaId={MediaId}, S3Key={Key}, PreviousJobId={PreviousJobId}",
            media.Id, s3Key, media.FaceSearchJobId);

        string rekJobId;
        try
        {
            rekJobId = await _faceRecognitionService.StartVideoFaceSearchAsync(
                _blobService.BucketName, s3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RestartVideoFaceSearchAsync: failed to start Rekognition for MediaId={MediaId}", media.Id);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw;
        }

        media.FaceSearchJobId = rekJobId;
        media.VideoStatus = VideoProcessingStatus.PendingTagging;

        try
        {
            var labelJobId = await _faceRecognitionService.StartLabelDetectionAsync(
                _blobService.BucketName, s3Key);
            media.LabelJobRef = labelJobId;
            media.LabelTimelineJson = null;
            _logger.LogInformation(
                "RestartVideoFaceSearchAsync: label detection restarted. JobId={LabelJobId}, MediaId={MediaId}",
                labelJobId, media.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RestartVideoFaceSearchAsync: failed to restart label detection for MediaId={MediaId}.",
                media.Id);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "RestartVideoFaceSearchAsync: Rekognition job started. JobId={JobId}, MediaId={MediaId}",
            rekJobId, media.Id);
    }

    private string? ExtractS3KeyFromFileUrl(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return null;

        try
        {
            var uri = new Uri(fileUrl);
            var s3Key = uri.AbsolutePath.TrimStart('/');
            var bucketPrefix = $"{_blobService.BucketName}/";
            if (s3Key.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
                s3Key = s3Key[bucketPrefix.Length..];
            return s3Key;
        }
        catch (UriFormatException)
        {
            return fileUrl.Trim();
        }
    }

    /// <summary>
    /// True when Rekognition face-search results should be polled and <see cref="MediaTag"/> rows
    /// (plus face timelines) persisted. Covers the normal <see cref="VideoProcessingStatus.PendingTagging"/>
    /// path and late face-search webhooks that arrive after tagging already completed with zero tags.
    /// </summary>
    private bool ShouldProcessVideoFaceTags(MediaAsset media)
    {
        if (media.IsDeleted || string.IsNullOrEmpty(media.FaceSearchJobId))
        {
            _logger.LogDebug(
                "ShouldProcessVideoFaceTags=false: MediaId={MediaId}, IsDeleted={Deleted}, HasJobId={HasJobId}",
                media.Id, media.IsDeleted, !string.IsNullOrEmpty(media.FaceSearchJobId));
            return false;
        }

        if (media.VideoStatus == VideoProcessingStatus.Failed)
        {
            _logger.LogDebug(
                "ShouldProcessVideoFaceTags=false: MediaId={MediaId}, VideoStatus=Failed",
                media.Id);
            return false;
        }

        var activeTags = media.MediaTags.Where(t => !t.IsDeleted).ToList();

        if (media.VideoStatus is VideoProcessingStatus.PendingTagging)
        {
            _logger.LogDebug(
                "ShouldProcessVideoFaceTags=true: MediaId={MediaId}, VideoStatus={Status}",
                media.Id, media.VideoStatus);
            return true;
        }

        if (media.VideoStatus != VideoProcessingStatus.TaggingComplete)
        {
            _logger.LogDebug(
                "ShouldProcessVideoFaceTags=false: MediaId={MediaId}, VideoStatus={Status} (not TaggingComplete)",
                media.Id, media.VideoStatus);
            return false;
        }

        if (activeTags.Count == 0)
        {
            _logger.LogDebug(
                "ShouldProcessVideoFaceTags=true: MediaId={MediaId}, TaggingComplete with zero active tags (late recovery)",
                media.Id);
            return true;
        }

        var allMissingTimeline = activeTags.All(t => string.IsNullOrEmpty(t.FaceSegmentsJson));
        _logger.LogDebug(
            "ShouldProcessVideoFaceTags={Result}: MediaId={MediaId}, TaggingComplete, ActiveTags={Count}, AllMissingTimeline={Missing}",
            allMissingTimeline, media.Id, activeTags.Count, allMissingTimeline);
        return allMissingTimeline;
    }

    /// <summary>
    /// Advances <see cref="MediaAsset.VideoStatus"/> to <see cref="VideoProcessingStatus.TaggingComplete"/>
    /// once face tagging is persisted.
    /// </summary>
    private async Task TryAdvanceToProcessingCompleteAsync(MediaAsset media)
    {
        if (media.VideoStatus is VideoProcessingStatus.Failed or VideoProcessingStatus.TaggingComplete)
            return;

        media.VideoStatus = VideoProcessingStatus.TaggingComplete;
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation(
            "VideoStatus=TaggingComplete for MediaId={MediaId}.", media.Id);

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.MediaVideoReady(media.UploaderId, media.Id));
    }

    /// <summary>
    /// Core logic shared by <see cref="ProcessVideoTagsAsync"/> and <see cref="TryProcessVideoTagsAsync"/>.
    /// Returns (true, newTags) when the Rekognition job succeeded and tags were saved,
    /// or (false, empty) when the job is still IN_PROGRESS.
    /// Throws on FAILED status.
    /// </summary>
    private async Task<(bool Done, List<MediaTag> NewTags)> DoProcessVideoTagsAsync(MediaAsset media)
    {
        var result = await _faceRecognitionService.GetVideoFaceSearchResultsAsync(media.FaceSearchJobId!);

        if (result == null)
            return (false, new List<MediaTag>()); // still IN_PROGRESS

        if (result.JobStatus == "FAILED")
        {
            _logger.LogWarning("Rekognition job FAILED for MediaId: {MediaId}, JobId: {JobId}",
                media.Id, media.FaceSearchJobId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.MediaAiTaggingFailed(media.UploaderId, media.Id));
            throw ErrorHelper.Internal("Video face recognition job failed.");
        }

        var newTags = new List<MediaTag>();

        // Capture per-student appearance timelines NOW, while the Rekognition job results
        // are still available (Rekognition retains video job results for only 7 days).
        // Persisting them lets the personal-video pipeline build clips indefinitely without
        // re-querying Rekognition. A null result means the job is not SUCCEEDED — unexpected
        // here since result.JobStatus was already checked, so we just skip persistence.
        Dictionary<Guid, VideoFaceTimelineResult>? timelines = null;
        try
        {
            timelines = await _faceRecognitionService.GetAllFaceTimelinesAsync(media.FaceSearchJobId!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "DoProcessVideoTagsAsync: failed to capture face timelines for MediaId={MediaId}. " +
                "Personal video generation will fall back to the safe legacy policy.", media.Id);
        }

        // Only tag students Active-enrolled in this media's class (same rule as manual tags).
        var inClassMatches = await FilterMatchesToActiveClassStudentsAsync(media.ClassId, result.Matches);

        // Batch-load all matched students in one query to avoid N+1 DB round-trips.
        var matchedUserIds = inClassMatches
            .Select(m => m.UserId)
            .Distinct()
            .ToList();
        var studentMap = matchedUserIds.Count > 0
            ? (await _unitOfWork.Users.GetAllAsync(u => matchedUserIds.Contains(u.Id)))
              .ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();

        foreach (var match in inClassMatches)
        {
            // Skip duplicates already in DB
            if (media.MediaTags.Any(t => t.StudentId == match.UserId))
            {
                _logger.LogDebug(
                    "DoProcessVideoTagsAsync: skipping duplicate tag for StudentId={StudentId}, MediaId={MediaId}",
                    match.UserId, media.Id);
                continue;
            }

            if (!studentMap.TryGetValue(match.UserId, out var student))
            {
                _logger.LogWarning("Skipping face match for non-existent UserId: {UserId} (FaceId: {FaceId})",
                    match.UserId, match.FaceId);
                continue;
            }

            var tag = new MediaTag
            {
                MediaId = media.Id,
                StudentId = match.UserId,
                ConfidenceScore = (decimal)match.Confidence,
                IsVerified = false
            };
            await _unitOfWork.MediaTags.AddAsync(tag);
            newTags.Add(tag);
        }

        // Persist captured timelines onto every tag (new + existing) for this media.
        if (timelines != null)
        {
            foreach (var tag in media.MediaTags.Concat(newTags))
            {
                if (timelines.TryGetValue(tag.StudentId, out var timeline))
                {
                    tag.FaceSegmentsJson = JsonSerializer.Serialize(timeline.Segments);
                    tag.HasOtherFaces = timeline.HasOtherFaces;
                    _logger.LogInformation(
                        "DoProcessVideoTagsAsync: persisted timeline for StudentId={StudentId}, MediaId={MediaId}: " +
                        "{SegmentCount} face segment(s), HasOtherFaces={HasOtherFaces}",
                        tag.StudentId, media.Id, timeline.Segments.Count, timeline.HasOtherFaces);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.MediaTagsProcessed(media.UploaderId, media.Id));

        await TryAdvanceToProcessingCompleteAsync(media);

        _logger.LogInformation("DoProcessVideoTagsAsync completed. {Count} new tag(s) for MediaId: {MediaId}",
            newTags.Count, media.Id);
        return (true, newTags);
    }

    /// <summary>
    /// Keeps only face matches for students with <see cref="ClassEnrollmentStatus.Active"/>
    /// enrollment in <paramref name="classId"/>. Out-of-class matches are skipped so AI tags
    /// never attach students outside the media's class.
    /// </summary>
    private async Task<List<FaceMatchResult>> FilterMatchesToActiveClassStudentsAsync(
        Guid classId,
        IReadOnlyList<FaceMatchResult> matches)
    {
        if (matches.Count == 0)
            return [];

        var matchedUserIds = matches.Select(m => m.UserId).Distinct().ToList();
        var enrolledIds = (await _unitOfWork.ClassEnrollments.GetAllAsync(
                ce => ce.ClassId == classId
                      && matchedUserIds.Contains(ce.StudentId)
                      && ce.Status == ClassEnrollmentStatus.Active
                      && !ce.IsDeleted))
            .Select(ce => ce.StudentId)
            .ToHashSet();

        var filtered = new List<FaceMatchResult>(matches.Count);
        foreach (var match in matches)
        {
            if (enrolledIds.Contains(match.UserId))
            {
                filtered.Add(match);
                continue;
            }

            _logger.LogInformation(
                "Skipping face match for StudentId={StudentId}: not Active-enrolled in ClassId={ClassId} (FaceId={FaceId})",
                match.UserId, classId, match.FaceId);
        }

        return filtered;
    }

    /// <summary>
    /// Persists <see cref="MediaTag"/> rows from already-fetched Rekognition matches,
    /// limited to students Active-enrolled in <paramref name="classId"/>.
    /// Called for images after face pre-validation in <see cref="UploadMediaAsync"/>.
    /// May return an empty list when no in-class students matched (upload still succeeds).
    /// </summary>
    private async Task<List<MediaTag>> SaveFaceTagsAsync(
        Guid mediaId,
        Guid classId,
        List<FaceMatchResult> matches)
    {
        var tags = new List<MediaTag>();
        var inClassMatches = await FilterMatchesToActiveClassStudentsAsync(classId, matches);

        // Batch-load all matched students in one query to avoid N+1 DB round-trips.
        var matchedUserIds = inClassMatches.Select(m => m.UserId).Distinct().ToList();
        var studentMap = matchedUserIds.Count > 0
            ? (await _unitOfWork.Users.GetAllAsync(u => matchedUserIds.Contains(u.Id)))
              .ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();

        foreach (var match in inClassMatches)
        {
            if (!studentMap.TryGetValue(match.UserId, out var student))
            {
                _logger.LogWarning("Skipping face match for non-existent UserId: {UserId} (FaceId: {FaceId})",
                    match.UserId, match.FaceId);
                continue;
            }

            var tag = new MediaTag
            {
                MediaId = mediaId,
                StudentId = match.UserId,
                ConfidenceScore = (decimal)match.Confidence,
                IsVerified = false
            };
            await _unitOfWork.MediaTags.AddAsync(tag);
            tags.Add(tag);
        }

        _logger.LogInformation("Image face-tagged: {Count} match(es) for Media: {MediaId}", tags.Count, mediaId);
        await _unitOfWork.SaveChangesAsync();
        return tags;
    }

    private async Task<MediaAssetDto> MapToDto(MediaAsset media, ICollection<MediaTag>? tags = null)
    {
        var tagSource = (tags ?? media.MediaTags).Where(t => !t.IsDeleted).ToList();
        var studentIds = tagSource.Select(t => t.StudentId).Distinct().ToList();
        var studentMap = studentIds.Count > 0
            ? (await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id))).ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();

        return MapAssetToDto(media, tagSource, studentMap);
    }

    private static MediaAssetDto MapAssetToDto(
        MediaAsset media,
        IEnumerable<MediaTag> tags,
        IReadOnlyDictionary<Guid, User> studentMap)
    {
        var isVideo = string.Equals(media.FileType, "video", StringComparison.OrdinalIgnoreCase);
        var isReady = !isVideo || media.VideoStatus == VideoProcessingStatus.TaggingComplete;

        return new MediaAssetDto
        {
            Id = media.Id,
            UploaderId = media.UploaderId,
            ClassId = media.ClassId,
            ClassSessionId = media.ClassSessionId,
            FileUrl = media.FileUrl,
            FileType = media.FileType,
            VideoStatus = media.VideoStatus,
            StatusLabel = GetVideoStatusLabel(media.FileType, media.VideoStatus),
            IsReady = isReady,
            UploadedAt = media.UploadedAt,
            LabelTimeline = ParseLabelTimeline(media.LabelTimelineJson),
            Tags = tags.Select(t => MapTagToDto(t, studentMap)).ToList()
        };
    }

    private static List<MediaAsset> ApplyGalleryFilters(
        List<MediaAsset> mediaList,
        Guid? classSessionId,
        string? fileType,
        VideoProcessingStatus? videoStatus,
        string? sortBy,
        bool isDescending)
    {
        if (classSessionId.HasValue)
            mediaList = mediaList.Where(m => m.ClassSessionId == classSessionId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            mediaList = mediaList
                .Where(m => string.Equals(m.FileType, fileType.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (videoStatus.HasValue)
            mediaList = mediaList.Where(m => m.VideoStatus == videoStatus.Value).ToList();

        return SortMediaList(mediaList, sortBy, isDescending);
    }

    private async Task<List<ClassGalleryMediaDto>> MapGalleryDtosAsync(List<MediaAsset> pageItems)
    {
        if (pageItems.Count == 0)
            return [];

        var classIds = pageItems.Select(m => m.ClassId).Distinct().ToList();
        var classes = (await _unitOfWork.Classes.GetAllAsync(
                c => classIds.Contains(c.Id) && !c.IsDeleted))
            .ToDictionary(c => c.Id);

        var programIds = classes.Values.Select(c => c.ProgramId).Distinct().ToList();
        var programs = programIds.Count == 0
            ? new Dictionary<Guid, Program>()
            : (await _unitOfWork.Programs.GetAllAsync(
                    p => programIds.Contains(p.Id) && !p.IsDeleted))
                .ToDictionary(p => p.Id);

        return pageItems.Select(media =>
        {
            classes.TryGetValue(media.ClassId, out var classEntity);
            programs.TryGetValue(classEntity?.ProgramId ?? Guid.Empty, out var program);
            return MapToGalleryDto(media, classEntity?.Name, classEntity?.ProgramId, program?.Name);
        }).ToList();
    }

    private static ClassGalleryMediaDto MapToGalleryDto(
        MediaAsset media,
        string? className = null,
        Guid? programId = null,
        string? programName = null)
    {
        var isVideo = string.Equals(media.FileType, "video", StringComparison.OrdinalIgnoreCase);
        var isReady = !isVideo || media.VideoStatus == VideoProcessingStatus.TaggingComplete;

        return new ClassGalleryMediaDto
        {
            Id = media.Id,
            UploaderId = media.UploaderId,
            ClassId = media.ClassId,
            ClassName = className,
            ProgramId = programId,
            ProgramName = programName,
            ClassSessionId = media.ClassSessionId,
            FileUrl = media.FileUrl,
            FileType = media.FileType,
            VideoStatus = media.VideoStatus,
            StatusLabel = GetVideoStatusLabel(media.FileType, media.VideoStatus),
            IsReady = isReady,
            UploadedAt = media.UploadedAt,
        };
    }

    private static MediaTagDto MapTagToDto(MediaTag tag, IReadOnlyDictionary<Guid, User> studentMap)
    {
        studentMap.TryGetValue(tag.StudentId, out var student);
        return new MediaTagDto
        {
            Id = tag.Id,
            StudentId = tag.StudentId,
            StudentName = student?.FullName,
            ConfidenceScore = tag.ConfidenceScore,
            IsVerified = tag.IsVerified,
            HasOtherFaces = tag.HasOtherFaces,
            FaceSegments = ParseFaceSegments(tag.FaceSegmentsJson)
        };
    }

    private static string GetVideoStatusLabel(string? fileType, VideoProcessingStatus status)
    {
        if (!string.Equals(fileType, "video", StringComparison.OrdinalIgnoreCase))
            return "Ready";

        return status switch
        {
            VideoProcessingStatus.None => "Ready",
            VideoProcessingStatus.Transcoding => "Transcoding",
            VideoProcessingStatus.PendingTagging => "Tagging faces",
            VideoProcessingStatus.TaggingComplete => "Ready",
            VideoProcessingStatus.Failed => "Failed",
            _ => status.ToString()
        };
    }

    private static List<LabelTimelineEntryDto> ParseLabelTimeline(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<LabelTimelineEntryDto>();

        try
        {
            var entries = JsonSerializer.Deserialize<List<LabelDetectionEntry>>(json);
            if (entries == null || entries.Count == 0)
                return new List<LabelTimelineEntryDto>();

            return entries.Select(e => new LabelTimelineEntryDto
            {
                TimestampMs = e.TimestampMs,
                LabelName = e.LabelName,
                Confidence = e.Confidence
            }).ToList();
        }
        catch (JsonException)
        {
            return new List<LabelTimelineEntryDto>();
        }
    }

    private static List<FaceSegmentDto> ParseFaceSegments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<FaceSegmentDto>();

        try
        {
            var segments = JsonSerializer.Deserialize<List<FaceTimestampSegment>>(json);
            if (segments == null || segments.Count == 0)
                return new List<FaceSegmentDto>();

            return segments.Select(s => new FaceSegmentDto
            {
                StartMs = s.StartMs,
                EndMs = s.EndMs
            }).ToList();
        }
        catch (JsonException)
        {
            return new List<FaceSegmentDto>();
        }
    }
}
