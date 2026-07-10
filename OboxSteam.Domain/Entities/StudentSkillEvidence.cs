namespace OboxSteam.Domain.Entities;

/// <summary>
/// Evidence linking a <see cref="StudentSkill"/> to a submission, certificate, and/or media asset.
/// At least one of the optional FKs must be set (enforced in the application layer).
/// </summary>
public class StudentSkillEvidence : BaseEntity
{
    public Guid StudentSkillId { get; set; }
    public StudentSkill StudentSkill { get; set; } = null!;

    public Guid? SubmissionId { get; set; }
    public Submission? Submission { get; set; }

    public Guid? CertificateId { get; set; }
    public Certificate? Certificate { get; set; }

    public Guid? MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }
}
