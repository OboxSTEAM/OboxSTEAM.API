using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class CurriculumChangeConsolidatorTests
{
    private static readonly Guid ProgramId = Guid.NewGuid();
    private static readonly Guid CourseA = Guid.NewGuid();
    private static readonly Guid CourseB = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);

    private static CurriculumChange Row(
        Guid targetId,
        long version,
        CurriculumChangeKind kind,
        IEnumerable<CurriculumFieldChange>? fields = null,
        Guid? parentBefore = null,
        Guid? parentAfter = null,
        int? orderBefore = null,
        int? orderAfter = null,
        string label = "Activity")
        => new()
        {
            Id = Guid.NewGuid(),
            ProgramId = ProgramId,
            Version = version,
            At = Start.AddMinutes(version),
            TargetType = ProgramAdvisoryTargetType.Activity,
            TargetId = targetId,
            ChangeKind = kind,
            FieldsJson = CurriculumChangeJson.SerializeFields(fields ?? []),
            ParentBefore = parentBefore,
            ParentAfter = parentAfter,
            ParentBeforeLabel = parentBefore == CourseB ? "Course B" : "Course A",
            ParentAfterLabel = parentAfter == CourseB ? "Course B" : "Course A",
            OrderBefore = orderBefore,
            OrderAfter = orderAfter,
            LabelSnapshot = label,
            PathSnapshotJson = CurriculumChangeJson.SerializePath(
            [
                new CurriculumPathSegment(ProgramAdvisoryTargetType.Program, ProgramId, "Program"),
                new CurriculumPathSegment(ProgramAdvisoryTargetType.Course, parentAfter ?? parentBefore ?? CourseA, "Course A"),
            ]),
        };

    private static CurriculumFieldChange Field(string key, object? before, object? after)
        => new()
        {
            FieldKey = key,
            Before = CurriculumChangeJson.ToNode(before),
            After = CurriculumChangeJson.ToNode(after),
        };

    [Fact]
    public void CreatedThenDeleted_IsOmitted()
    {
        var id = Guid.NewGuid();

        var items = CurriculumChangeConsolidator.Consolidate(
        [
            Row(id, 1, CurriculumChangeKind.Created, [Field("name", null, "Lab")], parentAfter: CourseA, orderAfter: 1),
            Row(id, 2, CurriculumChangeKind.Deleted, parentBefore: CourseA, orderBefore: 1),
        ]);

        Assert.Empty(items);
    }

    [Fact]
    public void CreatedThenUpdated_IsCreatedWithFinalValues()
    {
        var id = Guid.NewGuid();

        var item = Assert.Single(CurriculumChangeConsolidator.Consolidate(
        [
            Row(id, 1, CurriculumChangeKind.Created, [Field("durationMinutes", null, 90)], parentAfter: CourseA, orderAfter: 1),
            Row(id, 2, CurriculumChangeKind.Updated, [Field("durationMinutes", 90, 45)], CourseA, CourseA, 1, 1),
        ]));

        Assert.Equal(CurriculumChangeKind.Created, item.ChangeKind);
        var field = Assert.Single(item.Fields);
        Assert.Null(field.Before);
        Assert.Equal(45, field.After!.GetValue<int>());
    }

    [Fact]
    public void FieldReturnedToOriginal_IsDropped()
    {
        var id = Guid.NewGuid();

        var items = CurriculumChangeConsolidator.Consolidate(
        [
            Row(id, 1, CurriculumChangeKind.Updated, [Field("durationMinutes", 90, 45)], CourseA, CourseA, 1, 1),
            Row(id, 2, CurriculumChangeKind.Updated, [Field("durationMinutes", 45, 90)], CourseA, CourseA, 1, 1),
        ]);

        Assert.Empty(items);
    }

    [Fact]
    public void SeveralUpdates_KeepFirstBeforeAndLastAfter()
    {
        var id = Guid.NewGuid();

        var item = Assert.Single(CurriculumChangeConsolidator.Consolidate(
        [
            Row(id, 1, CurriculumChangeKind.Updated, [Field("durationMinutes", 90, 60)], CourseA, CourseA, 1, 1),
            Row(id, 3, CurriculumChangeKind.Updated, [Field("durationMinutes", 60, 45)], CourseA, CourseA, 1, 1),
        ]));

        Assert.Equal(CurriculumChangeKind.Updated, item.ChangeKind);
        var field = Assert.Single(item.Fields);
        Assert.Equal(90, field.Before!.GetValue<int>());
        Assert.Equal(45, field.After!.GetValue<int>());
        Assert.Equal(3, item.LastVersion);
    }

    [Fact]
    public void UpdatedThenDeleted_IsDeleted()
    {
        var id = Guid.NewGuid();

        var item = Assert.Single(CurriculumChangeConsolidator.Consolidate(
        [
            Row(id, 1, CurriculumChangeKind.Updated, [Field("name", "A", "B")], CourseA, CourseA, 1, 1),
            Row(id, 2, CurriculumChangeKind.Deleted, parentBefore: CourseA, orderBefore: 1),
        ]));

        Assert.Equal(CurriculumChangeKind.Deleted, item.ChangeKind);
        Assert.Empty(item.Fields);
    }

    [Fact]
    public void ParentChange_IsMoved()
    {
        var id = Guid.NewGuid();

        var item = Assert.Single(CurriculumChangeConsolidator.Consolidate(
        [
            Row(id, 1, CurriculumChangeKind.Moved, parentBefore: CourseA, parentAfter: CourseB, orderBefore: 2, orderAfter: 1),
        ]));

        Assert.Equal(CurriculumChangeKind.Moved, item.ChangeKind);
        Assert.True(item.HasPositionChange);
        Assert.Equal("Course A", item.ParentBeforeLabel);
        Assert.Equal("Course B", item.ParentAfterLabel);
    }

    [Fact]
    public void SiblingReorders_CollapseIntoParentItem()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var item = Assert.Single(CurriculumChangeConsolidator.Consolidate(
        [
            Row(first, 1, CurriculumChangeKind.Reordered, parentBefore: CourseA, parentAfter: CourseA, orderBefore: 1, orderAfter: 2, label: "First"),
            Row(second, 1, CurriculumChangeKind.Reordered, parentBefore: CourseA, parentAfter: CourseA, orderBefore: 2, orderAfter: 1, label: "Second"),
        ]));

        Assert.Equal(CurriculumChangeKind.Reordered, item.ChangeKind);
        Assert.Equal(CourseA, item.TargetId);
        Assert.Equal(ProgramAdvisoryTargetType.Course, item.TargetType);
        Assert.Equal(["Second", "First"], item.ReorderedChildren.Select(c => c.Label));
        Assert.Single(item.Path);
    }
}
