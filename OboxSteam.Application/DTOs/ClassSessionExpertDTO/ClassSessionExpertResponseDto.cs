using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionExpertDTO;

public sealed class ClassSessionExpertResponseDto
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
    public ClassSessionExpertStatus Status { get; set; }
    public string SessionTitle { get; set; } = null!;
    public SessionKind SessionKind { get; set; }
    public ClassSessionStatus SessionStatus { get; set; }
    public DateTime SessionStartTime { get; set; }
    public DateTime SessionEndTime { get; set; }
    public DateTime? ProposedStartTime { get; set; }
    public DateTime? ProposedEndTime { get; set; }
    public string? ScheduleConflictWarning { get; set; }
    public string? MentorFeedback { get; set; }
    public int? MentorFeedbackRating { get; set; }
    public DateTime? MentorFeedbackAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
