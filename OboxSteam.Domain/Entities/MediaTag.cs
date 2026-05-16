namespace OboxSteam.Domain.Entities;

/// <summary>
/// AI face detection results linking media to identified students.
/// Composite key: (MediaId, StudentId)
/// </summary>
public class MediaTag : BaseEntity
{
    public Guid MediaId { get; set; }
    public MediaAsset Media { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public decimal ConfidenceScore { get; set; }

    public bool IsVerified { get; set; } = true;
}
