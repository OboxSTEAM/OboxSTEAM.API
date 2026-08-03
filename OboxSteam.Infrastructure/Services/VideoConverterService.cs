using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Transcodes video using AWS MediaConvert (fully managed cloud service).
/// Workflow: raw video already in S3 → submit MediaConvert job → callers poll status via
/// <see cref="GetJobStatusAsync"/> when handling SNS/EventBridge completion (see
/// <c>MediaService.TryCompleteTranscodeAsync</c>). No local FFmpeg or /tmp files required.
/// </summary>
public class VideoConverterService : IVideoConverterService
{
    private const string EnvS3Bucket = "AWS_S3_BUCKET";
    private const string EnvRoleArn = "AWS_MEDIACONVERT_ROLE_ARN";

    private readonly IAmazonMediaConvert _mediaConvert;
    private readonly ILogger<VideoConverterService> _logger;

    public VideoConverterService(
        IAmazonMediaConvert mediaConvert,
        ILogger<VideoConverterService> logger)
    {
        _mediaConvert = mediaConvert;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> SubmitTranscodeJobAsync(string inputS3Key, string outputDestinationPrefix)
    {
        var bucket = RequireEnv(EnvS3Bucket);
        var roleArn = RequireEnv(EnvRoleArn);

        // Ensure prefix ends with /
        if (!outputDestinationPrefix.EndsWith('/'))
            outputDestinationPrefix += '/';

        var inputUri = $"s3://{bucket}/{inputS3Key}";
        var outputGroupUri = $"s3://{bucket}/{outputDestinationPrefix}";

        _logger.LogInformation(
            "Submitting MediaConvert job: {Input} → {Output}",
            inputUri, outputGroupUri);

        var request = new CreateJobRequest
        {
            Role = roleArn,
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
                                VideoDescription  = BuildVideoDescription(),
                                AudioDescriptions = BuildAudioDescriptions(),
                                ContainerSettings = BuildContainerSettings()
                            }
                        }
                    }
                }
            }
        };

        var response = await _mediaConvert.CreateJobAsync(request);
        var jobId = response.Job.Id;

        _logger.LogInformation("MediaConvert job submitted. JobId: {JobId}", jobId);
        return jobId;
    }

    /// <inheritdoc />
    public async Task<string> SubmitPersonalVideoJobAsync(List<ClipInput> clips, string outputS3Key)
    {
        var bucket = RequireEnv(EnvS3Bucket);
        var roleArn = RequireEnv(EnvRoleArn);
        // Watermark URI is configurable via environment variable so it survives bucket/region changes.
        var watermarkUri = ResolveWatermarkUri();

        _logger.LogInformation(
            "SubmitPersonalVideoJobAsync: {ClipCount} input(s) → s3://{Bucket}/{Key}",
            clips.Count, bucket, outputS3Key);

        var inputs = BuildPersonalVideoInputs(clips, bucket);

        var request = new CreateJobRequest
        {
            Role = roleArn,
            Settings = new JobSettings
            {
                Inputs = inputs,
                OutputGroups = [BuildPersonalVideoOutputGroup(bucket, outputS3Key, watermarkUri, "Personal Video MP4")]
            }
        };

        var response = await _mediaConvert.CreateJobAsync(request);
        _logger.LogInformation(
            "SubmitPersonalVideoJobAsync: job submitted. JobId={JobId}", response.Job.Id);
        return response.Job.Id;
    }

    /// <inheritdoc />
    public async Task<long?> GetOutputDurationMsAsync(string jobId)
    {
        var response = await _mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId });
        var durationMs = response.Job.OutputGroupDetails?
            .SelectMany(g => g.OutputDetails ?? [])
            .Select(o => o.DurationInMs)
            .FirstOrDefault(d => d > 0);

        if (durationMs is > 0)
        {
            _logger.LogInformation(
                "GetOutputDurationMsAsync: job {JobId} duration={DurationMs}ms", jobId, durationMs);
            return durationMs;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<MediaConvertJobStatus> GetJobStatusAsync(string jobId)
    {
        var progress = await GetJobProgressAsync(jobId);
        return progress.Status;
    }

    /// <inheritdoc />
    public async Task<MediaConvertJobProgress> GetJobProgressAsync(string jobId)
    {
        var response = await _mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId });
        var job = response.Job;
        var status = job.Status;

        _logger.LogDebug(
            "MediaConvert job {JobId} status: {Status}, percent: {Percent}",
            jobId, status, job.JobPercentComplete);

        MediaConvertJobStatus mapped;
        if (status == JobStatus.COMPLETE)
            mapped = MediaConvertJobStatus.Complete;
        else if (status == JobStatus.ERROR || status == JobStatus.CANCELED)
            mapped = MediaConvertJobStatus.Error;
        else
            mapped = MediaConvertJobStatus.InProgress;

        var percent = mapped switch
        {
            MediaConvertJobStatus.Complete => 100,
            MediaConvertJobStatus.Error => ClampPercent(job.JobPercentComplete),
            _ => ClampPercent(job.JobPercentComplete)
        };

        return new MediaConvertJobProgress(mapped, percent);
    }

    private static int ClampPercent(int value) => Math.Clamp(value, 0, 100);

    /// <inheritdoc />
    public async Task<string> GetOutputS3KeyAsync(string jobId)
    {
        var bucket = RequireEnv(EnvS3Bucket);
        var response = await _mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId });
        var job = response.Job;

        var outputGroup = job.Settings.OutputGroups.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"MediaConvert job {jobId} has no output groups.");

        var destUri = outputGroup.OutputGroupSettings.FileGroupSettings.Destination;
        var nameModifier = outputGroup.Outputs.FirstOrDefault()?.NameModifier ?? string.Empty;

        string outputUri;
        if (destUri.EndsWith('/'))
        {
            var inputUri = job.Settings.Inputs.First().FileInput;
            var inputFileName = inputUri.Split('/').Last();
            var inputBaseName = Path.GetFileNameWithoutExtension(inputFileName);
            outputUri = $"{destUri}{inputBaseName}{nameModifier}.mp4";
        }
        else
        {
            outputUri = $"{destUri}{nameModifier}.mp4";
        }

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
        var bucket = RequireEnv(EnvS3Bucket);
        var response = await _mediaConvert.GetJobAsync(new GetJobRequest { Id = jobId });

        var inputUri = response.Job.Settings.Inputs.First().FileInput;
        var s3Prefix = $"s3://{bucket}/";

        if (!inputUri.StartsWith(s3Prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Unexpected input URI format for job {jobId}: '{inputUri}'");

        var s3Key = inputUri[s3Prefix.Length..];
        _logger.LogInformation(
            "Resolved input S3 key for job {JobId}: {S3Key}", jobId, s3Key);
        return s3Key;
    }

    // ── Personal highlight video job builders ────────────────────────────────

    private static string ResolveWatermarkUri() =>
        Environment.GetEnvironmentVariable("AWS_WATERMARK_URI")
        ?? "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/logo-obox.png";

    private static List<Input> BuildPersonalVideoInputs(IReadOnlyList<ClipInput> clips, string bucket) =>
        clips.Select(clip =>
        {
            var input = new Input
            {
                FileInput = $"s3://{bucket}/{clip.S3Key}",
                TimecodeSource = InputTimecodeSource.ZEROBASED,
                AudioSelectors = new Dictionary<string, AudioSelector>
                {
                    ["Audio Selector 1"] = new AudioSelector
                    {
                        DefaultSelection = AudioDefaultSelection.DEFAULT
                    }
                }
            };

            if (clip.Clips is { Count: > 0 })
            {
                input.InputClippings = clip.Clips
                    .Select(c => new InputClipping
                    {
                        StartTimecode = c.StartTimecode,
                        EndTimecode = c.EndTimecode
                    })
                    .ToList();
            }

            return input;
        }).ToList();

    private static OutputGroup BuildPersonalVideoOutputGroup(
        string bucket,
        string outputS3Key,
        string watermarkUri,
        string groupName)
    {
        var outputFolder = Path.GetDirectoryName(outputS3Key)?.Replace('\\', '/')
                           ?? HighlightVideoConstants.OutputFolder;
        var outputFileName = Path.GetFileNameWithoutExtension(outputS3Key);
        var destUri = $"s3://{bucket}/{outputFolder}/{outputFileName}";

        return new OutputGroup
        {
            Name = groupName,
            OutputGroupSettings = new OutputGroupSettings
            {
                Type = OutputGroupType.FILE_GROUP_SETTINGS,
                FileGroupSettings = new FileGroupSettings
                {
                    Destination = destUri
                }
            },
            Outputs =
            [
                new Output
                {
                    VideoDescription = BuildVideoDescription(watermarkUri),
                    AudioDescriptions = BuildAudioDescriptions(),
                    ContainerSettings = BuildContainerSettings()
                }
            ]
        };
    }

    // ── Shared codec/container builders ──────────────────────────────────────

    private static VideoDescription BuildVideoDescription(string? watermarkS3Uri = null)
    {
        var desc = new VideoDescription
        {
            CodecSettings = new VideoCodecSettings
            {
                Codec = VideoCodec.H_264,
                H264Settings = new H264Settings
                {
                    RateControlMode = H264RateControlMode.QVBR,
                    MaxBitrate = 5_000_000, // 5 Mbps — required for QVBR
                    QvbrSettings = new H264QvbrSettings
                    {
                        QvbrQualityLevel = 7   // ~CRF 23 equivalent
                    }
                    // Note: do NOT set FlickerAdaptiveQuantization,
                    // SpatialAdaptiveQuantization, or TemporalAdaptiveQuantization
                    // when H264AdaptiveQuantization is AUTO (the default).
                    // MediaConvert handles these automatically.
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(watermarkS3Uri))
        {
            desc.VideoPreprocessors = new VideoPreprocessor
            {
                ImageInserter = new ImageInserter
                {
                    InsertableImages =
                    [
                        new InsertableImage
                        {
                            ImageInserterInput = watermarkS3Uri,
                            // Tọa độ giả định cho video 1080p (1920x1080)
                            // ImageX = 1920 - 150 (Width) - 50 (Margin) = 1720
                            // ImageY = 1080 - 150 (Height) - 50 (Margin) = 880
                            ImageX = 1780,
                            ImageY = 950,
                            Width = 125,  // Scale down width (adjust as needed)
                            Height = 125, // Scale down height (adjust as needed)
                            Opacity = 80,
                            Layer = 1
                        }
                    ]
                }
            };
        }

        return desc;
    }

    private static List<AudioDescription> BuildAudioDescriptions() =>
    [
        new AudioDescription
        {
            AudioSourceName = "Audio Selector 1",
            CodecSettings   = new AudioCodecSettings
            {
                Codec       = AudioCodec.AAC,
                AacSettings = new AacSettings
                {
                    Bitrate    = 96000,
                    CodingMode = AacCodingMode.CODING_MODE_2_0,
                    SampleRate = 48000
                }
            }
        }
    ];

    private static ContainerSettings BuildContainerSettings() => new()
    {
        Container = ContainerType.MP4,
        Mp4Settings = new Mp4Settings
        {
            // Move moov atom to front for progressive download / streaming
            MoovPlacement = Mp4MoovPlacement.PROGRESSIVE_DOWNLOAD
        }
    };

    // ── Private Helpers ───────────────────────────────────────────────────────────────

    private static string RequireEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Required environment variable '{key}' is not set.");
}
