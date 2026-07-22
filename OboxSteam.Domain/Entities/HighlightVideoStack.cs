using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// A stack of up to four personal highlight video outputs for one student/class,
/// keyed by an optional strength description (empty string = no specification).
/// </summary>
public class HighlightVideoStack : BaseEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    /// <summary>
    /// Strength filter for this stack. Empty string means face-only (no LLM filter).
    /// </summary>
    [MaxLength(2000)]
    public string StrengthDescription { get; set; } = string.Empty;

    public ICollection<HighlightVideoItem> Items { get; set; } = new List<HighlightVideoItem>();
}
