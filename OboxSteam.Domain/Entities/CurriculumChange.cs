using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// One curriculum component change produced by a single save. All rows of one save share
/// <see cref="Version"/>, which equals the program's <see cref="Program.CurriculumVersion"/>
/// after that save.
/// </summary>
public sealed class CurriculumChange : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public long Version { get; set; }

    public Guid? ActorUserId { get; set; }
    [MaxLength(255)] public string? ActorName { get; set; }
    public DateTime At { get; set; }

    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public CurriculumChangeKind ChangeKind { get; set; }

    /// <summary>JSON array of <c>{ fieldKey, label?, before, after }</c> with typed values.</summary>
    public string FieldsJson { get; set; } = "[]";

    public Guid? ParentBefore { get; set; }
    public Guid? ParentAfter { get; set; }
    [MaxLength(500)] public string? ParentBeforeLabel { get; set; }
    [MaxLength(500)] public string? ParentAfterLabel { get; set; }
    public int? OrderBefore { get; set; }
    public int? OrderAfter { get; set; }

    [MaxLength(500)] public string LabelSnapshot { get; set; } = string.Empty;

    /// <summary>JSON array of ancestors <c>{ targetType, targetId, label }</c>, program first.</summary>
    public string PathSnapshotJson { get; set; } = "[]";
}
