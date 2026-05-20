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
    // MediaConvert polling: 40 × 15 s = 10 minutes maximum wait for transcoding
    private const int MediaConvertMaxAttempts  = 40;

    // Rekognition polling: 40 × 15 s = 10 minutes maximum wait
    private const int RekognitionMaxAttempts   = 40;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly VideoProcessingChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VideoTagProcessingWorker> _logger;

    public VideoTagProcessingWorker(
        VideoProcessingChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<VideoTagProcessingWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
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
                _logger.LogError(ex, "Unhandled error processing video for MediaId: {MediaId}", mediaId);
            }
        }
    }

    // ── Recovery on startup ───────────────────────────────────────────────────

    private async Task RecoverPendingJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope    = _scopeFactory.CreateScope();
            var unitOfWork     = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Recover videos that are mid-flight: still transcoding OR waiting for Rekognition.
            var pendingMedia = await unitOfWork.MediaAssets.GetAllAsync(m =>
                !m.IsDeleted &&
                (m.VideoStatus == VideoProcessingStatus.Transcoding ||
                 m.VideoStatus == VideoProcessingStatus.PendingTagging));

            foreach (var media in pendingMedia)
            {
                // For Transcoding jobs, the raw video is on S3 (durable).
                // Unlike the old FFmpeg flow, we no longer depend on a local /tmp file,
                // so all Transcoding jobs can be safely re-enqueued regardless of crash.
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
    ///   1. Submit AWS MediaConvert job (non-blocking)
    ///   2. Poll MediaConvert job status (up to 10 min)
    ///   3. Poll Rekognition face-search (up to 10 min)
    /// </summary>
    private async Task ProcessMediaAsync(IMediaService mediaService, Guid mediaId, CancellationToken ct)
    {
        _logger.LogInformation("ProcessMediaAsync started for MediaId: {MediaId}", mediaId);

        // ── Phase 1a: Submit MediaConvert job (fast, non-blocking) ────────────
        try
        {
            await mediaService.StartVideoTranscodeAsync(mediaId);
        }
        catch (Exception ex)
        {
            // StartVideoTranscodeAsync already set VideoStatus = Failed.
            _logger.LogError(ex, "MediaConvert job submission failed for MediaId: {MediaId}. Aborting.", mediaId);
            return;
        }

        // ── Phase 1b: Poll MediaConvert until transcoding completes ───────────
        var transcodeCompleted = await PollMediaConvertAsync(mediaService, mediaId, ct);
        if (!transcodeCompleted)
            return; // failure already logged and status set to Failed

        // ── Phase 2: Poll Rekognition ─────────────────────────────────────────
        await RetryRekognitionAsync(mediaService, mediaId, ct);
    }

    /// <summary>
    /// Polls MediaConvert every 15 seconds, up to 40 attempts (10 minutes).
    /// Returns true when transcoding is complete (and Rekognition job has been started).
    /// Returns false if all attempts are exhausted or the job fails.
    /// </summary>
    private async Task<bool> PollMediaConvertAsync(
        IMediaService mediaService, Guid mediaId, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MediaConvertMaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(PollInterval, ct);

            bool isDone;
            try
            {
                isDone = await mediaService.TryCompleteTranscodeAsync(mediaId);
            }
            catch (Exception ex)
            {
                // TryCompleteTranscodeAsync throws on ERROR status (VideoStatus already set to Failed).
                _logger.LogError(ex, "MediaConvert job FAILED for MediaId: {MediaId}", mediaId);
                return false;
            }

            if (isDone)
            {
                _logger.LogInformation(
                    "MediaConvert transcoding completed for MediaId: {MediaId}", mediaId);
                return true;
            }

            _logger.LogInformation(
                "MediaConvert poll attempt {Attempt}/{Max}: still in progress for MediaId: {MediaId}",
                attempt, MediaConvertMaxAttempts, mediaId);
        }

        // All attempts exhausted
        _logger.LogWarning(
            "Gave up waiting for MediaConvert after {Max} attempts ({Minutes} min) for MediaId: {MediaId}. Marking as Failed.",
            MediaConvertMaxAttempts,
            MediaConvertMaxAttempts * PollInterval.TotalMinutes,
            mediaId);

        await MarkAsFailedAsync(mediaId);
        return false;
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
            await Task.Delay(PollInterval, ct);

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
                _logger.LogInformation(
                    "Video tags processed successfully for MediaId: {MediaId}", mediaId);
                return;
            }

            _logger.LogInformation(
                "Rekognition poll attempt {Attempt}/{Max}: still in progress for MediaId: {MediaId}",
                attempt, RekognitionMaxAttempts, mediaId);
        }

        // All attempts exhausted — mark as Failed so recovery doesn't re-enqueue it.
        _logger.LogWarning(
            "Gave up waiting for Rekognition after {Max} attempts ({Minutes} min) for MediaId: {MediaId}. Marking as Failed.",
            RekognitionMaxAttempts,
            RekognitionMaxAttempts * PollInterval.TotalMinutes,
            mediaId);

        await MarkAsFailedAsync(mediaId);
    }

    private async Task MarkAsFailedAsync(Guid mediaId)
    {
        try
        {
            using var scope  = _scopeFactory.CreateScope();
            var unitOfWork   = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

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
