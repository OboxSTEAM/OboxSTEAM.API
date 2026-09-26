namespace OboxSteam.Domain.Entities;

/// <summary>
/// Student curation of an achieved catalog skill on a portfolio.
/// Achievement itself is computed; this row only stores visibility, pin, and order.
/// </summary>
public class PortfolioSkill : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public bool IsVisible { get; set; } = true;

    public bool IsPinned { get; set; }

    public int DisplayOrder { get; set; }
}
