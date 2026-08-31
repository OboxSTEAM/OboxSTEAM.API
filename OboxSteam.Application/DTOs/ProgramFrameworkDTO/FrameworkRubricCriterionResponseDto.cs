namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public class FrameworkRubricCriterionResponseDto
{
    public Guid Id { get; set; }
    public Guid FrameworkId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int MaxScore { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
