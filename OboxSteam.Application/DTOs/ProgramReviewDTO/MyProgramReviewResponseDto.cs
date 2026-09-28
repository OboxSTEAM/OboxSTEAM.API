using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramReviewDTO;

/// <summary>
/// The current student's review state for a program.
/// </summary>
public class MyProgramReviewResponseDto
{
    /// <summary>True when the student may create a new review now.</summary>
    public bool CanReview { get; set; }

    /// <summary>Why the student cannot create a review; null when <see cref="CanReview"/> is true.</summary>
    public ProgramReviewEligibilityReason? Reason { get; set; }

    /// <summary>The student's active review; set only when <see cref="Reason"/> is AlreadyReviewed.</summary>
    public ProgramReviewResponseDto? Review { get; set; }
}
