using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public sealed class ProgramReviewSubmission : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public int SubmissionNumber { get; set; }
    public Guid SubmittedByManagerId { get; set; }
    public User SubmittedByManager { get; set; } = null!;
    public Guid AssignedAdvisorExpertId { get; set; }
    public Expert AssignedAdvisorExpert { get; set; } = null!;
    public Guid? FrameworkVersionId { get; set; }
    public ProgramFrameworkVersion? FrameworkVersion { get; set; }
    public string CurriculumSnapshotJson { get; set; } = null!;
    public string RubricSnapshotJson { get; set; } = "[]";
    public ProgramReviewSubmissionIntent? ReviewRoundIntent { get; set; }
    public ProgramReviewSubmissionStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    [ConcurrencyCheck] public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();
    public ICollection<ProgramAdvisoryThread> Threads { get; set; } = [];
}
