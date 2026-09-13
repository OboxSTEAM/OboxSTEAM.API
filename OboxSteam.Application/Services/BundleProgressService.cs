using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class BundleProgressService : IBundleProgressService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICertificateService _certificateService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<BundleProgressService> _logger;

    public BundleProgressService(
        IUnitOfWork unitOfWork,
        ICertificateService certificateService,
        INotificationPublisher notificationPublisher,
        ILogger<BundleProgressService> logger)
    {
        _unitOfWork = unitOfWork;
        _certificateService = certificateService;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task SyncAfterProgramProgressAsync(
        Guid programEnrollmentId,
        EnrollmentStatus previousStatus)
    {
        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            return;
        }

        var bundleEnrollments = (await _unitOfWork.BundleEnrollments.GetAllAsync(
                e => e.StudentId == enrollment.StudentId
                     && !e.IsDeleted
                     && (e.Status == BundleEnrollmentStatus.Active
                         || e.Status == BundleEnrollmentStatus.Completed)))
            .ToList();

        var related = new List<(BundleEnrollment Enrollment, List<ProgramBundleItem> Items)>();
        foreach (var bundleEnrollment in bundleEnrollments)
        {
            var items = (await _unitOfWork.ProgramBundleItems.GetAllAsync(
                    i => i.BundleId == bundleEnrollment.BundleId && !i.IsDeleted))
                .OrderBy(i => i.SortOrder)
                .ToList();
            if (items.All(i => i.ProgramId != enrollment.ProgramId))
            {
                continue;
            }

            bundleEnrollment.ProgressPercent = await BundleEnrollmentHelper.RecalculateProgressPercentAsync(
                _unitOfWork,
                enrollment.StudentId,
                items);
            related.Add((bundleEnrollment, items));
        }

        if (related.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        var justCompleted = previousStatus != EnrollmentStatus.Completed
                            && enrollment.Status == EnrollmentStatus.Completed;
        if (!justCompleted)
        {
            return;
        }

        var student = await _unitOfWork.Users.GetByIdAsync(enrollment.StudentId);
        var program = await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);
        var studentName = student?.FullName;
        var programName = program?.Name;

        var commands = new List<NotificationCommand>
        {
            NotificationCatalog.ProgramCompleted(
                enrollment.StudentId,
                enrollment.ProgramId,
                enrollment.Id,
                programName,
                studentName)
        };

        var completedAfter = await BundleEnrollmentHelper.GetCompletedProgramIdsAsync(
            _unitOfWork,
            enrollment.StudentId);
        var completedBefore = completedAfter
            .Where(id => id != enrollment.ProgramId)
            .ToHashSet();

        foreach (var (bundleEnrollment, items) in related)
        {
            if (bundleEnrollment.Status != BundleEnrollmentStatus.Active)
            {
                continue;
            }

            var newlyUnlocked = BundleEnrollmentHelper.FindNewlyUnlockedItems(
                items,
                completedBefore,
                completedAfter);
            foreach (var item in newlyUnlocked)
            {
                if (completedAfter.Contains(item.ProgramId))
                {
                    continue;
                }

                var unlockedProgram = await _unitOfWork.Programs.GetByIdAsync(item.ProgramId);
                commands.Add(NotificationCatalog.ProgramUnlocked(
                    enrollment.StudentId,
                    item.ProgramId,
                    bundleEnrollment.BundleId,
                    bundleEnrollment.Id,
                    unlockedProgram?.Name,
                    studentName));
            }

            if (!BundleEnrollmentHelper.AreAllItemsCompleted(items, completedAfter))
            {
                continue;
            }

            bundleEnrollment.Status = BundleEnrollmentStatus.Completed;
            bundleEnrollment.ProgressPercent = 100m;
            await _unitOfWork.SaveChangesAsync();

            await TryEnsureBundleCertificateAsync(bundleEnrollment.Id);

            var bundle = await _unitOfWork.ProgramBundles.GetByIdAsync(bundleEnrollment.BundleId);
            commands.Add(NotificationCatalog.BundleCompleted(
                enrollment.StudentId,
                bundleEnrollment.BundleId,
                bundleEnrollment.Id,
                studentName,
                bundle?.Name));
        }

        await _notificationPublisher.PublishManyAsync(commands);

        _logger.LogInformation(
            "[SyncAfterProgramProgressAsync] Program enrollment {EnrollmentId} completed. Notifications={Count}.",
            programEnrollmentId,
            commands.Count);
    }

    private async Task TryEnsureBundleCertificateAsync(Guid bundleEnrollmentId)
    {
        try
        {
            await _certificateService.EnsureBundleCertificateInternalAsync(bundleEnrollmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TryEnsureBundleCertificateAsync] Failed for bundle enrollment {EnrollmentId}. Pathway completion was not rolled back.",
                bundleEnrollmentId);
        }
    }
}
