using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Services;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Hosted loop that closes purchases whose required AssignmentWindow has already ended.
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
                using var scope = _serviceProvider.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<ProgramPurchaseLifecycle>();
                var closed = await lifecycle.CloseElapsedRequiredWindowsAsync(stoppingToken);
                if (closed > 0)
                {
                    _logger.LogInformation(
                        "AssignmentWindowCloseService closed {Count} purchase(s).",
                        closed);
                }

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
