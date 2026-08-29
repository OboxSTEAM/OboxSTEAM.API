using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>
/// Rebuy class picker for a student with a closed purchase on this program.
/// Lists Open and InProgress Standard classes that still have seats, with class-level module progress.
/// </summary>
public sealed class RebuyClassCatalogDto
{
    public Guid ProgramId { get; set; }

    public Guid SourceProgramEnrollmentId { get; set; }

    public EnrollmentStatus SourceStatus { get; set; }

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
