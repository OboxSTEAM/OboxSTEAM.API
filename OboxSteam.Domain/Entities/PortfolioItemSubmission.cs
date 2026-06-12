using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Appendix row linking prior milestone <see cref="Submission"/> records to a
/// capstone <see cref="PortfolioCustomItem"/> (one combined portfolio project).
/// </summary>
public class PortfolioItemSubmission : BaseEntity
{
    public Guid PortfolioCustomItemId { get; set; }
    public PortfolioCustomItem PortfolioCustomItem { get; set; } = null!;

    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    [MaxLength(255)]
    public string SectionTitle { get; set; } = null!;

    public int DisplayOrder { get; set; }
}
