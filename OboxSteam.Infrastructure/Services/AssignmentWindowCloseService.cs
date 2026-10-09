using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Infrastructure.Observability;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Hosted loop that grades timed-out quiz attempts, then closes purchases whose required
/// AssignmentWindow has already ended.
/// </summary>
public sealed class AssignmentWindowCloseService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AssignmentWindowCloseService> _logger;

    public AssignmentWindowCloseService(
        IServiceProvider serviceProvider,
        ILogger<AssignmentWindowCloseService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AssignmentWindowCloseService running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (SeedExecutionGuard.IsSeeding)
                {
                    await Task.Delay(RunInterval, stoppingToken);
                    continue;
                }

                await TelemetrySources.RunPollingJobAsync("assignment-window-close", async activity =>
                {
                    using var scope = _serviceProvider.CreateScope();

                    // Grade timed-out quiz attempts first so a saved passing draft counts before
                    // the elapsed-window close decides AcademicFail.
                    var quizAttempts = scope.ServiceProvider.GetRequiredService<IQuizAttemptService>();
                    var graded = await quizAttempts.FinalizeExpiredAttemptsAsync(stoppingToken);
                    activity?.SetTag("job.quiz_attempts_graded", graded);
                    if (graded > 0)
                    {
                        _logger.LogInformation(
                            "AssignmentWindowCloseService graded {Count} expired quiz attempt(s).",
                            graded);
                    }

                    var lifecycle = scope.ServiceProvider.GetRequiredService<ProgramPurchaseLifecycle>();
                    var closed = await lifecycle.CloseElapsedRequiredWindowsAsync(stoppingToken);
                    activity?.SetTag("job.purchases_closed", closed);
                    if (closed > 0)
                    {
                        _logger.LogInformation(
                            "AssignmentWindowCloseService closed {Count} purchase(s).",
                            closed);
                    }

                    return graded > 0 || closed > 0;
                });

                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running AssignmentWindowCloseService.");
                try
                {
                    await Task.Delay(RunInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("AssignmentWindowCloseService stopped.");
    }
}
