using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>
/// Student class picker for checkout. First purchase and Completed retakes list Open
/// Standard classes. Failed/Dropped rebuys list Open and InProgress classes with
/// stop-module eligibility.
/// </summary>
public sealed class RebuyClassCatalogDto
{
    public Guid ProgramId { get; set; }

    /// <summary>
    /// True when the latest closed purchase is Failed or Dropped (InProgress classes
    /// and stop-module rules apply). False for first purchase and Completed retakes (Open only).
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
