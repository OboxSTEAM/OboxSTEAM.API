using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// Role-specific copy for one event. Missing variants fall back to <see cref="Default"/>.
/// Placeholders such as <c>{studentName}</c> are interpolated at publish time.
/// </summary>
public sealed class NotificationRoleTemplates
{
    public NotificationText Default { get; }

    public NotificationText? Student { get; }

    public NotificationText? Parent { get; }

    public NotificationText? Mentor { get; }

    public NotificationText? Manager { get; }

    public NotificationRoleTemplates(
        NotificationText @default,
        NotificationText? student = null,
        NotificationText? parent = null,
        NotificationText? mentor = null,
        NotificationText? manager = null)
    {
        Default = @default ?? throw new ArgumentNullException(nameof(@default));
        Student = student;
        Parent = parent;
        Mentor = mentor;
        Manager = manager;
    }

    public static NotificationRoleTemplates FromDefault(string title, string? body)
        => new(new NotificationText(title, body));

    public static NotificationRoleTemplates ForParent(string title, string parentBody)
    {
        var parent = new NotificationText(title, parentBody);
        return new(parent, parent: parent);
    }

    public static NotificationRoleTemplates ForStudentAndParent(
        string title,
        string studentBody,
        string parentBody)
    {
        var student = new NotificationText(title, studentBody);
        return new(student, student: student, parent: new NotificationText(title, parentBody));
    }

    public static NotificationRoleTemplates ForStudentParentAndMentor(
        string title,
        string studentBody,
        string parentBody,
        string? mentorBody = null)
    {
        var student = new NotificationText(title, studentBody);
        return new(
            student,
            student: student,
            parent: new NotificationText(title, parentBody),
            mentor: new NotificationText(title, mentorBody ?? studentBody));
    }

    public NotificationText Resolve(RoleType role) => role switch
    {
        RoleType.Student => Student ?? Default,
        RoleType.Parent => Parent ?? Default,
        RoleType.Mentor => Mentor ?? Default,
        RoleType.Manager or RoleType.Admin => Manager ?? Default,
        _ => Default
    };
}
