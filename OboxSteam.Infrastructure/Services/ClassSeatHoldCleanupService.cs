using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

public sealed class ClassSeatHoldCleanupService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ClassSeatHoldCleanupService> _logger;

    public ClassSeatHoldCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ClassSeatHoldCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClassSeatHoldCleanupService running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReleaseExpiredHoldsAsync(stoppingToken);
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running ClassSeatHoldCleanupService.");
                await Task.Delay(RunInterval, stoppingToken);
            }
        }

        _logger.LogInformation("ClassSeatHoldCleanupService stopped.");
    }

    private async Task ReleaseExpiredHoldsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var seatHoldService = scope.ServiceProvider.GetRequiredService<IClassSeatHoldService>();
        var affected = await seatHoldService.ReleaseExpiredHoldsAsync(stoppingToken);

        foreach (var (classId, programId) in affected)
        {
            await seatHoldService.PublishSeatsChangedAsync(programId, classId, stoppingToken);
        }
    }
}
