using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;
using System.Text.Json;

namespace OboxSteam.API.Controllers;

[ApiController]
[Route("api/webhooks/aws")]
public class AwsWebhookController : ControllerBase
{
    private readonly ILogger<AwsWebhookController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediaService _mediaService;
    private readonly IVideoConverterService _videoConverterService;
    private readonly IBlobService _blobService;

    public AwsWebhookController(
        ILogger<AwsWebhookController> logger,
        IUnitOfWork unitOfWork,
        IMediaService mediaService,
        IVideoConverterService videoConverterService,
        IBlobService blobService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _mediaService = mediaService;
        _videoConverterService = videoConverterService;
        _blobService = blobService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleAwsNotification()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                _logger.LogWarning("Received AWS Webhook but the body is empty.");
                return BadRequest("Empty body");
            }

            _logger.LogInformation("Received AWS Webhook payload: {Body}", body);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Type", out var typeProp))
            {
                return BadRequest("Missing Type property");
            }

            var type = typeProp.GetString();

            if (type == "SubscriptionConfirmation")
            {
                var subscribeUrl = root.GetProperty("SubscribeURL").GetString();
                _logger.LogInformation("SubscriptionConfirmation received. URL: {Url}", subscribeUrl);

                using var http = new HttpClient();
                await http.GetAsync(subscribeUrl);
                return Ok();
            }

            if (type == "Notification")
            {
                var message = root.GetProperty("Message").GetString() ?? "";

                // Parse the inner message
                using var msgDoc = JsonDocument.Parse(message);
                var msgRoot = msgDoc.RootElement;

                // EventBridge (MediaConvert)
                if (msgRoot.TryGetProperty("detail-type", out var detailTypeProp) &&
                    detailTypeProp.GetString() == "MediaConvert Job State Change")
                {
                    var detail = msgRoot.GetProperty("detail");
                    var jobId = detail.GetProperty("jobId").GetString();
                    var status = detail.GetProperty("status").GetString();

                    if (status == "COMPLETE" || status == "ERROR")
                    {
                        await ProcessMediaConvertJobAsync(jobId!, status == "COMPLETE");
                    }
                    else
                    {
                        _logger.LogInformation("MediaConvert JobId: {JobId} is in status: {Status}. No action required.", jobId, status);
                    }
                }
                // Rekognition
                else if (msgRoot.TryGetProperty("JobId", out var rekJobIdProp))
                {
                    var jobId = rekJobIdProp.GetString();
                    var status = msgRoot.GetProperty("Status").GetString();

                    if (status == "SUCCEEDED" || status == "FAILED")
                    {
                        await ProcessRekognitionJobAsync(jobId!, status == "SUCCEEDED");
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AWS webhook.");
            return StatusCode(500);
        }
    }

    private async Task ProcessMediaConvertJobAsync(string jobId, bool isSuccess)
    {
        _logger.LogInformation("Processing MediaConvert Webhook JobId: {JobId}, Success: {Success}", jobId, isSuccess);

        // 1. Check HighlightVideo (PersonalVideoJobRef)
        var highlightVideo = await _unitOfWork.HighlightVideos.FirstOrDefaultAsync(
            h => h.PersonalVideoJobRef == jobId && !h.IsDeleted);

        if (highlightVideo != null)
        {
            if (isSuccess)
            {
                try
                {
                    var outputS3Key = await _videoConverterService.GetOutputS3KeyAsync(jobId);
                    var videoUrl = await _blobService.GetPreviewUrlAsync(outputS3Key);

                    highlightVideo.VideoUrl = videoUrl;
                    highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Completed;
                    highlightVideo.Status = "Completed";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resolve MediaConvert output for HighlightVideo {Id}", highlightVideo.Id);
                    highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Failed;
                    highlightVideo.Status = "Failed";
                }
            }
            else
            {
                highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Failed;
                highlightVideo.Status = "Failed";
            }

            await _unitOfWork.SaveChangesAsync();
            return;
        }

        // 2. Check MediaAsset (VideoJobRef == "mc:{jobId}")
        var mcRef = $"mc:{jobId}";
        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.VideoJobRef == mcRef && !m.IsDeleted);

        if (mediaAsset != null)
        {
            if (isSuccess)
            {
                try
                {
                    await _mediaService.TryCompleteTranscodeAsync(mediaAsset.Id);
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
        }
    }

    private async Task ProcessRekognitionJobAsync(string jobId, bool isSuccess)
    {
        _logger.LogInformation("Processing Rekognition Webhook JobId: {JobId}, Success: {Success}", jobId, isSuccess);

        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.VideoJobRef == jobId && !m.IsDeleted);

        if (mediaAsset != null)
        {
            if (isSuccess)
            {
                try
                {
                    await _mediaService.TryProcessVideoTagsAsync(mediaAsset.Id);
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
    }
}
