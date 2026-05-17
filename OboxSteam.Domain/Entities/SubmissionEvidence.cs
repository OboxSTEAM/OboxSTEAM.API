namespace OboxSteam.Domain.Entities;

/// <summary>
/// Join table linking Submissions to MediaAssets as evidence.
/// Composite key: (SubmissionId, MediaId)
/// </summary>
public class SubmissionEvidence : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public Guid MediaId { get; set; }
    public MediaAsset Media { get; set; } = null!;
}
