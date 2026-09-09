namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public sealed class ProgramFrameworkVersionResponseDto
{
    public Guid Id { get; set; }
    public Guid FrameworkId { get; set; }
    public int VersionNumber { get; set; }
    public string? Description { get; set; }
    public string? AcademicGuidance { get; set; }
    public int? MinModules { get; set; }
    public int? MinOfflineSessions { get; set; }
    public int? MinLiveSessions { get; set; }
    public bool? RequireCapstoneResearchMilestone { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public IReadOnlyList<FrameworkRubricCriterionResponseDto> Criteria { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
