using System.ComponentModel;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// Typed deep-link payload for <c>NotificationDto.payload</c> (OpenAPI) and
/// serialized <c>payloadJson</c> (camelCase). FE resolves routes from notification
/// <c>type</c> + these keys.
/// </summary>
public sealed class NotificationPayload
{
    [Description("Program catalog id used in /programs/{programId} and manager curriculum routes.")]
    public Guid? ProgramId { get; set; }

    /// <summary>Program enrollment id (legacy / internal name).</summary>
    [Description("Same as enrollmentId (legacy key). Prefer enrollmentId on FE.")]
    public Guid? ProgramEnrollmentId { get; set; }

    /// <summary>
    /// FE deeplink alias for program enrollment
    /// (<c>/parent/children/{studentId}/programs/{enrollmentId}</c>).
    /// Prefer setting via <see cref="SetEnrollment"/> so both keys stay in sync.
    /// </summary>
    [Description("Program enrollment id for parent progression and student learn context.")]
    public Guid? EnrollmentId { get; set; }

    [Description("Module id (manager curriculum editor / progression context).")]
    public Guid? ModuleId { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }

    [Description("Course id for manager activity editor deep-links.")]
    public Guid? CourseId { get; set; }

    [Description("Activity node id (completed or target). Prefer nextActivityId for progression.")]
    public Guid? ActivityId { get; set; }

    /// <summary>
    /// Next actionable activity after the event (e.g. after complete/unlock/activate).
    /// FE should prefer this over <see cref="ActivityId"/> for progression deep-links.
    /// </summary>
    [Description("Next actionable activity id for learn ?activityId= deep-links.")]
    public Guid? NextActivityId { get; set; }

    [Description("Class entity id for manager/mentor class routes.")]
    public Guid? ClassId { get; set; }

    public Guid? ClassEnrollmentId { get; set; }

    [Description("Class session id for manager attendance deep-links.")]
    public Guid? ClassSessionId { get; set; }

    public Guid? ClassMentorRequestId { get; set; }
    public Guid? AssessmentRecoveryRequestId { get; set; }
    public Guid? ClassRedeliveryRequestId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? PaymentRequestId { get; set; }

    [Description("Assignment node id for learn ?assignmentId= or manager assignment editor.")]
    public Guid? AssignmentId { get; set; }

    public Guid? SubmissionId { get; set; }
    public Guid? MaterialId { get; set; }

    [Description("Media asset id for mentor/manager media deep-links.")]
    public Guid? MediaAssetId { get; set; }

    public Guid? HighlightVideoId { get; set; }
    public Guid? ParentStudentId { get; set; }

    [Description("Subject student user id (required for parent child-scoped routing).")]
    public Guid? StudentId { get; set; }

    public string? Extra { get; set; }

    /// <summary>Sets both <see cref="ProgramEnrollmentId"/> and <see cref="EnrollmentId"/>.</summary>
    public NotificationPayload SetEnrollment(Guid? programEnrollmentId)
    {
        ProgramEnrollmentId = programEnrollmentId;
        EnrollmentId = programEnrollmentId;
        return this;
    }

    public NotificationPayload Clone() => new()
    {
        ProgramId = ProgramId,
        ProgramEnrollmentId = ProgramEnrollmentId,
        EnrollmentId = EnrollmentId,
        ModuleId = ModuleId,
        ModuleEnrollmentId = ModuleEnrollmentId,
        CourseId = CourseId,
        ActivityId = ActivityId,
        NextActivityId = NextActivityId,
        ClassId = ClassId,
        ClassEnrollmentId = ClassEnrollmentId,
        ClassSessionId = ClassSessionId,
        ClassMentorRequestId = ClassMentorRequestId,
        AssessmentRecoveryRequestId = AssessmentRecoveryRequestId,
        ClassRedeliveryRequestId = ClassRedeliveryRequestId,
        PaymentId = PaymentId,
        PaymentRequestId = PaymentRequestId,
        AssignmentId = AssignmentId,
        SubmissionId = SubmissionId,
        MaterialId = MaterialId,
        MediaAssetId = MediaAssetId,
        HighlightVideoId = HighlightVideoId,
        ParentStudentId = ParentStudentId,
        StudentId = StudentId,
        Extra = Extra
    };
}
