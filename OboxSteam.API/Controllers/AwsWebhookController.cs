using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public AwsWebhookController(
        ILogger<AwsWebhookController> logger,
        IUnitOfWork unitOfWork,
        IMediaService mediaService,
        IVideoConverterService videoConverterService,
        IBlobService blobService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _mediaService = mediaService;
        _videoConverterService = videoConverterService;
        _blobService = blobService;
        _httpClientFactory = httpClientFactory;
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
                return BadRequest("Missing Type property");

            var type = typeProp.GetString();

            // ── Security: Verify SNS message signature ────────────────────────
            // Reject any message whose signature does not verify against the
            // certificate AWS publishes at SigningCertURL.
            if (!await VerifySnsSignatureAsync(root, body))
            {
                _logger.LogWarning("SNS signature verification failed. Rejecting webhook.");
                return Unauthorized("Invalid SNS signature.");
            }

            if (type == "SubscriptionConfirmation")
            {
                var subscribeUrl = root.GetProperty("SubscribeURL").GetString();

                // ── Security: SSRF guard ─────────────────────────────────────
                // Only follow URLs that are HTTPS on official AWS domains.
                if (!IsAllowedSnsUrl(subscribeUrl))
                {
                    _logger.LogWarning(
                        "SubscriptionConfirmation URL blocked (non-AWS domain): {Url}", subscribeUrl);
                    return BadRequest("Invalid SubscribeURL domain.");
                }

                _logger.LogInformation("SubscriptionConfirmation received. Confirming URL: {Url}", subscribeUrl);
                var http = _httpClientFactory.CreateClient("sns");
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
                        _logger.LogInformation(
                            "MediaConvert JobId: {JobId} is in status: {Status}. No action required.",
                            jobId, status);
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

    // ── Security Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates that a SubscriptionConfirmation URL is HTTPS and on an
    /// official AWS SNS domain to prevent SSRF attacks.
    /// </summary>
    private static bool IsAllowedSnsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;

        // Allow only official AWS domains
        var host = uri.Host;
        return host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".aws.amazon.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the SNS message signature against the certificate published
    /// at <c>SigningCertURL</c>. Returns <c>true</c> if valid.
    /// See: https://docs.aws.amazon.com/sns/latest/dg/sns-verify-signature-of-message.html
    /// </summary>
    private async Task<bool> VerifySnsSignatureAsync(JsonElement root, string rawBody)
    {
        try
        {
            // All required fields must be present
            if (!root.TryGetProperty("SignatureVersion", out var sigVerProp) ||
                !root.TryGetProperty("Signature", out var sigProp) ||
                !root.TryGetProperty("SigningCertURL", out var certUrlProp))
            {
                _logger.LogWarning("SNS message missing signature fields.");
                return false;
            }

            var sigVersion = sigVerProp.GetString();
            if (sigVersion != "1")
            {
                _logger.LogWarning("Unsupported SNS signature version: {Version}", sigVersion);
                return false;
            }

            var certUrl = certUrlProp.GetString();

            // Guard: only fetch certificates from official SNS endpoints
            if (!IsAllowedSnsUrl(certUrl))
            {
                _logger.LogWarning("SigningCertURL is not an allowed AWS domain: {Url}", certUrl);
                return false;
            }

            // Download the certificate
            var http = _httpClientFactory.CreateClient("sns");
            var certBytes = await http.GetByteArrayAsync(certUrl);
            using var cert = new X509Certificate2(certBytes);

            // Build the string-to-sign per SNS specification
            var stringToSign = BuildSnsStringToSign(root);
            var signatureBytes = Convert.FromBase64String(sigProp.GetString()!);

            using var rsa = cert.GetRSAPublicKey()
                ?? throw new InvalidOperationException("Certificate has no RSA public key.");

            var isValid = rsa.VerifyData(
                Encoding.UTF8.GetBytes(stringToSign),
                signatureBytes,
                HashAlgorithmName.SHA1,
                RSASignaturePadding.Pkcs1);

            if (!isValid)
                _logger.LogWarning("SNS signature is invalid.");

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during SNS signature verification.");
            return false;
        }
    }

    /// <summary>
    /// Builds the canonical string-to-sign for an SNS message per AWS specification.
    /// Field order depends on message type (Notification vs SubscriptionConfirmation).
    /// </summary>
    private static string BuildSnsStringToSign(JsonElement root)
    {
        var sb = new StringBuilder();
        var type = root.GetProperty("Type").GetString();

        void Append(string key)
        {
            if (root.TryGetProperty(key, out var val))
            {
                sb.Append(key).Append('\n');
                sb.Append(val.GetString()).Append('\n');
            }
        }

        if (type == "Notification")
        {
            Append("Message");
            Append("MessageId");
            Append("Subject");
            Append("Timestamp");
            Append("TopicArn");
            Append("Type");
        }
        else // SubscriptionConfirmation / UnsubscribeConfirmation
        {
            Append("Message");
            Append("MessageId");
            Append("SubscribeURL");
            Append("Timestamp");
            Append("Token");
            Append("TopicArn");
            Append("Type");
        }

        return sb.ToString();
    }

    // ── Job Processors ────────────────────────────────────────────────────────

    private async Task ProcessMediaConvertJobAsync(string jobId, bool isSuccess)
    {
        _logger.LogInformation(
            "Processing MediaConvert Webhook JobId: {JobId}, Success: {Success}", jobId, isSuccess);

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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to resolve MediaConvert output for HighlightVideo {Id}", highlightVideo.Id);
                    highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Failed;
                }
            }
            else
            {
                highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Failed;
            }

            await _unitOfWork.SaveChangesAsync();
            return;
        }

        // 2. Check MediaAsset
        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.MediaConvertJobId == jobId && !m.IsDeleted);

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
        _logger.LogInformation(
            "Processing Rekognition Webhook JobId: {JobId}, Success: {Success}", jobId, isSuccess);

        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.FaceSearchJobId == jobId && !m.IsDeleted);

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
