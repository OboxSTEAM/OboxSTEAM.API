namespace OboxSteam.Domain.Enums;

/// <summary>
/// Catalog lifecycle for a program bundle. Stored as text via EF string enum conversion.
/// Distinct from <see cref="ProgramStatus"/> — bundles do not go through expert review.
/// </summary>
public enum ProgramBundleStatus
{
    /// <summary>Manager is authoring; not open for public purchase.</summary>
    Draft = 0,

    /// <summary>Public catalog; checkout allowed.</summary>
    Active = 1,

    /// <summary>Stopped; no new purchase.</summary>
    Inactive = 2
}
