using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionExpertDTO;

/// <summary>
/// Co-teach card for a student enrolled in the class. Feedback and schedule
/// warnings are omitted so they are not serialized.
/// </summary>
public sealed class ClassSessionExpertStudentResponseDto
{
    public Guid Id { get; set; }
    public Guid ClassSessionId { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid ProgramId { get; set; }
    public Guid ExpertId { get; set; }
    public Guid? ExpertUserId { get; set; }
    public string ExpertCode { get; set; } = null!;
    public string ExpertName { get; set; } = null!;
    public string? ExpertAvatarUrl { get; set; }
    public ClassSessionExpertStatus Status { get; set; }
    public string SessionTitle { get; set; } = null!;
    public SessionKind SessionKind { get; set; }
    public ClassSessionStatus SessionStatus { get; set; }
    public DateTime SessionStartTime { get; set; }
    public DateTime SessionEndTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
