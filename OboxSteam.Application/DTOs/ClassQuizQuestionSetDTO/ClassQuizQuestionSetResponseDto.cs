namespace OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;

public class ClassQuizQuestionSetResponseDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid AssignmentId { get; set; }
    public DateTime PulledAt { get; set; }
    public bool IsLocked { get; set; }
    public List<ClassQuizQuestionResponseDto> Questions { get; set; } = new();
}
