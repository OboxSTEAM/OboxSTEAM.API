using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class PortfolioCustomItem : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;

    [MaxLength(50)]
    public string ItemType { get; set; } = null!; // InternalCert, ExternalCert, Hobby, Extracurricular

    /// <summary>If InternalCert, maps to Certificates.Id.</summary>
    public Guid? ReferenceId { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? MediaUrl { get; set; } // S3 Link (Proof)

    public int DisplayOrder { get; set; }
}
