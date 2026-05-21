using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Transcodes video using AWS MediaConvert (fully managed cloud service).
/// Workflow: raw video already in S3 → submit MediaConvert job → poll until complete.
/// No local FFmpeg or /tmp files required.
/// </summary>
public class VideoConverterService : IVideoConverterService
{
    private const string EnvS3Bucket  = "AWS_S3_BUCKET";
    private const string EnvRoleArn   = "AWS_MEDIACONVERT_ROLE_ARN";

    private readonly IAmazonMediaConvert _mediaConvert;
    private readonly ILogger<VideoConverterService> _logger;

    public VideoConverterService(
        IAmazonMediaConvert mediaConvert,
        ILogger<VideoConverterService> logger)
    {
        _mediaConvert = mediaConvert;
        _logger       = logger;
    }

    /// <inheritdoc />
    public async Task<string> SubmitTranscodeJobAsync(string inputS3Key, string outputDestinationPrefix)
    {
        var bucket  = RequireEnv(EnvS3Bucket);
        var roleArn = RequireEnv(EnvRoleArn);

        // Ensure prefix ends with /
        if (!outputDestinationPrefix.EndsWith('/'))
            outputDestinationPrefix += '/';

        var inputUri       = $"s3://{bucket}/{inputS3Key}";
        var outputGroupUri = $"s3://{bucket}/{outputDestinationPrefix}";

        _logger.LogInformation(
            "Submitting MediaConvert job: {Input} → {Output}",
            inputUri, outputGroupUri);

        var request = new CreateJobRequest
        {
            Role     = roleArn,
            Settings = new JobSettings
            {
                Inputs = new List<Input>
                {
                    new Input
                    {
                        FileInput      = inputUri,
                        TimecodeSource = InputTimecodeSource.ZEROBASED,
                        AudioSelectors = new Dictionary<string, AudioSelector>
                        {
                            ["Audio Selector 1"] = new AudioSelector
                            {
                                DefaultSelection = AudioDefaultSelection.DEFAULT
                            }
                        }
                    }
                },
                OutputGroups = new List<OutputGroup>
                {
                    new OutputGroup
                    {
                        Name                = "MP4 Output",
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            Type              = OutputGroupType.FILE_GROUP_SETTINGS,
                            FileGroupSettings = new FileGroupSettings
                            {
                                Destination = outputGroupUri
                            }
                        },
                        Outputs = new List<Output>
                        {
                            new Output
                            {
                                // "_conv" suffix keeps the base name readable and avoids
                                // space-encoded (%20) characters in the output S3 key/URL.
                                NameModifier      = "_conv",
                                VideoDescription  = new VideoDescription
                                {
                                    CodecSettings = new VideoCodecSettings
                                    {
                                        Codec       = VideoCodec.H_264,
                                        H264Settings = new H264Settings
                                        {
                                            RateControlMode = H264RateControlMode.QVBR,
                                            MaxBitrate      = 5_000_000, // 5 Mbps — required for QVBR
                                            QvbrSettings    = new H264QvbrSettings
                                            {
                                                QvbrQualityLevel = 7   // ~CRF 23 equivalent
                                            }
                                            // Note: do NOT set FlickerAdaptiveQuantization,
                                            // SpatialAdaptiveQuantization, or TemporalAdaptiveQuantization
                                            // when H264AdaptiveQuantization is AUTO (the default).
                                            // MediaConvert handles these automatically.
                                        }
                                    }
                                },
                                AudioDescriptions = new List<AudioDescription>
                                {
                                    new AudioDescription
                                    {
                                        AudioSourceName = "Audio Selector 1",
                                        CodecSettings   = new AudioCodecSettings
                                        {
                                            Codec       = AudioCodec.AAC,
                                            AacSettings = new AacSettings
                                            {
                                                Bitrate     = 96000,
                                                CodingMode  = AacCodingMode.CODING_MODE_2_0,
                                                SampleRate  = 48000
                                            }
                                        }
                                    }
                                },
                                ContainerSettings = new ContainerSettings
                                {
                                    Container  = ContainerType.MP4,
                                    Mp4Settings = new Mp4Settings
                                    {
                                        // Move moov atom to front for progressive download / streaming
                                        MoovPlacement = Mp4MoovPlacement.PROGRESSIVE_DOWNLOAD
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var response = await _mediaConvert.CreateJobAsync(request);
        var jobId    = response.Job.Id;

        _logger.LogInformation("MediaConvert job submitted. JobId: {JobId}", jobId);
        return jobId;
    }

    /// <inheritdoc />
    public async Task<MediaConvertJobStatus> GetJobStatusAsync(string jobId)
    {
        var response = await _mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId });

        var status = response.Job.Status;
        _logger.LogDebug("MediaConvert job {JobId} status: {Status}", jobId, status);

        if (status == JobStatus.COMPLETE)
            return MediaConvertJobStatus.Complete;
        if (status == JobStatus.ERROR || status == JobStatus.CANCELED)
            return MediaConvertJobStatus.Error;
        // SUBMITTED, PROGRESSING, etc.
        return MediaConvertJobStatus.InProgress;
    }

    /// <inheritdoc />
    public async Task<string> GetOutputS3KeyAsync(string jobId)
    {
        var bucket   = RequireEnv(EnvS3Bucket);
        var response = await _mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId });
        var job      = response.Job;

        // Reconstruct the output S3 key from the job's own settings.
        // MediaConvert naming: {Destination}{baseName}{NameModifier}.{ext}
        // We set:
        //   Destination = "s3://bucket/media/"
        //   NameModifier = " " (single space, minimal valid value)
        //   Container = MP4 → extension = ".mp4"
        //   Input = "s3://bucket/raw/{baseName}.{origExt}"
        //
        // Therefore output = "s3://bucket/media/{baseName} .mp4"
        // After trimming the space from NameModifier the actual file produced is
        // "{baseName} .mp4" — we need to replicate this precisely.

        var inputUri = job.Settings.Inputs.First().FileInput;
        // inputUri = "s3://bucket/raw/activityId_timestamp.mov"
        var inputFileName    = inputUri.Split('/').Last();               // "activityId_timestamp.mov"
        var inputBaseName    = Path.GetFileNameWithoutExtension(inputFileName); // "activityId_timestamp"

        // Get destination from the first output group (FileGroupSettings)
        var destUri = job.Settings.OutputGroups
            .First().OutputGroupSettings.FileGroupSettings.Destination;
        // destUri = "s3://bucket/media/"

        // NameModifier was set to " " (single space)
        var nameModifier = job.Settings.OutputGroups
            .First().Outputs.First().NameModifier ?? string.Empty;

        // Full output URI = destUri + baseName + nameModifier + ".mp4"
        var outputUri = $"{destUri}{inputBaseName}{nameModifier}.mp4";

        // Strip "s3://{bucket}/" prefix → bucket-relative key
        var s3Prefix = $"s3://{bucket}/";
        if (!outputUri.StartsWith(s3Prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Unexpected output URI format for job {jobId}: '{outputUri}'");

        var s3Key = outputUri[s3Prefix.Length..];
        _logger.LogInformation(
            "Resolved output S3 key for job {JobId}: {S3Key}", jobId, s3Key);
        return s3Key;
    }

    /// <inheritdoc />
    public async Task<string> GetInputS3KeyAsync(string jobId)
    {
        var bucket   = RequireEnv(EnvS3Bucket);
        var response = await _mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId });

        // inputUri = "s3://bucket/raw/activityId_timestamp.mov"
        var inputUri  = response.Job.Settings.Inputs.First().FileInput;
        var s3Prefix  = $"s3://{bucket}/";

        if (!inputUri.StartsWith(s3Prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Unexpected input URI format for job {jobId}: '{inputUri}'");

        var s3Key = inputUri[s3Prefix.Length..];
        _logger.LogInformation(
            "Resolved input S3 key for job {JobId}: {S3Key}", jobId, s3Key);
        return s3Key;
    }

    // ── Private Helpers ───────────────────────────────────────────────────────────────

    private static string RequireEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Required environment variable '{key}' is not set.");
}
