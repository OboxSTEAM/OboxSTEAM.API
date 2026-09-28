using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Outcome of <see cref="ProgramReviewValidator.ResolveEligibilityAsync"/>.
/// <see cref="ActiveReview"/> is set only when <see cref="Reason"/> is
/// <see cref="ProgramReviewEligibilityReason.AlreadyReviewed"/>.
/// </summary>
public sealed record ProgramReviewEligibility(
    ProgramReviewEligibilityReason? Reason,
    ProgramReview? ActiveReview)
{
    public bool CanReview => Reason == null;
}
