namespace OboxSteam.Domain.Enums;

/// <summary>
/// Persisted reason a manager submitted a review round. Nullable on legacy rows
/// because older submissions did not record enough information to distinguish the intent safely.
/// </summary>
public enum ProgramReviewSubmissionIntent
{
    InitialReview,
    RevisionVerification,
}
