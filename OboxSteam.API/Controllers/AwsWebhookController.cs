using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Interfaces;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace OboxSteam.API.Controllers;

/// <summary>
/// Receives AWS SNS notifications for activity media and personal highlight videos.
/// Verifies SNS signatures, confirms subscriptions, and routes:
/// MediaConvert / Transcribe (EventBridge) and Rekognition (SNS) events to
/// <see cref="IMediaService"/> and <see cref="IPersonalVideoService"/>.
/// No JWT — security relies on SNS signature verification.
/// </summary>
[ApiController]
[Route("api/webhooks/aws")]
public class AwsWebhookController : ControllerBase
{
    // SNS certificates are stable per URL — cache them to avoid redundant downloads.
    private static readonly ConcurrentDictionary<string, byte[]> _certCache = new();

    private readonly ILogger<AwsWebhookController> _logger;
    private readonly IMediaService _mediaService;
    private readonly IPersonalVideoService _personalVideoService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AwsWebhookController(
        ILogger<AwsWebhookController> logger,
        IMediaService mediaService,
        IPersonalVideoService personalVideoService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _mediaService = mediaService;
        _personalVideoService = personalVideoService;
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
                // EventBridge (Transcribe). Transcribe has no SNS NotificationChannel, so an
                // EventBridge rule routes "Transcribe Job State Change" events to this SNS topic.
                else if (msgRoot.TryGetProperty("detail-type", out var transcribeTypeProp) &&
                         transcribeTypeProp.GetString() == "Transcribe Job State Change")
                {
                    var detail = msgRoot.GetProperty("detail");
                    var jobName = detail.GetProperty("TranscriptionJobName").GetString();
                    var status = detail.GetProperty("TranscriptionJobStatus").GetString();

                    if (status == "COMPLETED" || status == "FAILED")
                    {
                        await ProcessTranscribeJobAsync(jobName!, status == "COMPLETED");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Transcribe JobName: {JobName} is in status: {Status}. No action required.",
                            jobName, status);
                    }
                }
                // Rekognition
                else if (msgRoot.TryGetProperty("JobId", out var rekJobIdProp))
                {
                    var jobId = rekJobIdProp.GetString();
                    var status = msgRoot.GetProperty("Status").GetString();
                    // "API" field distinguishes job types, e.g. "StartFaceSearch" vs "StartLabelDetection".
                    var api = msgRoot.TryGetProperty("API", out var apiProp) ? apiProp.GetString() : null;

                    if (status == "SUCCEEDED" || status == "FAILED")
                    {
                        await ProcessRekognitionJobAsync(jobId!, status == "SUCCEEDED", api);
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

            // Download the certificate (cached per URL — SNS certs are stable)
            var http = _httpClientFactory.CreateClient("sns");
            if (!_certCache.TryGetValue(certUrl!, out var certBytes))
            {
                certBytes = await http.GetByteArrayAsync(certUrl);
                _certCache.TryAdd(certUrl!, certBytes);
            }
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

        // Activity media and personal highlight videos both use MediaConvert but correlate
        // via different tables (MediaAssets vs HighlightVideoItems). Route by job match.
        var handledByMedia = await _mediaService.HandleMediaConvertWebhookAsync(jobId, isSuccess);
        if (!handledByMedia)
            await _personalVideoService.HandlePersonalVideoJobCompletionAsync(jobId, isSuccess);
    }

    private async Task ProcessTranscribeJobAsync(string jobName, bool isSuccess)
    {
        _logger.LogInformation(
            "Processing Transcribe Webhook JobName: {JobName}, Success: {Success}", jobName, isSuccess);

        await _mediaService.HandleTranscribeWebhookAsync(jobName, isSuccess);
    }

    private async Task ProcessRekognitionJobAsync(string jobId, bool isSuccess, string? api)
    {
        _logger.LogInformation(
            "Processing Rekognition Webhook JobId: {JobId}, API: {Api}, Success: {Success}",
            jobId, api ?? "unknown", isSuccess);

        if (string.Equals(api, "StartLabelDetection", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaService.HandleLabelDetectionWebhookAsync(jobId, isSuccess);
            return;
        }

        // Face Search job (default / legacy path)
        await _mediaService.HandleFaceSearchWebhookAsync(jobId, isSuccess);
    }
}
