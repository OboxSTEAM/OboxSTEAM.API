using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Side effects shared by every path that moves a <see cref="ClassSession"/> into
/// <see cref="ClassSessionStatus.Completed"/> (manual PUT and the elapsed-session job).
/// </summary>
public static class ClassSessionCompletionHelper
{
    /// <summary>
    /// Closes open LiveOnline join segments on the roster. Caller saves changes.
    /// </summary>
    public static async Task CloseOpenParticipationSegmentsAsync(
        IUnitOfWork unitOfWork,
        ClassSession session,
        DateTime now)
    {
        var attendances = await unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.ClassSessionId == session.Id && !sa.IsDeleted);

        foreach (var attendance in attendances)
        {
            if (attendance.CheckedInAt == null || attendance.LeftAt != null)
            {
                continue;
            }

            SessionParticipationHelper.CloseOpenSegment(attendance, session.EndTime, now);
            await unitOfWork.SessionAttendances.Update(attendance);
        }
    }

    /// <summary>
    /// <c>ClassSessionCompleted</c> for the roster plus one feedback request per Accepted co-teach expert.
    /// </summary>
    public static async Task<List<NotificationCommand>> BuildCompletedNotificationsAsync(
        IUnitOfWork unitOfWork,
        ClassSession session,
        Class classEntity)
    {
        var commands = new List<NotificationCommand>
        {
            NotificationCatalog.ClassSessionCompleted(
                session.ClassId,
                session.Id,
                classEntity.ProgramId,
                classEntity.Name),
        };

        var accepted = (await unitOfWork.ClassSessionExperts.GetAllAsync(
                e => e.ClassSessionId == session.Id
                     && !e.IsDeleted
                     && e.Status == ClassSessionExpertStatus.Accepted))
            .OrderBy(e => e.CreatedAt)
            .ToList();

        foreach (var coTeach in accepted)
        {
            var expert = await unitOfWork.Experts.GetByIdAsync(coTeach.ExpertId);
            if (expert?.UserId is not Guid expertUserId)
            {
                continue;
            }

            commands.Add(NotificationCatalog.ClassSessionExpertFeedbackRequested(
                expertUserId,
                coTeach.Id,
                session.Id,
                classEntity.Id,
                classEntity.ProgramId,
                classEntity.Name,
                programName: null,
                session.Title));
        }

        return commands;
    }
}
