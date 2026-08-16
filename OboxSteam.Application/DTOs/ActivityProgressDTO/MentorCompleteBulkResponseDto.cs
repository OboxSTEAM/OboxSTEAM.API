namespace OboxSteam.Application.DTOs.ActivityProgressDTO;

public class MentorCompleteBulkResponseDto
{
    public Guid ClassSessionId { get; set; }

    public Guid ActivityId { get; set; }

    public List<MentorCompleteStudentResultDto> Results { get; set; } = [];
}

public class MentorCompleteStudentResultDto
{
    public Guid StudentId { get; set; }

    public MentorCompleteOutcome Outcome { get; set; }

    public string? Reason { get; set; }

    public ActivityProgressResponseDto? Progress { get; set; }
}

public enum MentorCompleteOutcome
{
    Completed,
    AlreadyDone,
    Skipped,
}
