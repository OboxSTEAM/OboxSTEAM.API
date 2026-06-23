using System.Globalization;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Speaker diarization via AWS Transcribe.
/// Workflow: video already in S3 → StartTranscriptionJob (ShowSpeakerLabels=true) →
/// EventBridge "Transcribe Job State Change" → SNS → webhook → read transcript JSON from S3.
/// Transcribe has no SNS NotificationChannel like Rekognition, so completion is delivered via
/// an EventBridge rule routed to the same SNS topic the webhook controller already consumes.
/// </summary>
public class TranscribeService : ITranscribeService
{
    private const string EnvS3Bucket = "AWS_S3_BUCKET";

    /// <summary>S3 prefix where Transcribe writes its output transcript JSON.</summary>
    private const string OutputPrefix = "transcribe";

    /// <summary>
    /// Upper bound for distinct speakers Transcribe will attempt to separate. A generous cap
    /// keeps small-group classroom videos accurate without splitting one person into many.
    /// </summary>
    private const int MaxSpeakerLabels = 10;

    // Content may mix Vietnamese and English, so let Transcribe pick per-job from these options.
    private static readonly List<string> LanguageOptions = new() { "vi-VN", "en-US" };

    private readonly IAmazonTranscribeService _transcribe;
    private readonly IAmazonS3 _s3;
    private readonly ILogger<TranscribeService> _logger;

    public TranscribeService(
        IAmazonTranscribeService transcribe,
        IAmazonS3 s3,
        ILogger<TranscribeService> logger)
    {
        _transcribe = transcribe;
        _s3 = s3;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> StartSpeakerDiarizationAsync(string s3Bucket, string s3Key, Guid mediaId)
    {
        // Job name must be unique per AWS account; include mediaId for traceability.
        var jobName = $"obox-{mediaId:N}-{Guid.NewGuid():N}".Substring(0, 60);
        var mediaFileUri = $"s3://{s3Bucket}/{s3Key}";

        _logger.LogInformation(
            "StartSpeakerDiarizationAsync: JobName={JobName}, Media={MediaUri}", jobName, mediaFileUri);

        var request = new StartTranscriptionJobRequest
        {
            TranscriptionJobName = jobName,
            Media = new Media { MediaFileUri = mediaFileUri },
            IdentifyLanguage = true,
            LanguageOptions = LanguageOptions,
            OutputBucketName = s3Bucket,
            OutputKey = $"{OutputPrefix}/{jobName}.json",
            Settings = new Settings
            {
                ShowSpeakerLabels = true,
                MaxSpeakerLabels = MaxSpeakerLabels
            }
        };

        var response = await _transcribe.StartTranscriptionJobAsync(request);

        _logger.LogInformation(
            "Transcribe job started. JobName={JobName}, Status={Status}",
            jobName, response.TranscriptionJob?.TranscriptionJobStatus);

        return jobName;
    }

    /// <inheritdoc />
    public async Task<List<SpeakerSegment>?> GetSpeakerSegmentsAsync(string jobName)
    {
        _logger.LogInformation("GetSpeakerSegmentsAsync: JobName={JobName}", jobName);

        var jobResponse = await _transcribe.GetTranscriptionJobAsync(
            new GetTranscriptionJobRequest { TranscriptionJobName = jobName });

        var status = jobResponse.TranscriptionJob?.TranscriptionJobStatus;

        if (status == TranscriptionJobStatus.QUEUED || status == TranscriptionJobStatus.IN_PROGRESS)
        {
            _logger.LogInformation("Transcribe job {JobName} still {Status}.", jobName, status);
            return null;
        }

        if (status == TranscriptionJobStatus.FAILED)
        {
            _logger.LogWarning(
                "Transcribe job {JobName} FAILED: {Reason}",
                jobName, jobResponse.TranscriptionJob?.FailureReason);
            return new List<SpeakerSegment>();
        }

        // status == COMPLETED — read the transcript JSON we asked Transcribe to write to S3.
        var bucket = RequireEnv(EnvS3Bucket);
        var outputKey = $"{OutputPrefix}/{jobName}.json";

        string json;
        try
        {
            using var obj = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = outputKey
            });
            using var reader = new StreamReader(obj.ResponseStream);
            json = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetSpeakerSegmentsAsync: failed to read transcript output s3://{Bucket}/{Key}",
                bucket, outputKey);
            return new List<SpeakerSegment>();
        }

        return ParseSpeakerSegments(json, jobName);
    }

    /// <summary>
    /// Parses the AWS Transcribe output JSON, extracting <c>results.speaker_labels.segments[]</c>.
    /// Each segment has string <c>start_time</c>/<c>end_time</c> (seconds) and a
    /// <c>speaker_label</c> (e.g. "spk_0").
    /// </summary>
    private List<SpeakerSegment> ParseSpeakerSegments(string json, string jobName)
    {
        var segments = new List<SpeakerSegment>();

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                !results.TryGetProperty("speaker_labels", out var speakerLabels) ||
                !speakerLabels.TryGetProperty("segments", out var segArray) ||
                segArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "GetSpeakerSegmentsAsync: no speaker_labels.segments in transcript for JobName={JobName}.", jobName);
                return segments;
            }

            foreach (var seg in segArray.EnumerateArray())
            {
                if (!seg.TryGetProperty("speaker_label", out var labelProp)) continue;
                if (!seg.TryGetProperty("start_time", out var startProp)) continue;
                if (!seg.TryGetProperty("end_time", out var endProp)) continue;

                var label = labelProp.GetString();
                if (string.IsNullOrEmpty(label)) continue;

                if (!TryParseSecondsToMs(startProp.GetString(), out var startMs)) continue;
                if (!TryParseSecondsToMs(endProp.GetString(), out var endMs)) continue;
                if (endMs < startMs) continue;

                segments.Add(new SpeakerSegment(label, startMs, endMs));
            }

            _logger.LogInformation(
                "GetSpeakerSegmentsAsync: parsed {Count} speaker segment(s) for JobName={JobName}.",
                segments.Count, jobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSpeakerSegmentsAsync: failed to parse transcript JSON for JobName={JobName}.", jobName);
        }

        return segments;
    }

    private static bool TryParseSecondsToMs(string? seconds, out long ms)
    {
        ms = 0;
        if (string.IsNullOrEmpty(seconds)) return false;
        if (!double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
            return false;
        ms = (long)Math.Round(s * 1000d);
        return true;
    }

    private static string RequireEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Required environment variable '{key}' is not set.");
}
