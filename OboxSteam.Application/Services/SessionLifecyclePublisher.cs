using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class SessionLifecyclePublisher : ISessionLifecyclePublisher
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTime _currentTime;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<SessionLifecyclePublisher> _logger;

    public SessionLifecyclePublisher(
        IUnitOfWork unitOfWork,
        ICurrentTime currentTime,
        INotificationPublisher notificationPublisher,
        ILogger<SessionLifecyclePublisher> logger)
    {
        _unitOfWork = unitOfWork;
        _currentTime = currentTime;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task<int> StartDueSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = _currentTime.GetCurrentTime();

        var dueSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            s => !s.IsDeleted
                 && (s.SessionKind == SessionKind.LiveOnline || s.SessionKind == SessionKind.Offline)
                 && s.Status == ClassSessionStatus.Scheduled
                 && s.StartTime <= now
                 && s.EndTime > now);

        if (dueSessions.Count == 0)
            return 0;

        var classById = await LoadClassesAsync(dueSessions.Select(s => s.ClassId));

        var commands = new List<NotificationCommand>();
        foreach (var session in dueSessions)
        {
            session.Status = ClassSessionStatus.InProgress;
            await _unitOfWork.ClassSessions.Update(session);

            if (classById.TryGetValue(session.ClassId, out var classEntity))
            {
                commands.Add(NotificationCatalog.ClassSessionStarted(
                    session.ClassId,
                    session.Id,
                    classEntity.ProgramId,
                    classEntity.Name));
            }
        }

        await _unitOfWork.SaveChangesAsync();

        if (commands.Count > 0)
        {
            await _notificationPublisher.PublishManyAsync(commands, cancellationToken);
        }

        _logger.LogInformation("Auto-started {Count} class session(s).", dueSessions.Count);
        return dueSessions.Count;
    }

    public async Task<int> CompleteElapsedSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = _currentTime.GetCurrentTime();

        var elapsedSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            s => !s.IsDeleted
                 && (s.SessionKind == SessionKind.LiveOnline || s.SessionKind == SessionKind.Offline)
                 && (s.Status == ClassSessionStatus.Scheduled || s.Status == ClassSessionStatus.InProgress)
                 && s.EndTime <= now);

        if (elapsedSessions.Count == 0)
            return 0;

        var classById = await LoadClassesAsync(elapsedSessions.Select(s => s.ClassId));

        var commands = new List<NotificationCommand>();
        foreach (var session in elapsedSessions)
        {
            session.Status = ClassSessionStatus.Completed;
            await ClassSessionCompletionHelper.CloseOpenParticipationSegmentsAsync(_unitOfWork, session, now);
            await _unitOfWork.ClassSessions.Update(session);

            if (classById.TryGetValue(session.ClassId, out var classEntity))
            {
                commands.AddRange(await ClassSessionCompletionHelper.BuildCompletedNotificationsAsync(
                    _unitOfWork,
                    session,
                    classEntity));
            }
        }

        await _unitOfWork.SaveChangesAsync();

        if (commands.Count > 0)
        {
            await _notificationPublisher.PublishManyAsync(commands, cancellationToken);
        }

        _logger.LogInformation("Auto-completed {Count} elapsed class session(s).", elapsedSessions.Count);
        return elapsedSessions.Count;
    }

    private async Task<Dictionary<Guid, Class>> LoadClassesAsync(IEnumerable<Guid> classIds)
    {
        var ids = classIds.Distinct().ToList();
        var classes = await _unitOfWork.Classes.GetAllAsync(c => ids.Contains(c.Id) && !c.IsDeleted);
        return classes.ToDictionary(c => c.Id);
    }
}
