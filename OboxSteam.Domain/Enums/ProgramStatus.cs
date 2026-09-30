namespace OboxSteam.Domain.Enums;

/// <summary>
/// Catalog lifecycle for a program. Stored as text via EF string enum conversion.
/// Aligns with FE: Draft (bản nháp), Approved (đã duyệt, chờ publish),
/// Active (đang mở), Inactive (ngừng hoạt động).
/// </summary>
public enum ProgramStatus
{
    /// <summary>Manager is authoring; not open for public registration or purchase.</summary>
    Draft = 0,

    /// <summary>Public catalog; enrollment and payment allowed.</summary>
    Active = 1,

    /// <summary>Stopped; no new registration or purchase.</summary>
    Inactive = 2,

    /// <summary>Removed from the lifecycle; existing rows are migrated to Draft.</summary>
    [Obsolete("PendingReview was removed from the approval lifecycle. Programs stay Draft until the advisor approves.")]
    PendingReview = 3,

    /// <summary>
    /// The advisor approved the current curriculum version; ready for manager publish.
    /// Curriculum edits revoke the approval and return the program to Draft.
    /// </summary>
    Approved = 4,
}
