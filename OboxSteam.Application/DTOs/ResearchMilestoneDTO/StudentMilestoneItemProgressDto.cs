using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class StudentMilestoneItemProgressDto
{
    public Guid MilestoneId { get; set; }
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public int MilestoneOrder { get; set; }
    public bool IsCapstone { get; set; }
    public bool IsUnlocked { get; set; }
    public string? UnlockReason { get; set; }
    public bool CanSubmit { get; set; }
    public List<string> SubmitBlockReasons { get; set; } = [];
    public Guid AssignmentId { get; set; }
    public Guid? SubmissionId { get; set; }
    public SubmissionStatus? SubmissionStatus { get; set; }
    public decimal? AssignedGrade { get; set; }
    public bool? Passed { get; set; }
    public List<StudentMilestoneActivityProgressDto> RequiredActivities { get; set; } = [];
}
