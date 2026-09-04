using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>
/// Student class picker for checkout. First purchase, Completed retakes, and
/// Failed/Dropped after the 3-month window list Open Standard classes.
/// Failed/Dropped inside the window list Open and InProgress classes with
/// stop-module eligibility.
/// </summary>
public sealed class RebuyClassCatalogDto
{
    public Guid ProgramId { get; set; }

    /// <summary>
    /// True when the latest closed purchase is Failed or Dropped and still inside
    /// the 3-month window (InProgress classes and stop-module rules apply). False for
    /// first purchase, Completed retakes, and fail/drop after the window (Open only).
    /// </summary>
    public bool IsRebuy { get; set; }

    public Guid? SourceProgramEnrollmentId { get; set; }

    public EnrollmentStatus? SourceStatus { get; set; }

    public ProgramPurchaseEndReason? SourceEndReason { get; set; }

    public Guid? StopModuleId { get; set; }

    public string? StopModuleCode { get; set; }

    public string? StopModuleName { get; set; }

    public int? StopModuleOrder { get; set; }

    public bool WithinRebuyWindow { get; set; }

    /// <summary><c>RetakeFee ?? Price</c> inside the window; full <c>Price</c> after.</summary>
    public decimal CheckoutAmount { get; set; }

    public List<RebuyClassDto> Classes { get; set; } = [];
}
