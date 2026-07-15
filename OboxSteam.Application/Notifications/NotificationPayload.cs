namespace OboxSteam.Application.Notifications;

/// <summary>Typed deep-link payload serialized to <c>PayloadJson</c>.</summary>
public sealed class NotificationPayload
{
    public Guid? ProgramId { get; set; }
    public Guid? ProgramEnrollmentId { get; set; }
    public Guid? ModuleId { get; set; }
    public Guid? ModuleEnrollmentId { get; set; }
    public Guid? ActivityId { get; set; }
    public Guid? ClassId { get; set; }
    public Guid? ClassEnrollmentId { get; set; }
    public Guid? ClassSessionId { get; set; }
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
}
