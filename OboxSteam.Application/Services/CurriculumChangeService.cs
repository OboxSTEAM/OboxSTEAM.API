using System.Globalization;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Application.DTOs.CurriculumChangeDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class CurriculumChangeService : ICurriculumChangeService
{
    private const string VersionBasePrefix = "version:";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;

    public CurriculumChangeService(IUnitOfWork unitOfWork, IClaimsService claimsService, ICurrentTime currentTime)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
    }

    public async Task<CurriculumChangesDto> GetChangesAsync(Guid programId, string? baseSpec, long? toVersion)
    {
        var (program, actor) = await AdvisoryParticipantGuard.RequireAsync(_unitOfWork, _claimsService, programId);
        var currentVersion = program.CurriculumVersion;
        var to = toVersion ?? currentVersion;
        if (to < 0 || to > currentVersion)
        {
            throw ErrorHelper.BadRequest($"'to' must be between 0 and the current curriculum version ({currentVersion}).");
        }

        var seen = await _unitOfWork.CurriculumChangeSeens.FirstOrDefaultAsync(
            s => s.ProgramId == programId && s.UserId == actor.Id && !s.IsDeleted);
        var seenVersion = seen?.SeenVersion ?? 0;
        var from = await ResolveBaseVersionAsync(programId, baseSpec, seenVersion, currentVersion);
        if (from > to)
        {
            throw ErrorHelper.BadRequest("The base version must not be greater than the 'to' version.");
        }

        var rows = await _unitOfWork.CurriculumChanges.GetAllAsync(
            c => c.ProgramId == programId && c.Version > from && c.Version <= to && !c.IsDeleted);
        var consolidated = CurriculumChangeConsolidator.Consolidate(rows);
        var treeOrder = BuildTreeOrder(await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId));
        var items = consolidated
            .OrderBy(item => SortKey(item, treeOrder))
            .ThenBy(item => item.LastChangedAt)
            .Select(item => ToDto(item, actor.Id, seenVersion))
            .ToList();

        return new CurriculumChangesDto
        {
            FromVersion = from,
            ToVersion = to,
            CurrentVersion = currentVersion,
            SeenVersion = seenVersion,
            Summary = new CurriculumChangeSummaryDto
            {
                Created = consolidated.Count(i => i.ChangeKind == CurriculumChangeKind.Created),
                Updated = consolidated.Count(i => i.ChangeKind == CurriculumChangeKind.Updated),
                Deleted = consolidated.Count(i => i.ChangeKind == CurriculumChangeKind.Deleted),
                Moved = consolidated.Count(i => i.ChangeKind is CurriculumChangeKind.Moved or CurriculumChangeKind.Reordered),
            },
            Items = items,
        };
    }

    public async Task MarkSeenAsync(Guid programId, MarkCurriculumChangesSeenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (program, actor) = await AdvisoryParticipantGuard.RequireAsync(_unitOfWork, _claimsService, programId);
        if (request.Version < 0 || request.Version > program.CurriculumVersion)
        {
            throw ErrorHelper.BadRequest(
                $"Version must be between 0 and the current curriculum version ({program.CurriculumVersion}).");
        }

        var seen = await _unitOfWork.CurriculumChangeSeens.FirstOrDefaultAsync(
            s => s.ProgramId == programId && s.UserId == actor.Id && !s.IsDeleted);
        if (seen == null)
        {
            await _unitOfWork.CurriculumChangeSeens.AddAsync(new CurriculumChangeSeen
            {
                Id = Guid.NewGuid(),
                ProgramId = programId,
                UserId = actor.Id,
                SeenVersion = request.Version,
                CreatedAt = _currentTime.GetCurrentTime().ToUniversalTime(),
                CreatedBy = actor.Id,
            });
        }
        else if (request.Version > seen.SeenVersion)
        {
            seen.SeenVersion = request.Version;
            await _unitOfWork.CurriculumChangeSeens.Update(seen);
        }
        else
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<long> ResolveBaseVersionAsync(Guid programId, string? baseSpec, long seenVersion, long currentVersion)
    {
        var spec = string.IsNullOrWhiteSpace(baseSpec) ? "lastApproval" : baseSpec.Trim();
        if (spec.Equals("lastApproval", StringComparison.OrdinalIgnoreCase))
        {
            var approvals = await _unitOfWork.ProgramApprovals.GetAllAsync(
                a => a.ProgramId == programId && !a.IsDeleted);
            return approvals.OrderByDescending(a => a.ApprovedAt).FirstOrDefault()?.CurriculumVersion ?? 0;
        }

        if (spec.Equals("lastSeen", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Min(seenVersion, currentVersion);
        }

        if (spec.Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (spec.StartsWith(VersionBasePrefix, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(spec[VersionBasePrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            && version <= currentVersion)
        {
            return version;
        }

        throw ErrorHelper.BadRequest(
            "Invalid 'base'. Use lastApproval, lastSeen, start, or version:N with N not above the current version.");
    }

    private static CurriculumChangeItemDto ToDto(ConsolidatedCurriculumChange item, Guid viewerId, long seenVersion)
        => new()
        {
            TargetType = item.TargetType,
            TargetId = item.TargetId,
            Label = item.Label,
            Path = item.Path
                .Select(s => new CurriculumPathSegmentDto { TargetType = s.TargetType, TargetId = s.TargetId, Label = s.Label })
                .ToList(),
            ChangeKind = item.ChangeKind,
            Fields = item.Fields.Select(f => ToFieldDto(item.TargetType, f)).ToList(),
            Moved = item.HasPositionChange
                ? new CurriculumChangeMoveDto
                {
                    FromParentLabel = item.ParentBeforeLabel,
                    ToParentLabel = item.ParentAfterLabel,
                    FromOrder = item.OrderBefore,
                    ToOrder = item.OrderAfter,
                }
                : null,
            ReorderedChildren = item.ReorderedChildren
                .Select(c => new CurriculumReorderedChildDto
                {
                    TargetType = c.TargetType,
                    TargetId = c.TargetId,
                    Label = c.Label,
                    FromOrder = c.FromOrder,
                    ToOrder = c.ToOrder,
                })
                .ToList(),
            ChangedBy = item.ChangedBy
                .Select(a => new CurriculumChangeActorDto { UserId = a.UserId, Name = a.Name })
                .ToList(),
            LastChangedAt = item.LastChangedAt,
            IsUnseen = item.IsUnseenBy(viewerId, seenVersion),
        };

    private static CurriculumChangeFieldDto ToFieldDto(ProgramAdvisoryTargetType targetType, CurriculumFieldChange field)
    {
        var (catalogLabel, valueType) = CurriculumChangeFieldCatalog.Describe(targetType, field.FieldKey);
        return new CurriculumChangeFieldDto
        {
            FieldKey = field.FieldKey,
            Label = catalogLabel ?? DynamicLabel(field),
            ValueType = valueType,
            Before = field.Before?.DeepClone(),
            After = field.After?.DeepClone(),
        };
    }

    private static string DynamicLabel(CurriculumFieldChange field)
    {
        var name = field.Label ?? string.Empty;
        if (field.FieldKey.StartsWith(CurriculumChangeFieldCatalog.SkillFieldPrefix, StringComparison.Ordinal))
        {
            return $"Skill: {name}";
        }

        if (field.FieldKey.StartsWith(CurriculumChangeFieldCatalog.ActivityLinkRequiredFieldPrefix, StringComparison.Ordinal))
        {
            return $"Required before submission: {name}";
        }

        if (field.FieldKey.StartsWith(CurriculumChangeFieldCatalog.ActivityLinkFieldPrefix, StringComparison.Ordinal))
        {
            return $"Linked activity: {name}";
        }

        return field.FieldKey;
    }

    /// <summary>
    /// Current tree position; deleted components sort right after their deepest surviving
    /// ancestor.
    /// </summary>
    private static double SortKey(ConsolidatedCurriculumChange item, Dictionary<Guid, int> treeOrder)
    {
        if (treeOrder.TryGetValue(item.TargetId, out var position))
        {
            return position;
        }

        for (var index = item.Path.Count - 1; index >= 0; index--)
        {
            if (treeOrder.TryGetValue(item.Path[index].TargetId, out var ancestor))
            {
                return ancestor + 0.5;
            }
        }

        return double.MaxValue;
    }

    private static Dictionary<Guid, int> BuildTreeOrder(ProgramCurriculumTreeSnapshot tree)
    {
        var order = new Dictionary<Guid, int>();
        void Add(Guid id) => order.TryAdd(id, order.Count);

        Add(tree.Program.Id);
        foreach (var module in tree.Modules)
        {
            Add(module.Id);
            foreach (var course in tree.CoursesByModuleId.GetValueOrDefault(module.Id, []).OrderBy(c => c.CourseOrder))
            {
                Add(course.Id);
                foreach (var activity in tree.ActivitiesByCourseId.GetValueOrDefault(course.Id, []).OrderBy(a => a.ActivityOrder))
                {
                    Add(activity.Id);
                    if (tree.MaterialsByActivityId.TryGetValue(activity.Id, out var material))
                    {
                        Add(material.Id);
                    }
                }

                foreach (var assignment in tree.AssignmentsByCourseId.GetValueOrDefault(course.Id, []))
                {
                    Add(assignment.Id);
                }
            }

            foreach (var milestone in tree.MilestonesByModuleId.GetValueOrDefault(module.Id, []).OrderBy(m => m.MilestoneOrder))
            {
                Add(milestone.Id);
                foreach (var link in tree.LinksByMilestoneId.GetValueOrDefault(milestone.Id, []).OrderBy(l => l.DisplayOrder))
                {
                    Add(link.ActivityId);
                    if (tree.MaterialsByActivityId.TryGetValue(link.ActivityId, out var material))
                    {
                        Add(material.Id);
                    }
                }

                Add(milestone.AssignmentId);
            }

            foreach (var assignment in tree.ModuleScopedAssignmentsByModuleId.GetValueOrDefault(module.Id, []))
            {
                Add(assignment.Id);
            }
        }

        return order;
    }
}
