using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.AssignmentDTO;

public class AssignmentResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid ModuleId { get; set; }
    public Guid? CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public AssignmentType AssignmentType { get; set; }
    public int MaxPoints { get; set; }
    public decimal PassScore { get; set; }
    public bool IsRequiredForModulePass { get; set; }
    public bool AllowShuffle { get; set; }
    public Guid? QuestionBankId { get; set; }
    public int? QuestionCount { get; set; }
    public bool ShuffleOptions { get; set; }
    public int EasyPercent { get; set; }
    public int MediumPercent { get; set; }
    public int HardPercent { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
