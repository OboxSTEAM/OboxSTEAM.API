using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

public class ClassResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid ProgramId { get; set; }
    public Guid? MentorId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MaxCapacity { get; set; }
    public int SeatsTaken { get; set; }
    public ClassStatus Status { get; set; }
    public int MinHoursBeforeAssignmentJoin { get; set; }
    public string? ScheduleSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ClassStudentResponseDto> Students { get; set; } = new();
}
