using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>Net change of one curriculum component across a version range.</summary>
public sealed class ConsolidatedCurriculumChange
{
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<CurriculumPathSegment> Path { get; set; } = [];
    public CurriculumChangeKind ChangeKind { get; set; }
    public List<CurriculumFieldChange> Fields { get; set; } = [];

    public Guid? ParentBefore { get; set; }
    public Guid? ParentAfter { get; set; }
    public string? ParentBeforeLabel { get; set; }
    public string? ParentAfterLabel { get; set; }
    public int? OrderBefore { get; set; }
    public int? OrderAfter { get; set; }

    /// <summary>True when the item carries a parent or order transition worth showing.</summary>
    public bool HasPositionChange { get; set; }

    public List<ReorderedCurriculumChild> ReorderedChildren { get; set; } = [];
    public List<CurriculumChangeActor> ChangedBy { get; set; } = [];
    public DateTime LastChangedAt { get; set; }
    public long LastVersion { get; set; }

    /// <summary>Version and actor of every raw row folded into this item.</summary>
    public List<CurriculumChangeEdit> Edits { get; set; } = [];

    /// <summary>
    /// True when someone other than the viewer changed the item after the viewer's seen version;
    /// the viewer's own edits never count as unseen.
    /// </summary>
    public bool IsUnseenBy(Guid viewerId, long seenVersion)
        => Edits.Any(e => e.Version > seenVersion && e.ActorUserId != viewerId);
}
