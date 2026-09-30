using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>Hourly removal of advisory chat attachments that were uploaded but never sent.</summary>
public sealed class DiscussionAttachmentPurgeService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DiscussionAttachmentPurgeService> _logger;

    public DiscussionAttachmentPurgeService(
        IServiceProvider serviceProvider,
        ILogger<DiscussionAttachmentPurgeService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DiscussionAttachmentPurgeService running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var attachments = scope.ServiceProvider.GetRequiredService<IProgramAdvisoryAttachmentService>();
                await attachments.PurgeUnsentAsync(stoppingToken);
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running DiscussionAttachmentPurgeService.");
                await Task.Delay(RunInterval, stoppingToken);
            }
        }

        _logger.LogInformation("DiscussionAttachmentPurgeService stopped.");
    }
}
