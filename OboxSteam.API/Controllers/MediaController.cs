using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    /// Upload an image or video to an activity. Auto face-tagging is applied.
    /// Images are tagged synchronously. Videos upload raw to S3, submit MediaConvert in this
    /// request, then AWS webhooks drive transcode and face search. Poll
    /// POST /api/media/{mediaId}/process-tags if tags are not ready yet.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(3L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3L * 1024 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload media to activity",
        Description = "Uploads an image (.jpg, .jpeg, .png) or video (.mp4, .mov) to an activity. " +
                      "Images are auto face-tagged immediately. Videos: raw upload + MediaConvert submit " +
                      "in one request; AWS SNS webhooks complete transcode and start Rekognition. " +
                      "Call POST /api/media/{mediaId}/process-tags to poll face tags if webhooks are delayed."
    )]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadMedia(IFormFile file, [FromQuery] Guid activityId)
    {
        var result = await _mediaService.UploadMediaAsync(file, activityId);

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
    /// Get ready media filtered by class and/or student (role-scoped).
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get media by class and/or student",
        Description = "Returns ready media (images, or videos with TaggingComplete), scoped by role. " +
                      "Manager/SuperAdmin may omit filters to list all ready media. " +
                      "Mentor is limited to mentored class activities. " +
                      "Student always sees own tags; Parent requires linked studentId."
    )]
    [ProducesResponseType(typeof(ApiResult<List<MediaAssetDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMedia(
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? studentId = null)
    {
        var result = await _mediaService.GetMediaAsync(classId, studentId);
        return Ok(ApiResult<List<MediaAssetDto>>.Success(result, "200", "Media retrieved."));
    }

    /// <summary>
    /// Get one media asset by id (role-scoped).
    /// </summary>
    [HttpGet("{mediaId:guid}")]
    [SwaggerOperation(
        Summary = "Get media by id",
        Description = "Returns one media asset including face tags. Access is role-scoped: " +
                      "Manager/SuperAdmin any media; Mentor only class-scheduled activities; " +
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
    /// Get all media for an activity (including face tags), scoped by role.
    /// </summary>
    [HttpGet("activity/{activityId:guid}")]
    [SwaggerOperation(
        Summary = "Get media by activity",
        Description = "Retrieves media assets for a specific activity, including face recognition tags. " +
                      "Students and parents only receive ready media they (or linked students) are tagged in."
    )]
    [ProducesResponseType(typeof(ApiResult<List<MediaAssetDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMediaByActivity([FromRoute] Guid activityId)
    {
        var result = await _mediaService.GetMediaByActivityAsync(activityId);
        return Ok(ApiResult<List<MediaAssetDto>>.Success(result, "200", "Media retrieved."));
    }

    /// <summary>
    /// Manually add a verified student tag (mentor/manager).
    /// </summary>
    [HttpPost("{mediaId:guid}/tags")]
    [Authorize(Roles = "Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Add media tag",
        Description = "Manually tags a student onto ready media. Creates a verified tag. " +
                      "Mentors may only tag students enrolled in their class that schedules the media activity."
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
    [Authorize(Roles = "Mentor,Manager,SuperAdmin")]
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
    [Authorize(Roles = "Mentor,Manager,SuperAdmin")]
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
