namespace OboxSteam.Domain.Enums;

/// <summary>
/// Catalog lifecycle for a program. Stored as text via EF string enum conversion.
/// Aligns with FE: Draft (bản nháp), PendingReview (chờ expert duyệt),
/// Approved (đã duyệt, chờ publish), Active (đang mở), Inactive (ngừng hoạt động).
/// </summary>
public enum ProgramStatus
{
    /// <summary>Manager is authoring; not open for public registration or purchase.</summary>
    Draft = 0,

    /// <summary>Public catalog; enrollment and payment allowed.</summary>
    Active = 1,

    /// <summary>Stopped; no new registration or purchase.</summary>
    Inactive = 2,

    /// <summary>Submitted for expert review; curriculum edits lock once review APIs land.</summary>
    PendingReview = 3,

    /// <summary>Expert approved; waiting for manager publish before classes can be created.</summary>
    Approved = 4,
}
