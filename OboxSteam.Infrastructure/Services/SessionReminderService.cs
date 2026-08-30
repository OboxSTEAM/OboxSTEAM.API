using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Hosted loop that wakes every 5 minutes and publishes 30-minute session reminders
/// via <see cref="ISessionReminderPublisher"/>.
/// </summary>
public sealed class SessionReminderService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionReminderService> _logger;

    public SessionReminderService(
        IServiceProvider serviceProvider,
        ILogger<SessionReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionReminderService running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publisher = scope.ServiceProvider.GetRequiredService<ISessionReminderPublisher>();
                var sent = await publisher.PublishDueRemindersAsync(stoppingToken);
                if (sent > 0)
                {
                    _logger.LogInformation("SessionReminderService published {Count} reminder(s).", sent);
                }

                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running SessionReminderService.");
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

        _logger.LogInformation("SessionReminderService stopped.");
    }
}
