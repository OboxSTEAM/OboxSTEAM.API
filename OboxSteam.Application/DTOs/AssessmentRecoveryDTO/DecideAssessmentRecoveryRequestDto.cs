namespace OboxSteam.Application.DTOs.AssessmentRecoveryDTO;

public class DecideAssessmentRecoveryRequestDto
{
    /// <summary>Extra attempts to grant (0 allowed for deadline-only / Theory window extension).</summary>
    public int ExtraAttemptsGranted { get; set; } = 1;

    public DateTime? PersonalDueDate { get; set; }
    public DateTime? PersonalAvailableUntil { get; set; }
    public string? MentorNote { get; set; }
}
