namespace OboxSteam.Domain.Enums;

/// <summary>
/// Catalog lifecycle for a program. Stored as text via EF string enum conversion.
/// Aligns with FE: Draft (bản nháp), Active (đang mở), Inactive (ngừng hoạt động).
/// </summary>
public enum ProgramStatus
{
    /// <summary>Not open for public registration or purchase.</summary>
    Draft = 0,

    /// <summary>Public catalog; enrollment and payment allowed.</summary>
    Active = 1,

    /// <summary>Stopped; no new registration or purchase.</summary>
    Inactive = 2,
}
