using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/media")]
[ApiController]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public MediaController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    /// <summary>
    /// Upload an image or video to a class. Auto face-tagging is applied.
    /// Images are tagged synchronously. Videos upload raw to S3, submit MediaConvert in this
    /// request, then AWS webhooks drive transcode and face search. Poll
    /// POST /api/media/{mediaId}/process-tags if tags are not ready yet.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(3L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3L * 1024 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload media to class",
        Description = "Uploads an image (.jpg, .jpeg, .png) or video (.mp4, .mov) for a class. " +
                      "classId is required; classSessionId is optional and must belong to that class. " +
                      "Images are auto face-tagged immediately. Videos: raw upload + MediaConvert submit " +
                      "in one request; AWS SNS webhooks complete transcode and start Rekognition. " +
                      "Call POST /api/media/{mediaId}/process-tags to poll face tags if webhooks are delayed."
    )]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadMedia(
        IFormFile file,
        [FromQuery] Guid classId,
        [FromQuery] Guid? classSessionId = null)
    {
        var result = await _mediaService.UploadMediaAsync(file, classId, classSessionId);

        if (string.Equals(result.FileType, "video", StringComparison.OrdinalIgnoreCase))
        {
            return Accepted(ApiResult<MediaAssetDto>.Success(
                result,
                "202",
                "Video uploaded; processing started."));
        }

        return StatusCode(StatusCodes.Status201Created, ApiResult<MediaAssetDto>.Success(
            result,
            "201",
            "Media uploaded."));
    }

    /// <summary>
    /// Get media filtered by class and/or student (role-scoped), with pagination and status filters.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get media by class and/or student",
        Description = "Returns paginated media scoped by role. " +
                      "Manager/Admin/Mentor see all video statuses by default (including Transcoding / PendingTagging). " +
                      "Filter with videoStatus=TaggingComplete for ready-only. " +
                      "Student always sees own tagged ready media; Parent requires linked studentId."
    )]
    [ProducesResponseType(typeof(ApiResult<Pagination<MediaAssetDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMedia(
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] Guid? classSessionId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by file type: image or video")] string? fileType = null,
        [FromQuery, SwaggerParameter(Description = "Filter by video status (e.g. TaggingComplete)")] VideoProcessingStatus? videoStatus = null,
        [FromQuery, SwaggerParameter(Description = "Sort by: uploadedAt, createdAt, fileType, videoStatus")] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _mediaService.GetMediaAsync(
            classId, studentId, classSessionId, fileType, videoStatus, sortBy, isDescending, page, pageSize);
        return Ok(ApiResult<Pagination<MediaAssetDto>>.Success(result, "200", "Media retrieved."));
    }

    /// <summary>
    /// Student class gallery: all media for a class (no face tags).
    /// Research submission evidence is excluded.
    /// </summary>
    [HttpGet("class/{classId:guid}/gallery")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Get class gallery (student)",
        Description = "Returns paginated media for a class without face tags. " +
                      "Student must be Active-enrolled in the class. " +
                      "Includes all video statuses (Transcoding, PendingTagging, TaggingComplete, Failed). " +
                      "Excludes research submission evidence (SubmissionEvidence-linked media). " +
                      "Supports the same filters/sort/pagination as GET /api/media."
    )]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassGalleryMediaDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassGallery(
        [FromRoute] Guid classId,
        [FromQuery] Guid? classSessionId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by file type: image or video")] string? fileType = null,
        [FromQuery, SwaggerParameter(Description = "Filter by video status (e.g. TaggingComplete)")] VideoProcessingStatus? videoStatus = null,
        [FromQuery, SwaggerParameter(Description = "Sort by: uploadedAt, createdAt, fileType, videoStatus")] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _mediaService.GetClassGalleryAsync(
            classId, classSessionId, fileType, videoStatus, sortBy, isDescending, page, pageSize);
        return Ok(ApiResult<Pagination<ClassGalleryMediaDto>>.Success(result, "200", "Class gallery retrieved."));
    }

    /// <summary>
    /// Student gallery across all Active-enrolled classes (portfolio media picker).
    /// </summary>
    [HttpGet("my-gallery")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Get my enrollment gallery (student)",
        Description = "Returns paginated class media from every class the student is Active-enrolled in. " +
                      "Filter with programId and/or classId. Same filters/sort/pagination as class gallery. " +
                      "Excludes research submission evidence (SubmissionEvidence-linked media). " +
                      "Use POST /api/portfolios/me/media/from-class-gallery to copy selected items into the portfolio."
    )]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassGalleryMediaDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMyGallery(
        [FromQuery, SwaggerParameter(Description = "Filter by program")] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by enrolled class")] Guid? classId = null,
        [FromQuery] Guid? classSessionId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by file type: image or video")] string? fileType = null,
        [FromQuery, SwaggerParameter(Description = "Filter by video status (e.g. TaggingComplete)")] VideoProcessingStatus? videoStatus = null,
        [FromQuery, SwaggerParameter(Description = "Sort by: uploadedAt, createdAt, fileType, videoStatus")] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _mediaService.GetMyGalleryAsync(
            programId, classId, classSessionId, fileType, videoStatus, sortBy, isDescending, page, pageSize);
        return Ok(ApiResult<Pagination<ClassGalleryMediaDto>>.Success(result, "200", "Enrollment gallery retrieved."));
    }

    /// <summary>
    /// Get one media asset by id (role-scoped).
    /// </summary>
    [HttpGet("{mediaId:guid}")]
    [SwaggerOperation(
        Summary = "Get media by id",
        Description = "Returns one media asset including face tags. Access is role-scoped: " +
                      "Manager/Admin any media; Mentor only mentored classes; " +
                      "Student only ready media they are tagged in; Parent only ready media tagged for linked students."
    )]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMediaById([FromRoute] Guid mediaId)
    {
        var result = await _mediaService.GetMediaByIdAsync(mediaId);
        return Ok(ApiResult<MediaAssetDto>.Success(result, "200", "Media retrieved."));
    }

    /// <summary>
    /// Poll video processing progress (MediaConvert percent while transcoding).
    /// </summary>
    [HttpGet("{mediaId:guid}/progress")]
    [SwaggerOperation(
        Summary = "Get media processing progress",
        Description = "While VideoStatus is Transcoding, percentComplete is MediaConvert JobPercentComplete (0–100). " +
                      "While PendingTagging, percentComplete is null — poll until TaggingComplete or Failed. " +
                      "Also usable via GET /api/media/{mediaId} for status-only checks."
    )]
    [ProducesResponseType(typeof(ApiResult<MediaProcessingProgressDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetProcessingProgress([FromRoute] Guid mediaId)
    {
        var result = await _mediaService.GetProcessingProgressAsync(mediaId);
        return Ok(ApiResult<MediaProcessingProgressDto>.Success(result, "200", "Processing progress retrieved."));
    }

    /// <summary>
    /// Get all media for a class session (including face tags), scoped by role.
    /// </summary>
    [HttpGet("class-session/{classSessionId:guid}")]
    [SwaggerOperation(
        Summary = "Get media by class session",
        Description = "Retrieves media assets for a specific class session, including face recognition tags. " +
                      "Students and parents only receive ready media they (or linked students) are tagged in."
    )]
    [ProducesResponseType(typeof(ApiResult<List<MediaAssetDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMediaByClassSession([FromRoute] Guid classSessionId)
    {
        var result = await _mediaService.GetMediaByClassSessionAsync(classSessionId);
        return Ok(ApiResult<List<MediaAssetDto>>.Success(result, "200", "Media retrieved."));
    }

    /// <summary>
    /// Manually add a verified student tag (mentor/manager).
    /// </summary>
    [HttpPost("{mediaId:guid}/tags")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Add media tag",
        Description = "Manually tags a student onto ready media. Creates a verified tag. " +
                      "Mentors may only tag students enrolled in the media's class."
    )]
    [ProducesResponseType(typeof(ApiResult<MediaTagDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddMediaTag(
        [FromRoute] Guid mediaId,
        [FromBody] AddMediaTagRequest request)
    {
        var result = await _mediaService.AddMediaTagAsync(mediaId, request.StudentId);
        return StatusCode(StatusCodes.Status201Created, ApiResult<MediaTagDto>.Success(
            result, "201", "Media tag added."));
    }

    /// <summary>
    /// Verify or reject an AI/manual media tag.
    /// </summary>
    [HttpPatch("{mediaId:guid}/tags/{studentId:guid}")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Set media tag verification",
        Description = "Sets IsVerified for mentor review of face tags."
    )]
    [ProducesResponseType(typeof(ApiResult<MediaTagDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> SetMediaTagVerification(
        [FromRoute] Guid mediaId,
        [FromRoute] Guid studentId,
        [FromBody] UpdateMediaTagVerificationRequest request)
    {
        var result = await _mediaService.SetMediaTagVerificationAsync(mediaId, studentId, request.IsVerified);
        return Ok(ApiResult<MediaTagDto>.Success(result, "200", "Media tag verification updated."));
    }

    /// <summary>
    /// Remove a student tag from media.
    /// </summary>
    [HttpDelete("{mediaId:guid}/tags/{studentId:guid}")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Remove media tag",
        Description = "Soft-deletes a media tag for the given student."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> RemoveMediaTag(
        [FromRoute] Guid mediaId,
        [FromRoute] Guid studentId)
    {
        await _mediaService.RemoveMediaTagAsync(mediaId, studentId);
        return Ok(ApiResult.Success("200", "Media tag removed."));
    }

    /// <summary>
    /// Restart Rekognition face search on the transcoded video and persist MediaTags when ready.
    /// </summary>
    [HttpPost("{mediaId:guid}/process-tags")]
    [SwaggerOperation(
        Summary = "Process video face tags",
        Description = "Submits a new Rekognition face-search job on the transcoded video output, " +
                      "restarts label detection, then polls once for results. " +
                      "Returns 202 while VideoStatus is PendingTagging; call again until TaggingComplete."
    )]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> ProcessVideoTags([FromRoute] Guid mediaId)
    {
        var result = await _mediaService.ProcessVideoTagsAsync(mediaId);

        if (result.VideoStatus == VideoProcessingStatus.PendingTagging)
        {
            return Accepted(ApiResult<MediaAssetDto>.Success(
                result,
                "202",
                "Face tagging in progress. Call again when processing completes."));
        }

        return Ok(ApiResult<MediaAssetDto>.Success(result, "200", "Video tags processed."));
    }

    /// <summary>
    /// Delete a media asset (soft delete + removes file from S3).
    /// </summary>
    [HttpDelete("{mediaId:guid}")]
    [SwaggerOperation(
        Summary = "Delete media",
        Description = "Soft-deletes a media asset and removes the file from S3 storage."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteMedia([FromRoute] Guid mediaId)
    {
        await _mediaService.DeleteMediaAsync(mediaId);
        return Ok(ApiResult.Success("200", "Media deleted."));
    }
}
