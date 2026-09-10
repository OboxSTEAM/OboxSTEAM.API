namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

public sealed class OwnedProgramDeductionDto
{
    public Guid ProgramId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Retail program price subtracted from the bundle list price.</summary>
    public decimal DeductedPrice { get; set; }
}
