using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Infrastructure.Services;

public class PendingEnrollmentCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingEnrollmentCleanupService> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    public PendingEnrollmentCleanupService(IServiceProvider serviceProvider, ILogger<PendingEnrollmentCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingEnrollmentCleanupService running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredEnrollmentsAsync(stoppingToken);
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running PendingEnrollmentCleanupService.");
                await Task.Delay(RunInterval, stoppingToken);
            }
        }

        _logger.LogInformation("PendingEnrollmentCleanupService stopped.");
    }

    private async Task CleanupExpiredEnrollmentsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoffTime = DateTime.UtcNow.AddDays(-1);
        _logger.LogInformation("Checking for expired pending enrollments created before {CutoffTime}.", cutoffTime);

        bool changesMade = false;

        // 1. Clean up expired ProgramEnrollments
        var expiredEnrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.Status == EnrollmentStatus.PendingPayment && pe.CreatedAt < cutoffTime && !pe.IsDeleted
        );

        if (expiredEnrollments.Any())
        {
            _logger.LogInformation("Found {Count} expired pending program enrollments to clean up.", expiredEnrollments.Count);
            await unitOfWork.ProgramEnrollments.SoftRemoveRange(expiredEnrollments);
            changesMade = true;
        }

        // 2. Clean up expired ModuleEnrollments
        var expiredModuleEnrollments = await unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.Status == EnrollmentStatus.PendingPayment && me.CreatedAt < cutoffTime && !me.IsDeleted
        );

        if (expiredModuleEnrollments.Any())
        {
            _logger.LogInformation("Found {Count} expired pending module enrollments to clean up.", expiredModuleEnrollments.Count);
            await unitOfWork.ModuleEnrollments.SoftRemoveRange(expiredModuleEnrollments);
            changesMade = true;
        }

        if (changesMade)
        {
            int affectedRows = await unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Successfully soft deleted expired enrollment records. SaveChanges affected {AffectedRows} rows.", affectedRows);

            if (expiredEnrollments.Any())
            {
                var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
                var notifications = expiredEnrollments
                    .Select(pe => NotificationCatalog.PendingPaymentExpired(pe.StudentId, pe.Id, pe.ProgramId))
                    .ToList();
                await notificationPublisher.PublishManyAsync(notifications, stoppingToken);
            }
        }
        else
        {
            _logger.LogInformation("No expired pending enrollments found.");
        }
    }
}
