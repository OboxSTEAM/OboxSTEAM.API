using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>
/// Class cohort details with all sessions scheduled for that class.
/// </summary>
public class ClassWithSessionsResponseDto
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ClassSessionResponseDto> Sessions { get; set; } = new();
}
