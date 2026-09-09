using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class ProgramReviewSubmissionSummaryDto
{
    public Guid Id { get; set; }

    public int SubmissionNumber { get; set; }

    public ProgramReviewSubmissionStatus Status { get; set; }

    public Guid AssignedAdvisorExpertId { get; set; }

    public Guid? FrameworkVersionId { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid ConcurrencyVersion { get; set; }
}

public sealed class ProgramReviewSubmissionDetailDto
{
    public Guid Id { get; set; }

    public Guid ProgramId { get; set; }

    public int SubmissionNumber { get; set; }

    public ProgramReviewSubmissionStatus Status { get; set; }

    public Guid SubmittedByManagerId { get; set; }

    public Guid AssignedAdvisorExpertId { get; set; }

    public Guid? FrameworkVersionId { get; set; }

    public string CurriculumSnapshotJson { get; set; } = null!;

    public string RubricSnapshotJson { get; set; } = "[]";

    public DateTime SubmittedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid ConcurrencyVersion { get; set; }
}
