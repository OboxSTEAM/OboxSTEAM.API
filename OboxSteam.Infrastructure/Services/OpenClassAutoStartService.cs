using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

public class OpenClassAutoStartService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OpenClassAutoStartService> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);

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
            try
            {
                await AutoStartEligibleClassesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running OpenClassAutoStartService.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }

        _logger.LogInformation("OpenClassAutoStartService stopped.");
    }

    private async Task AutoStartEligibleClassesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();

        var startedCount = await classService.AutoStartEligibleOpenClassesAsync();

        if (startedCount > 0)
        {
            _logger.LogInformation(
                "OpenClassAutoStartService auto-started {Count} class(es) to InProgress.",
                startedCount);
        }
    }
}
