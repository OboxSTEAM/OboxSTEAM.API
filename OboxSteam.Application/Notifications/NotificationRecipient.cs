using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// One resolved inbox target. Parents of multiple students in the same audience
/// receive a separate row per <see cref="ContextStudentId"/>.
/// </summary>
public sealed class NotificationRecipient
{
    public Guid UserId { get; }

    public RoleType Role { get; }

    public Guid? ContextStudentId { get; }

    public NotificationRecipient(Guid userId, RoleType role, Guid? contextStudentId = null)
    {
        UserId = userId;
        Role = role;
        ContextStudentId = contextStudentId;
    }
}
