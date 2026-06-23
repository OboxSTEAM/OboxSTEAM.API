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
    private const string EnvS3Bucket = "AWS_S3_BUCKET";
    private const string EnvRoleArn = "AWS_MEDIACONVERT_ROLE_ARN";
    private const string PersonalVideoFolder = "personal-videos";

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
        var watermarkUri = Environment.GetEnvironmentVariable("AWS_WATERMARK_URI")
            ?? "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/logo-obox.png";

        _logger.LogInformation(
            "SubmitPersonalVideoJobAsync: {ClipCount} input(s) → s3://{Bucket}/{Key}",
            clips.Count, bucket, outputS3Key);

        // Build one MediaConvert Input per source video
        var inputs = clips.Select(clip =>
        {
            var input = new Input
            {
                FileInput = $"s3://{bucket}/{clip.S3Key}",
                // ZEROBASED so timecodes in InputClippings are relative to 00:00:00:000
                TimecodeSource = InputTimecodeSource.ZEROBASED,
                AudioSelectors = new Dictionary<string, AudioSelector>
                {
                    ["Audio Selector 1"] = new AudioSelector
                    {
                        DefaultSelection = AudioDefaultSelection.DEFAULT
                    }
                }
            };

            // If time clips are specified, add InputClippings
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

        // Derive destination prefix and filename from the output S3 key
        var outputFolder = Path.GetDirectoryName(outputS3Key)?.Replace('\\', '/') ?? PersonalVideoFolder;
        var outputFileName = Path.GetFileNameWithoutExtension(outputS3Key);
        var destUri = $"s3://{bucket}/{outputFolder}/{outputFileName}";

        var request = new CreateJobRequest
        {
            Role = roleArn,
            Settings = new JobSettings
            {
                Inputs = inputs,
                OutputGroups = new List<OutputGroup>
                {
                    new OutputGroup
                    {
                        Name                = "Personal Video MP4",
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            Type              = OutputGroupType.FILE_GROUP_SETTINGS,
                            FileGroupSettings = new FileGroupSettings
                            {
                                Destination = destUri
                            }
                        },
                        Outputs = new List<Output>
                        {
                            new Output
                            {
                                VideoDescription  = BuildVideoDescription(watermarkUri),
                                AudioDescriptions = BuildAudioDescriptions(),
                                ContainerSettings = BuildContainerSettings()
                            }
                        }
                    }
                }
            }
        };

        var response = await _mediaConvert.CreateJobAsync(request);
        _logger.LogInformation(
            "SubmitPersonalVideoJobAsync: job submitted. JobId={JobId}", response.Job.Id);
        return response.Job.Id;
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
