namespace OboxSteam.Application.DTOs.AssessmentRecoveryDTO;

public class DecideAssessmentRecoveryRequestDto
{
    /// <summary>Extra attempts to grant (must be at least 1).</summary>
    public int ExtraAttemptsGranted { get; set; } = 1;

    public string? MentorNote { get; set; }
}
