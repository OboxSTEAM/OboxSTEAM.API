namespace OboxSteam.Domain.Enums;

/// <summary>
/// Kinds of portfolio sections. Group kinds are built-in (seeded per portfolio,
/// hide/reorder only); the remaining kinds are student-created custom blocks.
/// </summary>
public enum PortfolioSectionKind
{
    ProjectsGroup,
    ActivitiesGroup,
    LinksGroup,
    RichText,
    Gallery,
    Embed,
}
