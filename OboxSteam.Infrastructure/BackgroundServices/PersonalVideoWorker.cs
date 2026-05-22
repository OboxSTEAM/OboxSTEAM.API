using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that polls the status of MediaConvert personal video generation jobs.
/// Reads HighlightVideo IDs from <see cref="PersonalVideoChannel"/>, polls until the
/// MediaConvert job completes, then writes the output S3 URL back to the DB.
/// </summary>
public class PersonalVideoWorker : BackgroundService
{
    // Poll up to 60 × 15 s = 15 minutes per job
    private const int MaxAttempts = 60;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly PersonalVideoChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersonalVideoWorker> _logger;

    public PersonalVideoWorker(
        PersonalVideoChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<PersonalVideoWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // On startup, re-enqueue any jobs that were in progress when the server was restarted.
        await RecoverPendingJobsAsync(stoppingToken);

        await foreach (var highlightVideoId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(highlightVideoId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("PersonalVideoWorker is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error processing personal video for HighlightVideoId={Id}", highlightVideoId);
            }
        }
    }

    // ── Recovery ─────────────────────────────────────────────────────────────

    private async Task RecoverPendingJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope  = _scopeFactory.CreateScope();
            var unitOfWork   = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var pending = await unitOfWork.HighlightVideos.GetAllAsync(
                hv => !hv.IsDeleted && hv.PersonalVideoStatus == HighlightVideoStatus.Processing);

            foreach (var hv in pending)
            {
                _logger.LogInformation(
                    "PersonalVideoWorker: recovering job HighlightVideoId={Id}, JobRef={Ref}",
                    hv.Id, hv.PersonalVideoJobRef);
                await _channel.Writer.WriteAsync(hv.Id, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PersonalVideoWorker: failed to recover pending jobs on startup.");
        }
    }

    // ── Per-job processing ────────────────────────────────────────────────────

    private async Task ProcessJobAsync(Guid highlightVideoId, CancellationToken ct)
    {
        _logger.LogInformation(
            "PersonalVideoWorker: starting poll for HighlightVideoId={Id}", highlightVideoId);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var scope             = _scopeFactory.CreateScope();
            var unitOfWork              = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var videoConverterService   = scope.ServiceProvider.GetRequiredService<IVideoConverterService>();
            var blobService             = scope.ServiceProvider.GetRequiredService<IBlobService>();

            var hv = await unitOfWork.HighlightVideos.GetByIdAsync(highlightVideoId);
            if (hv == null || hv.IsDeleted)
            {
                _logger.LogWarning(
                    "PersonalVideoWorker: HighlightVideoId={Id} not found or deleted. Aborting.", highlightVideoId);
                return;
            }

            if (hv.PersonalVideoStatus != HighlightVideoStatus.Processing || string.IsNullOrEmpty(hv.PersonalVideoJobRef))
            {
                _logger.LogInformation(
                    "PersonalVideoWorker: HighlightVideoId={Id} is no longer Processing. Aborting poll.", highlightVideoId);
                return;
            }

            MediaConvertJobStatus status;
            try
            {
                status = await videoConverterService.GetJobStatusAsync(hv.PersonalVideoJobRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PersonalVideoWorker: GetJobStatusAsync failed (attempt {Attempt}/{Max}) for HighlightVideoId={Id}",
                    attempt, MaxAttempts, highlightVideoId);

                await Task.Delay(PollInterval, ct);
                continue;
            }

            if (status == MediaConvertJobStatus.InProgress)
            {
                _logger.LogInformation(
                    "PersonalVideoWorker: poll {Attempt}/{Max} → still in progress. HighlightVideoId={Id}",
                    attempt, MaxAttempts, highlightVideoId);
                await Task.Delay(PollInterval, ct);
                continue;
            }

            if (status == MediaConvertJobStatus.Error)
            {
                _logger.LogError(
                    "PersonalVideoWorker: MediaConvert job FAILED for HighlightVideoId={Id}. Marking as Failed.",
                    highlightVideoId);
                await MarkFailedAsync(highlightVideoId);
                return;
            }

            // ── COMPLETE ──────────────────────────────────────────────────────
            try
            {
                var outputS3Key = await videoConverterService.GetOutputS3KeyAsync(hv.PersonalVideoJobRef);
                var videoUrl    = await blobService.GetPreviewUrlAsync(outputS3Key);

                hv.VideoUrl             = videoUrl;
                hv.PersonalVideoStatus  = HighlightVideoStatus.Completed;
                hv.Status               = "Completed";
                await unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "PersonalVideoWorker: job COMPLETED for HighlightVideoId={Id}. URL={Url}",
                    highlightVideoId, videoUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PersonalVideoWorker: post-completion update failed for HighlightVideoId={Id}. Marking as Failed.",
                    highlightVideoId);
                await MarkFailedAsync(highlightVideoId);
            }

            return; // done
        }

        // ── Max attempts exhausted ────────────────────────────────────────────
        _logger.LogWarning(
            "PersonalVideoWorker: gave up after {Max} attempts ({Minutes} min) for HighlightVideoId={Id}. Marking as Failed.",
            MaxAttempts, MaxAttempts * PollInterval.TotalMinutes, highlightVideoId);

        await MarkFailedAsync(highlightVideoId);
    }

    private async Task MarkFailedAsync(Guid highlightVideoId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork  = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var hv = await unitOfWork.HighlightVideos.GetByIdAsync(highlightVideoId);
            if (hv != null)
            {
                hv.PersonalVideoStatus = HighlightVideoStatus.Failed;
                hv.Status              = "Failed";
                await unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PersonalVideoWorker: MarkFailedAsync failed for HighlightVideoId={Id}.", highlightVideoId);
        }
    }
}
