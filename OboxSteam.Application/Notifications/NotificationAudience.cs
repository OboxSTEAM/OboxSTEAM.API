namespace OboxSteam.Application.Notifications;

/// <summary>Describes who should receive a notification; resolved by <see cref="Interfaces.INotificationRecipientResolver"/>.</summary>
public sealed class NotificationAudience
{
    public NotificationAudienceKind Kind { get; }
    public Guid? UserId { get; }
    public Guid? StudentId { get; }
    public Guid? ClassId { get; }
    public Guid? ProgramId { get; }

    private NotificationAudience(
        NotificationAudienceKind kind,
        Guid? userId = null,
        Guid? studentId = null,
        Guid? classId = null,
        Guid? programId = null)
    {
        Kind = kind;
        UserId = userId;
        StudentId = studentId;
        ClassId = classId;
        ProgramId = programId;
    }

    /// <param name="contextStudentId">
    /// Optional student this message is about (e.g. a parent-only payment request).
    /// Used as <c>ContextStudentId</c> when resolving the recipient.
    /// </param>
    public static NotificationAudience ForUser(Guid userId, Guid? contextStudentId = null)
        => new(NotificationAudienceKind.User, userId: userId, studentId: contextStudentId);

    public static NotificationAudience ForStudentAndParents(Guid studentId)
        => new(NotificationAudienceKind.StudentAndParents, studentId: studentId);

    public static NotificationAudience ForClassRoster(Guid classId)
        => new(NotificationAudienceKind.ClassRoster, classId: classId);

    public static NotificationAudience ForClassRosterAndParents(Guid classId)
        => new(NotificationAudienceKind.ClassRosterAndParents, classId: classId);

    public static NotificationAudience ForClassMentor(Guid classId)
        => new(NotificationAudienceKind.ClassMentor, classId: classId);

    public static NotificationAudience ForClassRosterAndMentor(Guid classId)
        => new(NotificationAudienceKind.ClassRosterAndMentor, classId: classId);

    public static NotificationAudience ForClassRosterAndParentsAndMentor(Guid classId)
        => new(NotificationAudienceKind.ClassRosterAndParentsAndMentor, classId: classId);

    public static NotificationAudience ForManagers()
        => new(NotificationAudienceKind.Managers);

    /// <summary>
    /// Everyone following a program's curriculum: actively enrolled students, their verified
    /// parents, and mentors of the program's classes. Used by ephemeral sync events.
    /// </summary>
    public static NotificationAudience ForProgramParticipants(Guid programId)
        => new(NotificationAudienceKind.ProgramParticipants, programId: programId);
}
