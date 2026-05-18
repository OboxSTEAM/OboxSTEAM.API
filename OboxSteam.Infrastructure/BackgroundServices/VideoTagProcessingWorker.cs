using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Infrastructure.BackgroundServices;

public class VideoTagProcessingWorker : BackgroundService
{
    // Rekognition polling: 40 × 15 s = 10 minutes maximum wait
    private const int RekognitionMaxAttempts = 40;
    private static readonly TimeSpan RekognitionPollInterval = TimeSpan.FromSeconds(15);

    private readonly VideoProcessingChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VideoTagProcessingWorker> _logger;

    public VideoTagProcessingWorker(
        VideoProcessingChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<VideoTagProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // On startup, re-enqueue any jobs that were interrupted by a previous crash.
        await RecoverPendingJobsAsync(stoppingToken);

        // Process new items from the channel as they arrive.
        await foreach (var mediaId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

                await ProcessMediaAsync(mediaService, mediaId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("VideoTagProcessingWorker is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing video tags for MediaId: {MediaId}", mediaId);
            }
        }
    }

    // ── Recovery on startup ───────────────────────────────────────────────────

    private async Task RecoverPendingJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Recover videos that are mid-flight: still transcoding OR waiting for Rekognition.
            var pendingMedia = await unitOfWork.MediaAssets.GetAllAsync(m =>
                !m.IsDeleted &&
                (m.VideoStatus == VideoProcessingStatus.Transcoding ||
                 m.VideoStatus == VideoProcessingStatus.PendingTagging));

            foreach (var media in pendingMedia)
            {
                // For Transcoding jobs, the /tmp file must still exist.
                // If the container restarted, the file is gone — mark as Failed immediately.
                if (media.VideoStatus == VideoProcessingStatus.Transcoding)
                {
                    var tmpPath = media.RekognitionJobId;
                    if (string.IsNullOrEmpty(tmpPath) || !File.Exists(tmpPath))
                    {
                        _logger.LogWarning(
                            "Temp file missing for MediaId={MediaId} (container likely restarted). Marking as Failed.",
                            media.Id);
                        media.VideoStatus      = VideoProcessingStatus.Failed;
                        media.RekognitionJobId = null;
                        await unitOfWork.SaveChangesAsync();
                        continue;
                    }
                }

                _logger.LogInformation(
                    "Recovering pending video job: MediaId={MediaId}, VideoStatus={Status}",
                    media.Id, media.VideoStatus);
                await _channel.Writer.WriteAsync(media.Id, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover pending video jobs on startup.");
        }
    }

    // ── Per-item processing ───────────────────────────────────────────────────

    /// <summary>
    /// Full pipeline for one video:
    ///   1. FFmpeg transcode (blocking — handled inside StartVideoTranscodeAsync)
    ///   2. Rekognition face-search polling (up to 10 min)
    /// </summary>
    private async Task ProcessMediaAsync(IMediaService mediaService, Guid mediaId, CancellationToken ct)
    {
        _logger.LogInformation("ProcessMediaAsync started for MediaId: {MediaId}", mediaId);

        // ── Phase 1: Transcode ────────────────────────────────────────────────
        // This call blocks until MediaConvert completes (or throws on failure).
        // After it returns, VideoStatus = PendingTagging and the Rekognition job is running.
        try
        {
            await mediaService.StartVideoTranscodeAsync(mediaId);
        }
        catch (Exception ex)
        {
            // StartVideoTranscodeAsync already set VideoStatus = Failed and cleaned up /tmp.
            _logger.LogError(ex, "Transcoding failed for MediaId: {MediaId}. Aborting.", mediaId);
            return;
        }

        // ── Phase 2: Poll Rekognition ─────────────────────────────────────────
        await RetryRekognitionAsync(mediaService, mediaId, ct);
    }

    /// <summary>
    /// Polls Rekognition every 15 seconds, up to 40 attempts (10 minutes).
    /// Sets VideoStatus = Failed if all attempts are exhausted.
    /// </summary>
    private async Task RetryRekognitionAsync(IMediaService mediaService, Guid mediaId, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= RekognitionMaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            await Task.Delay(RekognitionPollInterval, ct);

            bool isDone;
            try
            {
                isDone = await mediaService.TryProcessVideoTagsAsync(mediaId);
            }
            catch (Exception ex)
            {
                // TryProcessVideoTagsAsync throws only on FAILED Rekognition job.
                // VideoStatus already set to Failed inside DoProcessVideoTagsAsync.
                _logger.LogError(ex, "Rekognition job FAILED for MediaId: {MediaId}", mediaId);
                return;
            }

            if (isDone)
            {
                _logger.LogInformation("Video tags processed successfully for MediaId: {MediaId}", mediaId);
                return;
            }

            _logger.LogInformation(
                "Rekognition poll attempt {Attempt}/{Max}: still in progress for MediaId: {MediaId}",
                attempt, RekognitionMaxAttempts, mediaId);
        }

        // All attempts exhausted — mark as Failed so recovery doesn't re-enqueue it.
        _logger.LogWarning(
            "Gave up waiting for Rekognition after {Max} attempts ({Minutes} min) for MediaId: {MediaId}. Marking as Failed.",
            RekognitionMaxAttempts, RekognitionMaxAttempts * RekognitionPollInterval.TotalMinutes, mediaId);

        await MarkAsFailedAsync(mediaId);
    }

    private async Task MarkAsFailedAsync(Guid mediaId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var media = await unitOfWork.MediaAssets.GetByIdAsync(mediaId);
            if (media != null)
            {
                media.VideoStatus = VideoProcessingStatus.Failed;
                await unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark MediaId={MediaId} as Failed in DB.", mediaId);
        }
    }
}
