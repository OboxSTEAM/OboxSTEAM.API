using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassMentorRequestDTO;

public class ClassMentorRequestResponseDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public Guid ProgramId { get; set; }
    public Guid MentorId { get; set; }
    public string? MentorCode { get; set; }
    public string? MentorName { get; set; }
    public ClassMentorRequestStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
