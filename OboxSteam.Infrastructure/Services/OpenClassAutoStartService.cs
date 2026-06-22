using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Tier 2 safety net for auto-starting Open classes that are full and past StartDate.
/// Uses adaptive sleep from <see cref="IClassService.ResolveOpenClassAutoStartScheduleAsync"/>
/// instead of fixed-interval polling. Tier 1 (immediate start on enroll) lives in
/// <see cref="IClassService.TryAutoStartClassIfReadyAsync"/>.
/// </summary>
public class OpenClassAutoStartService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OpenClassAutoStartService> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(30);

    /// <summary>Used only when schedule resolution throws; avoids a tight error loop.</summary>
    private static readonly TimeSpan ErrorFallbackDelay = TimeSpan.FromMinutes(30);

    public OpenClassAutoStartService(
        IServiceProvider serviceProvider,
        ILogger<OpenClassAutoStartService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OpenClassAutoStartService running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = ErrorFallbackDelay;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var classService = scope.ServiceProvider.GetRequiredService<IClassService>();

                // Inspect Open classes once per wake; decide run vs sleep (no fixed 30-minute poll).
                var schedule = await classService.ResolveOpenClassAutoStartScheduleAsync();

                if (schedule.ShouldRunAutoStart)
                {
                    var startedCount = await classService.AutoStartEligibleOpenClassesAsync();

                    if (startedCount > 0)
                    {
                        _logger.LogInformation(
                            "OpenClassAutoStartService auto-started {Count} class(es) to InProgress.",
                            startedCount);
                    }
                }

                nextDelay = schedule.NextDelay;
                _logger.LogInformation(
                    "OpenClassAutoStartService next check in {Delay} ({Reason}).",
                    nextDelay,
                    schedule.Reason);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running OpenClassAutoStartService.");
            }

            try
            {
                await Task.Delay(nextDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OpenClassAutoStartService stopped.");
    }
}
