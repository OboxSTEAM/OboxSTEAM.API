using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Infrastructure.Observability;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Hosted loop that wakes every 5 minutes and applies clock-driven session status moves
/// via <see cref="ISessionLifecyclePublisher"/>.
/// </summary>
public sealed class SessionLifecycleService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionLifecycleService> _logger;

    public SessionLifecycleService(
        IServiceProvider serviceProvider,
        ILogger<SessionLifecycleService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionLifecycleService running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (SeedExecutionGuard.IsSeeding)
                {
                    await Task.Delay(RunInterval, stoppingToken);
                    continue;
                }

                await TelemetrySources.RunPollingJobAsync("session-lifecycle", async activity =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<ISessionLifecyclePublisher>();
                    var completed = await publisher.CompleteElapsedSessionsAsync(stoppingToken);
                    var started = await publisher.StartDueSessionsAsync(stoppingToken);
                    activity?.SetTag("job.sessions_started", started);
                    activity?.SetTag("job.sessions_completed", completed);
                    if (completed > 0 || started > 0)
                    {
                        _logger.LogInformation(
                            "SessionLifecycleService started {Started} and completed {Completed} session(s).",
                            started,
                            completed);
                    }

                    return completed > 0 || started > 0;
                });

                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running SessionLifecycleService.");
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

        _logger.LogInformation("SessionLifecycleService stopped.");
    }
}
