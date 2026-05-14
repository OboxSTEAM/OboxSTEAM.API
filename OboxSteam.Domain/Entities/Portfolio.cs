using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Portfolio : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    /// <summary>For cloned/versioned portfolios.</summary>
    public Guid? ParentPortfolioId { get; set; }
    public Portfolio? ParentPortfolio { get; set; }

    [MaxLength(255)]
    public string? VersionName { get; set; }

    [MaxLength(100)]
    public string Subdomain { get; set; } = null!;

    public PlanType PlanType { get; set; } = PlanType.Standard;

    [MaxLength(100)]
    public string? TemplateId { get; set; }

    [MaxLength(20)]
    public string? PrimaryColor { get; set; }

    public bool IsPublic { get; set; } = true;

    // Navigation
    public ICollection<PortfolioCustomItem> CustomItems { get; set; } = new List<PortfolioCustomItem>();
}
