namespace OboxSteam.Application.Notifications;

/// <summary>
/// Typed deep-link payload serialized to <c>PayloadJson</c> (camelCase).
/// FE resolves routes from <c>type</c> + these keys — see FE <c>resolve-href.ts</c> contract.
/// </summary>
public sealed class NotificationPayload
{
    public Guid? ProgramId { get; set; }

    /// <summary>Program enrollment id (legacy / internal name).</summary>
    public Guid? ProgramEnrollmentId { get; set; }

    /// <summary>
    /// FE deeplink alias for program enrollment
    /// (<c>/parent/children/{studentId}/programs/{enrollmentId}</c>).
    /// Prefer setting via <see cref="SetEnrollment"/> so both keys stay in sync.
    /// </summary>
    public Guid? EnrollmentId { get; set; }

    public Guid? ModuleId { get; set; }
    public Guid? ModuleEnrollmentId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ActivityId { get; set; }

    /// <summary>
    /// Next actionable activity after the event (e.g. after complete/unlock/activate).
    /// FE should prefer this over <see cref="ActivityId"/> for progression deep-links.
    /// </summary>
    public Guid? NextActivityId { get; set; }

    public Guid? ClassId { get; set; }
    public Guid? ClassEnrollmentId { get; set; }
    public Guid? ClassSessionId { get; set; }
    public Guid? ClassMentorRequestId { get; set; }
    public Guid? AssessmentRecoveryRequestId { get; set; }
    public Guid? ClassRedeliveryRequestId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? PaymentRequestId { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid? MediaAssetId { get; set; }
    public Guid? HighlightVideoId { get; set; }
    public Guid? ParentStudentId { get; set; }
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
