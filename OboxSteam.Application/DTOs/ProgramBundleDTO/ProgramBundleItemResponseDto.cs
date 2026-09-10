namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

public sealed class ProgramBundleItemResponseDto
{
    public Guid Id { get; set; }

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public decimal ProgramPrice { get; set; }

    public int SortOrder { get; set; }

    public bool RequiresPreviousCompletion { get; set; }
}
