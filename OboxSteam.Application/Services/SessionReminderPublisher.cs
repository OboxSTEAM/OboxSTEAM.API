using System.Globalization;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class SessionReminderPublisher : ISessionReminderPublisher
{
    public static readonly TimeSpan ReminderLead = TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTime _currentTime;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<SessionReminderPublisher> _logger;

    public SessionReminderPublisher(
        IUnitOfWork unitOfWork,
        ICurrentTime currentTime,
        INotificationPublisher notificationPublisher,
        ILogger<SessionReminderPublisher> logger)
    {
        _unitOfWork = unitOfWork;
        _currentTime = currentTime;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task<int> PublishDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = _currentTime.GetCurrentTime();
        var windowEnd = now.Add(ReminderLead);

        var dueSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            s => !s.IsDeleted
                 && s.ReminderSentAt == null
                 && (s.Status == ClassSessionStatus.Scheduled || s.Status == ClassSessionStatus.InProgress)
                 && s.StartTime > now
                 && s.StartTime <= windowEnd);

        if (dueSessions.Count == 0)
            return 0;

        var classIds = dueSessions.Select(s => s.ClassId).Distinct().ToList();
        var classes = await _unitOfWork.Classes.GetAllAsync(c => classIds.Contains(c.Id) && !c.IsDeleted);
        var classById = classes.ToDictionary(c => c.Id);

        var commands = new List<NotificationCommand>();
        foreach (var session in dueSessions)
        {
            classById.TryGetValue(session.ClassId, out var classEntity);
            var startLabel = FormatVietnamDateTime(session.StartTime);

            commands.Add(NotificationCatalog.SessionStartingSoon(
                session.ClassId,
                session.Id,
                startLabel,
                classEntity?.ProgramId,
                classEntity?.Name,
                sessionTitle: session.Title));

            session.ReminderSentAt = now;
            await _unitOfWork.ClassSessions.Update(session);
        }

        await _unitOfWork.SaveChangesAsync();
        await _notificationPublisher.PublishManyAsync(commands, cancellationToken);

        _logger.LogInformation("Published {Count} session starting-soon reminder(s).", commands.Count);
        return commands.Count;
    }

    private static string FormatVietnamDateTime(DateTime utcInstant)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(AppDateTime.AsUtc(utcInstant), AppDateTime.VietnamTimeZone);
        return local.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
    }
}
