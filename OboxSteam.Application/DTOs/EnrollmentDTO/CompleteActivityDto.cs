namespace OboxSteam.Application.DTOs.EnrollmentDTO;

public class CompleteActivityRequestDto
{
    /// <summary>Optional audit hint (not persisted until audit column exists).</summary>
    public string? Source { get; set; }
}

public class CompleteActivityResponseDto
{
    public decimal ProgressPercent { get; set; }

    public Guid? NextActivityId { get; set; }

    public List<Guid> UnlockedModuleIds { get; set; } = [];

    public string ActivityStatus { get; set; } = "completed";
}
