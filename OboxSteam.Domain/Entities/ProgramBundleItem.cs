namespace OboxSteam.Domain.Entities;

/// <summary>Ordered program membership inside a <see cref="ProgramBundle"/>.</summary>
public class ProgramBundleItem : BaseEntity
{
    public Guid BundleId { get; set; }
    public ProgramBundle Bundle { get; set; } = null!;

    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public int SortOrder { get; set; }

    /// <summary>
    /// When true, class enrollment for this program is blocked until the previous
    /// item's program enrollment is Completed. Default false (flexible order).
    /// </summary>
    public bool RequiresPreviousCompletion { get; set; }
}
