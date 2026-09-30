using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>
/// Folds raw change rows of a version range into one net item per component.
/// </summary>
public static class CurriculumChangeConsolidator
{
    public static List<ConsolidatedCurriculumChange> Consolidate(IEnumerable<CurriculumChange> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var items = rows
            .GroupBy(r => (r.TargetType, r.TargetId))
            .Select(g => ConsolidateTarget(g.OrderBy(r => r.Version).ThenBy(r => r.At).ToList()))
            .Where(item => item != null)
            .Select(item => item!)
            .ToList();

        return CollapseSiblingReorders(items);
    }

    private static ConsolidatedCurriculumChange? ConsolidateTarget(List<CurriculumChange> ordered)
    {
        var first = ordered[0];
        var last = ordered[^1];
        var createdInRange = first.ChangeKind == CurriculumChangeKind.Created;
        var deletedInRange = last.ChangeKind == CurriculumChangeKind.Deleted;
        if (createdInRange && deletedInRange)
        {
            return null;
        }

        var item = new ConsolidatedCurriculumChange
        {
            TargetType = last.TargetType,
            TargetId = last.TargetId,
            Label = last.LabelSnapshot,
            Path = CurriculumChangeJson.DeserializePath(last.PathSnapshotJson),
            ChangedBy = ordered
                .Select(r => new CurriculumChangeActor(r.ActorUserId, r.ActorName))
                .DistinctBy(a => a.UserId)
                .ToList(),
            LastChangedAt = ordered.Max(r => r.At),
            LastVersion = ordered.Max(r => r.Version),
        };
        var merged = MergeFields(ordered);

        if (createdInRange)
        {
            item.ChangeKind = CurriculumChangeKind.Created;
            item.Fields = merged
                .Where(f => f.After != null)
                .Select(f => new CurriculumFieldChange { FieldKey = f.FieldKey, Label = f.Label, After = f.After })
                .ToList();
            item.ParentAfter = last.ParentAfter;
            item.ParentAfterLabel = last.ParentAfterLabel;
            item.OrderAfter = last.OrderAfter;
            return item;
        }

        if (deletedInRange)
        {
            item.ChangeKind = CurriculumChangeKind.Deleted;
            item.ParentBefore = first.ParentBefore;
            item.ParentBeforeLabel = first.ParentBeforeLabel;
            item.OrderBefore = first.OrderBefore;
            return item;
        }

        item.Fields = merged.Where(f => !CurriculumChangeJson.NodesEqual(f.Before, f.After)).ToList();
        item.ParentBefore = first.ParentBefore;
        item.ParentAfter = last.ParentAfter;
        item.ParentBeforeLabel = first.ParentBeforeLabel;
        item.ParentAfterLabel = last.ParentAfterLabel;
        item.OrderBefore = first.OrderBefore;
        item.OrderAfter = last.OrderAfter;

        var moved = item.ParentBefore != item.ParentAfter;
        var reordered = item.OrderBefore != item.OrderAfter;
        if (moved)
        {
            item.ChangeKind = CurriculumChangeKind.Moved;
            item.HasPositionChange = true;
        }
        else if (item.Fields.Count > 0)
        {
            item.ChangeKind = CurriculumChangeKind.Updated;
            item.HasPositionChange = reordered;
        }
        else if (reordered)
        {
            item.ChangeKind = CurriculumChangeKind.Reordered;
            item.HasPositionChange = true;
        }
        else
        {
            return null;
        }

        return item;
    }

    private static List<CurriculumFieldChange> MergeFields(List<CurriculumChange> ordered)
    {
        var merged = new List<CurriculumFieldChange>();
        var byKey = new Dictionary<string, CurriculumFieldChange>(StringComparer.Ordinal);
        foreach (var row in ordered)
        {
            foreach (var field in CurriculumChangeJson.DeserializeFields(row.FieldsJson))
            {
                if (byKey.TryGetValue(field.FieldKey, out var existing))
                {
                    existing.After = field.After;
                    existing.Label = field.Label ?? existing.Label;
                    continue;
                }

                var copy = new CurriculumFieldChange
                {
                    FieldKey = field.FieldKey,
                    Label = field.Label,
                    Before = field.Before,
                    After = field.After,
                };
                byKey[field.FieldKey] = copy;
                merged.Add(copy);
            }
        }

        return merged;
    }

    /// <summary>
    /// Several pure reorders under the same parent become one Reordered item on the parent,
    /// attached to the parent's own item when the parent also changed.
    /// </summary>
    private static List<ConsolidatedCurriculumChange> CollapseSiblingReorders(List<ConsolidatedCurriculumChange> items)
    {
        var groups = items
            .Where(i => i.ChangeKind == CurriculumChangeKind.Reordered && i.ParentAfter.HasValue && i.Path.Count > 0)
            .GroupBy(i => i.ParentAfter!.Value)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            var children = group.OrderBy(c => c.OrderAfter ?? int.MaxValue).ToList();
            foreach (var child in children)
            {
                items.Remove(child);
            }

            var parentSegment = children[0].Path[^1];
            var parent = items.FirstOrDefault(i => i.TargetId == group.Key);
            if (parent == null)
            {
                parent = new ConsolidatedCurriculumChange
                {
                    TargetType = parentSegment.TargetType,
                    TargetId = parentSegment.TargetId,
                    Label = parentSegment.Label,
                    Path = children[0].Path.Take(children[0].Path.Count - 1).ToList(),
                    ChangeKind = CurriculumChangeKind.Reordered,
                };
                items.Add(parent);
            }

            parent.ReorderedChildren = children
                .Select(c => new ReorderedCurriculumChild(c.TargetType, c.TargetId, c.Label, c.OrderBefore, c.OrderAfter))
                .ToList();
            parent.ChangedBy = parent.ChangedBy
                .Concat(children.SelectMany(c => c.ChangedBy))
                .DistinctBy(a => a.UserId)
                .ToList();
            parent.LastChangedAt = children.Select(c => c.LastChangedAt).Append(parent.LastChangedAt).Max();
            parent.LastVersion = children.Select(c => c.LastVersion).Append(parent.LastVersion).Max();
        }

        return items;
    }
}
