using OboxSteam.Application.Notifications;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class NotificationRecipientResolver : INotificationRecipientResolver
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationRecipientResolver(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        NotificationAudience audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        return audience.Kind switch
        {
            NotificationAudienceKind.User => ResolveUser(audience),
            NotificationAudienceKind.StudentAndParents => await ResolveStudentAndParentsAsync(audience),
            NotificationAudienceKind.ClassRoster => await ResolveClassRosterAsync(audience),
            NotificationAudienceKind.ClassMentor => await ResolveClassMentorAsync(audience),
            NotificationAudienceKind.ClassRosterAndMentor => await ResolveClassRosterAndMentorAsync(audience),
            NotificationAudienceKind.Managers => await ResolveManagersAsync(),
            _ => Array.Empty<Guid>()
        };
    }

    private static IReadOnlyList<Guid> ResolveUser(NotificationAudience audience)
    {
        if (audience.UserId is null || audience.UserId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        return new[] { audience.UserId.Value };
    }

    private async Task<IReadOnlyList<Guid>> ResolveStudentAndParentsAsync(NotificationAudience audience)
    {
        if (audience.StudentId is null || audience.StudentId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var studentId = audience.StudentId.Value;
        var parents = await _unitOfWork.ParentStudents.GetAllAsync(
            ps => ps.StudentId == studentId && ps.IsVerified);

        var ids = new HashSet<Guid> { studentId };
        foreach (var link in parents)
        {
            ids.Add(link.ParentId);
        }

        return ids.ToList();
    }

    private async Task<IReadOnlyList<Guid>> ResolveClassRosterAsync(NotificationAudience audience)
    {
        if (audience.ClassId is null || audience.ClassId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == audience.ClassId.Value
                  && ce.Status == ClassEnrollmentStatus.Active);

        return enrollments
            .Select(ce => ce.StudentId)
            .Distinct()
            .ToList();
    }

    private async Task<IReadOnlyList<Guid>> ResolveClassMentorAsync(NotificationAudience audience)
    {
        if (audience.ClassId is null || audience.ClassId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var clazz = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Id == audience.ClassId.Value);
        if (clazz is null || clazz.MentorId is null || clazz.MentorId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        return new[] { clazz.MentorId.Value };
    }

    private async Task<IReadOnlyList<Guid>> ResolveClassRosterAndMentorAsync(NotificationAudience audience)
    {
        var roster = await ResolveClassRosterAsync(audience);
        var mentor = await ResolveClassMentorAsync(audience);
        return roster.Concat(mentor).Distinct().ToList();
    }

    private async Task<IReadOnlyList<Guid>> ResolveManagersAsync()
    {
        var managers = await _unitOfWork.Users.GetAllAsync(
            u => u.Role == RoleType.Manager && u.Status == AccountStatus.Active);

        return managers.Select(u => u.Id).Distinct().ToList();
    }
}
