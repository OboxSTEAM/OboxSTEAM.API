namespace OboxSteam.Application.Realtime;

/// <summary>Well-known <see cref="SyncEvent.Scope"/> values.</summary>
public static class SyncScopes
{
    /// <summary>The curriculum tree or content of a program changed.</summary>
    public const string CurriculumStructureChanged = "curriculum.structureChanged";

    /// <summary>Open-class seat counts changed (hold, enroll, transfer, release).</summary>
    public const string SeatsChanged = "seats.changed";

    /// <summary>A program advisory chat message was posted, edited, or removed, or a system message was added.</summary>
    public const string AdvisoryDiscussionChanged = "advisory.discussionChanged";

    /// <summary>A chat message was pinned, unpinned, or its pin status changed.</summary>
    public const string AdvisoryPinChanged = "advisory.pinChanged";

    /// <summary>The program approval was requested, granted, revoked, or the program was published.</summary>
    public const string AdvisoryApprovalChanged = "advisory.approvalChanged";

    /// <summary>A mentor or staff member recorded a student's session attendance.</summary>
    public const string AttendanceChanged = "attendance.changed";

    /// <summary>A student turned in assignment or research work for mentor review.</summary>
    public const string SubmissionTurnedIn = "submission.turnedIn";

    /// <summary>A student's activity was completed (self, mentor bulk, or force complete).</summary>
    public const string ActivityProgressChanged = "activityProgress.changed";

    /// <summary>A mentor or manager graded or returned a FileUpload or research submission.</summary>
    public const string SubmissionGraded = "submission.graded";
}
