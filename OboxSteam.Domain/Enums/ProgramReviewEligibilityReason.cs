namespace OboxSteam.Domain.Enums;

/// <summary>
/// Why a student cannot create a program review. Absent (null) when the student may review.
/// </summary>
public enum ProgramReviewEligibilityReason
{
    NotEnrolled,
    NotCompleted,
    AlreadyReviewed,
    RemovedByModerator
}
