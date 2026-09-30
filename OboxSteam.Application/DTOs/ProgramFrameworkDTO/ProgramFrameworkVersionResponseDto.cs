namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public sealed class ProgramFrameworkVersionResponseDto
{
    public Guid Id { get; set; }
    public Guid FrameworkId { get; set; }
    public int VersionNumber { get; set; }
    public string? Description { get; set; }
    public string? AcademicGuidance { get; set; }
    public int? MinModules { get; set; }
    public int? MaxModules { get; set; }
    public int? MinCoursesPerModule { get; set; }
    public int? MaxCoursesPerModule { get; set; }
    public int? MinTotalHours { get; set; }
    public int? MaxTotalHours { get; set; }
    public int? MaxActivityMinutes { get; set; }
    public bool RequireActivityDuration { get; set; }
    public int? MinOfflineSessions { get; set; }
    public int? MinLiveSessions { get; set; }
    public int? MinOfflineRatioPercent { get; set; }
    public int? MinLiveRatioPercent { get; set; }
    public bool RequireAssignmentPerModule { get; set; }
    public bool RequireAssignmentPassScore { get; set; }
    public int? MinMaterialsPerActivity { get; set; }
    public bool RequireCategoryMatch { get; set; }
    public int? MinDescriptionLength { get; set; }
    public int? MinSkillsGained { get; set; }
    public bool RequireThumbnail { get; set; }
    public bool? RequireCapstoneResearchMilestone { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
