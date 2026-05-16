using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers
{
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
        /// Images are tagged synchronously; videos start an async Rekognition job.
        /// </summary>
        [HttpPost("upload")]
        [SwaggerOperation(
            Summary = "Upload media to activity",
            Description = "Uploads an image (.jpg, .jpeg, .png) or video (.mp4, .mov) to an activity. " +
                          "Images are auto face-tagged immediately. Videos start an async Rekognition job — " +
                          "call POST /api/media/{mediaId}/process-tags later to retrieve results."
        )]
        [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> UploadMedia(IFormFile file, [FromQuery] Guid activityId)
        {
            var result = await _mediaService.UploadMediaAsync(file, activityId);
            return Ok(ApiResult<MediaAssetDto>.Success(result, "200", "Media uploaded successfully."));
        }

        /// <summary>
        /// Get all media for an activity (including face tags).
        /// </summary>
        [HttpGet("activity/{activityId:guid}")]
        [SwaggerOperation(
            Summary = "Get media by activity",
            Description = "Retrieves all media assets for a specific activity, including face recognition tags."
        )]
        [ProducesResponseType(typeof(ApiResult<List<MediaAssetDto>>), 200)]
        public async Task<IActionResult> GetMediaByActivity([FromRoute] Guid activityId)
        {
            var result = await _mediaService.GetMediaByActivityAsync(activityId);
            return Ok(ApiResult<List<MediaAssetDto>>.Success(result, "200", "Media retrieved successfully."));
        }

        /// <summary>
        /// Process face tags for a video that has completed Rekognition analysis.
        /// Call this after uploading a video to retrieve face recognition results.
        /// </summary>
        [HttpPost("{mediaId:guid}/process-tags")]
        [SwaggerOperation(
            Summary = "Process video face tags",
            Description = "Polls the Rekognition Video job for a previously uploaded video and creates MediaTag records " +
                          "for each recognized face. Returns 400 if the job is still in progress."
        )]
        [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> ProcessVideoTags([FromRoute] Guid mediaId)
        {
            var result = await _mediaService.ProcessVideoTagsAsync(mediaId);
            return Ok(ApiResult<MediaAssetDto>.Success(result, "200", "Video face tags processed successfully."));
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
            return Ok(ApiResult.Success("200", "Media deleted successfully."));
        }
    }
}
