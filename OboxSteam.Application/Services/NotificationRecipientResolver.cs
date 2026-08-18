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

    public async Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(
        NotificationAudience audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        return audience.Kind switch
        {
            NotificationAudienceKind.User => await ResolveUserAsync(audience),
            NotificationAudienceKind.StudentAndParents => await ResolveStudentAndParentsAsync(audience),
            NotificationAudienceKind.ClassRoster => await ResolveClassRosterAsync(audience),
            NotificationAudienceKind.ClassRosterAndParents => await ResolveClassRosterAndParentsAsync(audience),
            NotificationAudienceKind.ClassMentor => await ResolveClassMentorAsync(audience),
            NotificationAudienceKind.ClassRosterAndMentor => await ResolveClassRosterAndMentorAsync(audience),
            NotificationAudienceKind.ClassRosterAndParentsAndMentor =>
                await ResolveClassRosterAndParentsAndMentorAsync(audience),
            NotificationAudienceKind.Managers => await ResolveManagersAsync(),
            _ => Array.Empty<NotificationRecipient>()
        };
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveUserAsync(NotificationAudience audience)
    {
        if (audience.UserId is null || audience.UserId == Guid.Empty)
        {
            return Array.Empty<NotificationRecipient>();
        }

        var userId = audience.UserId.Value;
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var role = user?.Role ?? RoleType.Student;
        return new[] { new NotificationRecipient(userId, role, audience.StudentId) };
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveStudentAndParentsAsync(
        NotificationAudience audience)
    {
        if (audience.StudentId is null || audience.StudentId == Guid.Empty)
        {
            return Array.Empty<NotificationRecipient>();
        }

        var studentId = audience.StudentId.Value;
        var recipients = new List<NotificationRecipient>
        {
            new(studentId, RoleType.Student, studentId)
        };

        var parents = await _unitOfWork.ParentStudents.GetAllAsync(
            ps => ps.StudentId == studentId && ps.IsVerified);

        foreach (var link in parents)
        {
            recipients.Add(new NotificationRecipient(link.ParentId, RoleType.Parent, studentId));
        }

        return recipients;
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveClassRosterAsync(
        NotificationAudience audience)
    {
        if (audience.ClassId is null || audience.ClassId == Guid.Empty)
        {
            return Array.Empty<NotificationRecipient>();
        }

        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == audience.ClassId.Value
                  && ce.Status == ClassEnrollmentStatus.Active);

        return enrollments
            .Select(ce => ce.StudentId)
            .Distinct()
            .Select(studentId => new NotificationRecipient(studentId, RoleType.Student, studentId))
            .ToList();
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveClassRosterAndParentsAsync(
        NotificationAudience audience)
    {
        var roster = await ResolveClassRosterAsync(audience);
        if (roster.Count == 0)
        {
            return roster;
        }

        var studentIds = roster
            .Select(r => r.ContextStudentId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var parentLinks = await _unitOfWork.ParentStudents.GetAllAsync(
            ps => studentIds.Contains(ps.StudentId) && ps.IsVerified);

        var recipients = new List<NotificationRecipient>(roster);
        foreach (var link in parentLinks)
        {
            recipients.Add(new NotificationRecipient(link.ParentId, RoleType.Parent, link.StudentId));
        }

        return recipients;
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveClassMentorAsync(
        NotificationAudience audience)
    {
        if (audience.ClassId is null || audience.ClassId == Guid.Empty)
        {
            return Array.Empty<NotificationRecipient>();
        }

        var clazz = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Id == audience.ClassId.Value);
        if (clazz is null || clazz.MentorId is null || clazz.MentorId == Guid.Empty)
        {
            return Array.Empty<NotificationRecipient>();
        }

        return new[] { new NotificationRecipient(clazz.MentorId.Value, RoleType.Mentor) };
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveClassRosterAndMentorAsync(
        NotificationAudience audience)
    {
        var roster = await ResolveClassRosterAsync(audience);
        var mentor = await ResolveClassMentorAsync(audience);
        return roster.Concat(mentor).ToList();
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveClassRosterAndParentsAndMentorAsync(
        NotificationAudience audience)
    {
        var rosterAndParents = await ResolveClassRosterAndParentsAsync(audience);
        var mentor = await ResolveClassMentorAsync(audience);
        return rosterAndParents.Concat(mentor).ToList();
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveManagersAsync()
    {
        var managers = await _unitOfWork.Users.GetAllAsync(
            u => u.Role == RoleType.Manager && u.Status == AccountStatus.Active);

        return managers
            .Select(u => u.Id)
            .Distinct()
            .Select(id => new NotificationRecipient(id, RoleType.Manager))
            .ToList();
    }
}
