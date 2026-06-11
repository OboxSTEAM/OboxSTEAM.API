using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when running PendingEnrollmentCleanupService.");
            }

            // Await next run
            await Task.Delay(RunInterval, stoppingToken);
        }

        _logger.LogInformation("PendingEnrollmentCleanupService running.");
    }

    private async Task CleanupExpiredEnrollmentsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoffTime = DateTime.UtcNow.AddDays(-1);
        _logger.LogInformation("Checking for expired pending enrollments created before {CutoffTime}.", cutoffTime);

        var expiredEnrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.Status == EnrollmentStatus.PendingPayment && pe.CreatedAt < cutoffTime && !pe.IsDeleted
        );

        if (expiredEnrollments.Any())
        {
            _logger.LogInformation("Found {Count} expired pending enrollments to clean up.", expiredEnrollments.Count);

            await unitOfWork.ProgramEnrollments.SoftRemoveRange(expiredEnrollments);
            int affectedRows = await unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Successfully soft deleted {Count} expired enrollment records. SaveChanges affected {AffectedRows} rows.", expiredEnrollments.Count, affectedRows);
        }
        else
        {
            _logger.LogInformation("No expired pending enrollments found.");
        }
    }
}
