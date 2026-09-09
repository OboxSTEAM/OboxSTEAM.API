using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public sealed class ProgramAdvisoryThread : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;
    public Guid? SubmissionId { get; set; }
    public ProgramReviewSubmission? Submission { get; set; }
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid? TargetId { get; set; }
    [MaxLength(255)] public string TargetLabel { get; set; } = null!;
    public string? TargetContext { get; set; }
    public ProgramAdvisoryThreadType Type { get; set; }
    public ProgramAdvisoryThreadStatus Status { get; set; }
    public DateTime LastMessageAt { get; set; }
    public ICollection<ProgramAdvisoryMessage> Messages { get; set; } = [];
}
