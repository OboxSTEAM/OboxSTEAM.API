using System.Text.Json;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class CurriculumChangeRecorder : ICurriculumChangeRecorder
{
    public static readonly TimeSpan SessionIdleWindow = TimeSpan.FromMinutes(10);
    private const int MaxLabelLength = 500;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly List<NotificationCommand> _pendingNotifications = [];

    public CurriculumChangeRecorder(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _notificationPublisher = notificationPublisher;
    }

    public async Task FlushNotificationsAsync()
    {
        if (_pendingNotifications.Count == 0)
        {
            return;
        }

        var commands = _pendingNotifications.ToList();
        _pendingNotifications.Clear();
        await _notificationPublisher.PublishManyAsync(commands);
    }

    public void DiscardNotifications() => _pendingNotifications.Clear();

    private async Task QueueAdvisorNotificationAsync(Program program, User? actor, string? actorName)
    {
        if (program.AdvisorExpertId is not { } advisorExpertId)
        {
            return;
        }

        var advisor = await _unitOfWork.Experts.GetByIdAsync(advisorExpertId);
        if (advisor?.UserId is not { } advisorUserId || advisorUserId == actor?.Id)
        {
            return;
        }

        _pendingNotifications.Add(NotificationCatalog.CurriculumApprovalRevoked(
            advisorUserId,
            program.Id,
            actor?.Id,
            program.Name,
            actorName));
    }

    public async Task RecordAsync(IReadOnlyList<CurriculumEntryChange> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (CurriculumChangeScope.IsSuppressed || entries.Count == 0)
        {
            return;
        }

        var lookup = new EntityLookup(_unitOfWork, entries);
        var programsInFlux = entries
            .Where(e => e.Entity is Program && EffectiveState(e) is CurriculumEntryState.Added or CurriculumEntryState.Deleted)
            .Select(e => ((Program)e.Entity).Id)
            .ToHashSet();

        var drafts = new List<ChangeDraft>();
        foreach (var entry in entries)
        {
            var draft = await BuildDraftAsync(entry, lookup);
            if (draft != null && !programsInFlux.Contains(draft.ProgramId))
            {
                drafts.Add(draft);
            }
        }

        var effective = MergeWithinSave(drafts);
        if (effective.Count == 0)
        {
            return;
        }

        var actor = await ResolveActorAsync();
        var now = _currentTime.GetCurrentTime().ToUniversalTime();
        foreach (var group in effective.GroupBy(d => d.ProgramId))
        {
            await RecordProgramAsync(group.Key, group.ToList(), actor, now);
        }
    }

    private async Task RecordProgramAsync(Guid programId, List<ChangeDraft> drafts, User? actor, DateTime now)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null)
        {
            return;
        }

        var previousVersion = program.CurriculumVersion;
        var version = previousVersion + 1;
        program.CurriculumVersion = version;

        var actorName = actor == null ? null : DisplayName(actor);
        var rows = drafts.Select(d => ToRow(d, version, actor?.Id, actorName, now)).ToList();
        foreach (var row in rows)
        {
            await _unitOfWork.CurriculumChanges.AddAsync(row);
        }

        var approval = await _unitOfWork.ProgramApprovals.FirstOrDefaultAsync(
            a => a.ProgramId == program.Id && a.RevokedAt == null && !a.IsDeleted);
        if (approval != null || program.Status == ProgramStatus.Approved)
        {
            await RevokeApprovalAsync(program, approval, actor, actorName, now);
        }

        if (actor != null)
        {
            await UpsertSessionMessageAsync(program, actor, actorName, previousVersion, version, rows, now);
        }

        await _unitOfWork.Programs.Update(program);
    }

    /// <summary>
    /// Revokes the active approval. An Approved program returns to Draft; an Active program
    /// stays published (re-approval of live programs is out of scope). The advisor is
    /// notified once, after the save commits.
    /// </summary>
    private async Task RevokeApprovalAsync(
        Program program,
        ProgramApproval? approval,
        User? actor,
        string? actorName,
        DateTime now)
    {
        if (approval != null)
        {
            approval.RevokedAt = now;
            approval.RevokedByUserId = actor?.Id;
            approval.RevokeReason = ProgramApprovalRevokeReason.CurriculumEdited;
            await _unitOfWork.ProgramApprovals.Update(approval);
        }

        if (program.Status == ProgramStatus.Approved)
        {
            program.Status = ProgramStatus.Draft;
        }

        await QueueAdvisorNotificationAsync(program, actor, actorName);
        await DiscussionSystemMessageWriter.AddAsync(
            _unitOfWork,
            program,
            DiscussionSystemEventCode.ApprovalRevoked,
            new
            {
                approvalId = approval?.Id,
                reason = ProgramApprovalRevokeReason.CurriculumEdited.ToString(),
                actorName,
            },
            now,
            actor?.Id ?? Guid.Empty);
    }

    private async Task UpsertSessionMessageAsync(
        Program program,
        User actor,
        string? actorName,
        long previousVersion,
        long version,
        List<CurriculumChange> newRows,
        DateTime now)
    {
        var latest = _unitOfWork.ProgramAdvisoryDiscussionMessages
            .GetQueryable()
            .Where(m => m.ProgramId == program.Id
                        && m.SystemEventCode == DiscussionSystemEventCode.CurriculumUpdated
                        && m.CreatedBy == actor.Id)
            .OrderByDescending(m => m.Sequence)
            .FirstOrDefault();
        var payload = latest == null ? null : ParsePayload(latest.SystemEventPayloadJson);
        var extend = latest != null
                     && payload != null
                     && payload.ActorUserId == actor.Id
                     && now - (latest.EditedAt ?? latest.CreatedAt) < SessionIdleWindow
                     && !_unitOfWork.ProgramAdvisoryDiscussionMessages
                         .GetQueryable()
                         .Any(m => m.ProgramId == program.Id
                                   && m.AuthorUserId == actor.Id
                                   && m.Kind == DiscussionMessageKind.User
                                   && m.Sequence > latest.Sequence);

        var fromVersion = extend ? payload!.FromVersion : previousVersion;
        var earlierRows = await _unitOfWork.CurriculumChanges.GetAllAsync(
            c => c.ProgramId == program.Id && c.Version > fromVersion && c.Version < version);
        var changeCount = CurriculumChangeConsolidator.Consolidate(earlierRows.Concat(newRows)).Count;
        var nextPayload = new CurriculumUpdatedPayload
        {
            ActorUserId = actor.Id,
            ActorName = actorName,
            FromVersion = fromVersion,
            ToVersion = version,
            ChangeCount = changeCount,
        };

        if (extend)
        {
            latest!.SystemEventPayloadJson = JsonSerializer.Serialize(nextPayload, CurriculumChangeJson.Options);
            latest.EditedAt = now;
            latest.Sequence = DiscussionSystemMessageWriter.AllocateSequence(_unitOfWork, program);
            await _unitOfWork.ProgramAdvisoryDiscussionMessages.Update(latest);
            return;
        }

        await DiscussionSystemMessageWriter.AddAsync(
            _unitOfWork,
            program,
            DiscussionSystemEventCode.CurriculumUpdated,
            nextPayload,
            now,
            actor.Id);
    }

    private static CurriculumUpdatedPayload? ParsePayload(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<CurriculumUpdatedPayload>(json, CurriculumChangeJson.Options);

    private async Task<User?> ResolveActorAsync()
    {
        var id = _claimsService.GetCurrentUserId;
        return id == Guid.Empty ? null : await _unitOfWork.Users.GetByIdAsync(id);
    }

    private static string DisplayName(User user)
        => string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;

    private static CurriculumChange ToRow(ChangeDraft draft, long version, Guid? actorId, string? actorName, DateTime now)
    {
        var moved = draft.Kind == CurriculumChangeKind.Updated && draft.ParentBefore != draft.ParentAfter;
        var reordered = draft.Kind == CurriculumChangeKind.Updated
                        && draft.Fields.Count == 0
                        && draft.OrderBefore != draft.OrderAfter;
        return new CurriculumChange
        {
            Id = Guid.NewGuid(),
            ProgramId = draft.ProgramId,
            Version = version,
            ActorUserId = actorId,
            ActorName = Truncate(actorName, 255),
            At = now,
            TargetType = draft.TargetType,
            TargetId = draft.TargetId,
            ChangeKind = moved ? CurriculumChangeKind.Moved
                : reordered ? CurriculumChangeKind.Reordered
                : draft.Kind,
            FieldsJson = CurriculumChangeJson.SerializeFields(draft.Fields),
            ParentBefore = draft.ParentBefore,
            ParentAfter = draft.ParentAfter,
            ParentBeforeLabel = Truncate(draft.ParentBeforeLabel, MaxLabelLength),
            ParentAfterLabel = Truncate(draft.ParentAfterLabel, MaxLabelLength),
            OrderBefore = draft.OrderBefore,
            OrderAfter = draft.OrderAfter,
            LabelSnapshot = Truncate(draft.Label, MaxLabelLength) ?? string.Empty,
            PathSnapshotJson = CurriculumChangeJson.SerializePath(draft.Path),
            CreatedAt = now,
            CreatedBy = actorId ?? Guid.Empty,
        };
    }

    private static string? Truncate(string? value, int max)
        => value == null || value.Length <= max ? value : value[..max];

    // ---- Draft building ----

    private static CurriculumEntryState? EffectiveState(CurriculumEntryChange entry)
    {
        var entity = (BaseEntity)entry.Entity;
        var wasDeleted = entry.OriginalValues.TryGetValue(nameof(BaseEntity.IsDeleted), out var original)
                         && original is true;
        return entry.State switch
        {
            CurriculumEntryState.Added => entity.IsDeleted ? null : CurriculumEntryState.Added,
            CurriculumEntryState.Deleted => wasDeleted ? null : CurriculumEntryState.Deleted,
            _ when !wasDeleted && entity.IsDeleted => CurriculumEntryState.Deleted,
            _ when wasDeleted && !entity.IsDeleted => CurriculumEntryState.Added,
            _ when wasDeleted => null,
            _ => CurriculumEntryState.Modified,
        };
    }

    private static async Task<ChangeDraft?> BuildDraftAsync(CurriculumEntryChange entry, EntityLookup lookup)
    {
        var state = EffectiveState(entry);
        if (state == null)
        {
            return null;
        }

        return entry.Entity switch
        {
            ProgramSkill link => await BuildSkillDraftAsync(link, state.Value, lookup),
            ResearchMilestoneActivity link => await BuildMilestoneLinkDraftAsync(link, entry, state.Value, lookup),
            Program when state != CurriculumEntryState.Modified => null,
            _ => await BuildComponentDraftAsync(entry, state.Value, lookup),
        };
    }

    private static async Task<ChangeDraft?> BuildComponentDraftAsync(
        CurriculumEntryChange entry,
        CurriculumEntryState state,
        EntityLookup lookup)
    {
        var descriptor = CurriculumChangeFieldCatalog.GetDescriptor(entry.Entity.GetType());
        if (descriptor == null)
        {
            return null;
        }

        var entity = (BaseEntity)entry.Entity;
        var fields = new List<CurriculumFieldChange>();
        if (state != CurriculumEntryState.Deleted)
        {
            foreach (var field in descriptor.Fields)
            {
                var after = CurriculumChangeJson.ToNode(Current(entity, field.PropertyName));
                var before = state == CurriculumEntryState.Added
                    ? null
                    : CurriculumChangeJson.ToNode(Original(entry, field.PropertyName));
                if (!CurriculumChangeJson.NodesEqual(before, after))
                {
                    fields.Add(new CurriculumFieldChange { FieldKey = field.FieldKey, Before = before, After = after });
                }
            }
        }

        var (beforeParentType, beforeParentId) = ParentOf(entity, name => Original(entry, name));
        var (afterParentType, afterParentId) = ParentOf(entity, name => Current(entity, name));
        int? orderBefore = null;
        int? orderAfter = null;
        if (descriptor.OrderProperty != null)
        {
            orderBefore = Original(entry, descriptor.OrderProperty) as int?;
            orderAfter = Current(entity, descriptor.OrderProperty) as int?;
        }

        if (state == CurriculumEntryState.Added)
        {
            beforeParentId = null;
            orderBefore = null;
        }
        else if (state == CurriculumEntryState.Deleted)
        {
            afterParentId = null;
            orderAfter = null;
        }
        else if (fields.Count == 0 && beforeParentId == afterParentId && orderBefore == orderAfter)
        {
            return null;
        }

        var pathParentType = state == CurriculumEntryState.Deleted ? beforeParentType : afterParentType;
        var pathParentId = state == CurriculumEntryState.Deleted ? beforeParentId : afterParentId;
        List<CurriculumPathSegment> path = entity is Program
            ? []
            : await lookup.PathToAsync(pathParentType, pathParentId);
        var programId = entity is Program program
            ? program.Id
            : path.FirstOrDefault(s => s.TargetType == ProgramAdvisoryTargetType.Program)?.TargetId;
        if (programId == null)
        {
            return null;
        }

        return new ChangeDraft
        {
            ProgramId = programId.Value,
            TargetType = descriptor.TargetType,
            TargetId = entity.Id,
            Kind = state switch
            {
                CurriculumEntryState.Added => CurriculumChangeKind.Created,
                CurriculumEntryState.Deleted => CurriculumChangeKind.Deleted,
                _ => CurriculumChangeKind.Updated,
            },
            IsComponent = true,
            Fields = fields,
            ParentBefore = beforeParentId,
            ParentAfter = afterParentId,
            ParentBeforeLabel = await lookup.LabelOfAsync(beforeParentType, beforeParentId),
            ParentAfterLabel = await lookup.LabelOfAsync(afterParentType, afterParentId),
            OrderBefore = orderBefore,
            OrderAfter = orderAfter,
            Label = EntityLookup.LabelOf(entity),
            Path = path,
        };
    }

    private static async Task<ChangeDraft?> BuildSkillDraftAsync(
        ProgramSkill link,
        CurriculumEntryState state,
        EntityLookup lookup)
    {
        if (state == CurriculumEntryState.Modified)
        {
            return null;
        }

        var program = await lookup.GetAsync<Program>(link.ProgramId);
        if (program == null)
        {
            return null;
        }

        var skill = await lookup.GetAsync<Skill>(link.SkillId);
        var added = state == CurriculumEntryState.Added;
        return new ChangeDraft
        {
            ProgramId = program.Id,
            TargetType = ProgramAdvisoryTargetType.Program,
            TargetId = program.Id,
            Kind = CurriculumChangeKind.Updated,
            Fields =
            [
                new CurriculumFieldChange
                {
                    FieldKey = CurriculumChangeFieldCatalog.SkillFieldPrefix + link.SkillId,
                    Label = skill?.Name ?? link.SkillId.ToString(),
                    Before = CurriculumChangeJson.ToNode(!added),
                    After = CurriculumChangeJson.ToNode(added),
                },
            ],
            Label = program.Name,
            Path = [],
        };
    }

    private static async Task<ChangeDraft?> BuildMilestoneLinkDraftAsync(
        ResearchMilestoneActivity link,
        CurriculumEntryChange entry,
        CurriculumEntryState state,
        EntityLookup lookup)
    {
        var milestone = await lookup.GetAsync<ResearchMilestone>(link.ResearchMilestoneId);
        if (milestone == null)
        {
            return null;
        }

        var activity = await lookup.GetAsync<Activity>(link.ActivityId);
        var activityLabel = activity?.Name ?? link.ActivityId.ToString();
        CurriculumFieldChange field;
        if (state == CurriculumEntryState.Modified)
        {
            var before = Original(entry, nameof(ResearchMilestoneActivity.IsRequiredForSubmission)) as bool?;
            if (before == link.IsRequiredForSubmission)
            {
                return null;
            }

            field = new CurriculumFieldChange
            {
                FieldKey = CurriculumChangeFieldCatalog.ActivityLinkRequiredFieldPrefix + link.ActivityId,
                Label = activityLabel,
                Before = CurriculumChangeJson.ToNode(before),
                After = CurriculumChangeJson.ToNode(link.IsRequiredForSubmission),
            };
        }
        else
        {
            var added = state == CurriculumEntryState.Added;
            field = new CurriculumFieldChange
            {
                FieldKey = CurriculumChangeFieldCatalog.ActivityLinkFieldPrefix + link.ActivityId,
                Label = activityLabel,
                Before = CurriculumChangeJson.ToNode(!added),
                After = CurriculumChangeJson.ToNode(added),
            };
        }

        var path = await lookup.PathToAsync(ProgramAdvisoryTargetType.Module, milestone.ModuleId);
        var programId = path.FirstOrDefault(s => s.TargetType == ProgramAdvisoryTargetType.Program)?.TargetId;
        if (programId == null)
        {
            return null;
        }

        var moduleLabel = path[^1].Label;
        return new ChangeDraft
        {
            ProgramId = programId.Value,
            TargetType = ProgramAdvisoryTargetType.ResearchMilestone,
            TargetId = milestone.Id,
            Kind = CurriculumChangeKind.Updated,
            Fields = [field],
            ParentBefore = milestone.ModuleId,
            ParentAfter = milestone.ModuleId,
            ParentBeforeLabel = moduleLabel,
            ParentAfterLabel = moduleLabel,
            OrderBefore = milestone.MilestoneOrder,
            OrderAfter = milestone.MilestoneOrder,
            Label = milestone.Title,
            Path = path,
        };
    }

    private static (ProgramAdvisoryTargetType Type, Guid? Id) ParentOf(BaseEntity entity, Func<string, object?> value)
        => entity switch
        {
            Module => (ProgramAdvisoryTargetType.Program, value(nameof(Module.ProgramId)) as Guid?),
            Course => (ProgramAdvisoryTargetType.Module, value(nameof(Course.ModuleId)) as Guid?),
            Activity => (ProgramAdvisoryTargetType.Course, value(nameof(Activity.CourseId)) as Guid?),
            Material => (ProgramAdvisoryTargetType.Activity, value(nameof(Material.ActivityId)) as Guid?),
            ResearchMilestone => (ProgramAdvisoryTargetType.Module, value(nameof(ResearchMilestone.ModuleId)) as Guid?),
            Assignment when value(nameof(Assignment.CourseId)) is Guid courseId =>
                (ProgramAdvisoryTargetType.Course, courseId),
            Assignment => (ProgramAdvisoryTargetType.Module, value(nameof(Assignment.ModuleId)) as Guid?),
            _ => (ProgramAdvisoryTargetType.Program, null),
        };

    private static object? Current(object entity, string propertyName)
        => entity.GetType().GetProperty(propertyName)?.GetValue(entity);

    private static object? Original(CurriculumEntryChange entry, string propertyName)
        => entry.OriginalValues.TryGetValue(propertyName, out var value)
            ? value
            : Current(entry.Entity, propertyName);

    // ---- Merge within one save ----

    private static List<ChangeDraft> MergeWithinSave(List<ChangeDraft> drafts)
    {
        var merged = new List<ChangeDraft>();
        foreach (var group in drafts.GroupBy(d => (d.ProgramId, d.TargetType, d.TargetId)))
        {
            var items = group.ToList();
            var created = items.Any(d => d.Kind == CurriculumChangeKind.Created);
            var deleted = items.Any(d => d.Kind == CurriculumChangeKind.Deleted);
            if (created && deleted)
            {
                continue;
            }

            var primary = items.FirstOrDefault(d => d.IsComponent) ?? items[0];
            var fields = new List<CurriculumFieldChange>();
            var byKey = new Dictionary<string, CurriculumFieldChange>(StringComparer.Ordinal);
            foreach (var field in items.SelectMany(d => d.Fields))
            {
                if (byKey.TryGetValue(field.FieldKey, out var existing))
                {
                    existing.After = field.After;
                    continue;
                }

                byKey[field.FieldKey] = field;
                fields.Add(field);
            }

            var kind = created ? CurriculumChangeKind.Created
                : deleted ? CurriculumChangeKind.Deleted
                : CurriculumChangeKind.Updated;
            if (kind == CurriculumChangeKind.Updated)
            {
                fields = fields.Where(f => !CurriculumChangeJson.NodesEqual(f.Before, f.After)).ToList();
                if (fields.Count == 0
                    && primary.ParentBefore == primary.ParentAfter
                    && primary.OrderBefore == primary.OrderAfter)
                {
                    continue;
                }
            }
            else if (kind == CurriculumChangeKind.Deleted)
            {
                fields = [];
            }

            primary.Kind = kind;
            primary.Fields = fields;
            merged.Add(primary);
        }

        return DropImpliedSiblingShifts(DropCascadedDeletes(merged));
    }

    /// <summary>
    /// Deleting a component soft-deletes its descendants in the same save; only the topmost
    /// deleted component is recorded.
    /// </summary>
    private static List<ChangeDraft> DropCascadedDeletes(List<ChangeDraft> drafts)
    {
        var deletedIds = drafts
            .Where(d => d.Kind == CurriculumChangeKind.Deleted)
            .Select(d => d.TargetId)
            .ToHashSet();
        return drafts
            .Where(d => d.Kind != CurriculumChangeKind.Deleted
                        || !d.Path.Any(segment => deletedIds.Contains(segment.TargetId)))
            .ToList();
    }

    /// <summary>
    /// Inserting, deleting, or moving a component renumbers its siblings. Those pure order
    /// shifts are implied by the structural change and are not recorded separately.
    /// </summary>
    private static List<ChangeDraft> DropImpliedSiblingShifts(List<ChangeDraft> drafts)
    {
        var structuralParents = new HashSet<Guid>();
        foreach (var draft in drafts)
        {
            var structural = draft.Kind is CurriculumChangeKind.Created or CurriculumChangeKind.Deleted
                             || draft.ParentBefore != draft.ParentAfter;
            if (!structural)
            {
                continue;
            }

            if (draft.ParentBefore.HasValue)
            {
                structuralParents.Add(draft.ParentBefore.Value);
            }

            if (draft.ParentAfter.HasValue)
            {
                structuralParents.Add(draft.ParentAfter.Value);
            }
        }

        return drafts
            .Where(d => !(d.Kind == CurriculumChangeKind.Updated
                          && d.Fields.Count == 0
                          && d.ParentBefore == d.ParentAfter
                          && d.ParentAfter.HasValue
                          && structuralParents.Contains(d.ParentAfter.Value)))
            .ToList();
    }

    private sealed class ChangeDraft
    {
        public Guid ProgramId { get; init; }
        public ProgramAdvisoryTargetType TargetType { get; init; }
        public Guid TargetId { get; init; }
        public CurriculumChangeKind Kind { get; set; }
        public bool IsComponent { get; init; }
        public List<CurriculumFieldChange> Fields { get; set; } = [];
        public Guid? ParentBefore { get; init; }
        public Guid? ParentAfter { get; init; }
        public string? ParentBeforeLabel { get; init; }
        public string? ParentAfterLabel { get; init; }
        public int? OrderBefore { get; init; }
        public int? OrderAfter { get; init; }
        public string Label { get; init; } = string.Empty;
        public List<CurriculumPathSegment> Path { get; init; } = [];
    }

    /// <summary>
    /// Resolves entities by id, preferring instances pending in the current save (which may
    /// not exist in the database yet), then the database including soft-deleted rows.
    /// </summary>
    private sealed class EntityLookup
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Dictionary<(Type, Guid), object?> _entities = new();

        public EntityLookup(IUnitOfWork unitOfWork, IEnumerable<CurriculumEntryChange> entries)
        {
            _unitOfWork = unitOfWork;
            foreach (var entry in entries)
            {
                var entity = (BaseEntity)entry.Entity;
                _entities[(entity.GetType(), entity.Id)] = entity;
            }
        }

        public async Task<T?> GetAsync<T>(Guid id) where T : BaseEntity
        {
            if (_entities.TryGetValue((typeof(T), id), out var cached))
            {
                return cached as T;
            }

            var found = (await _unitOfWork.Repository<T>().GetAllIncludingDeletedAsync(e => e.Id == id))
                .FirstOrDefault();
            _entities[(typeof(T), id)] = found;
            return found;
        }

        public async Task<List<CurriculumPathSegment>> PathToAsync(ProgramAdvisoryTargetType type, Guid? id)
        {
            if (id == null)
            {
                return [];
            }

            switch (type)
            {
                case ProgramAdvisoryTargetType.Program:
                    var program = await GetAsync<Program>(id.Value);
                    return program == null ? [] : [Segment(type, program)];
                case ProgramAdvisoryTargetType.Module:
                    var module = await GetAsync<Module>(id.Value);
                    return module == null
                        ? []
                        : [.. await PathToAsync(ProgramAdvisoryTargetType.Program, module.ProgramId), Segment(type, module)];
                case ProgramAdvisoryTargetType.Course:
                    var course = await GetAsync<Course>(id.Value);
                    return course == null
                        ? []
                        : [.. await PathToAsync(ProgramAdvisoryTargetType.Module, course.ModuleId), Segment(type, course)];
                case ProgramAdvisoryTargetType.Activity:
                    var activity = await GetAsync<Activity>(id.Value);
                    return activity == null
                        ? []
                        : [.. await PathToAsync(ProgramAdvisoryTargetType.Course, activity.CourseId), Segment(type, activity)];
                default:
                    return [];
            }
        }

        public async Task<string?> LabelOfAsync(ProgramAdvisoryTargetType type, Guid? id)
        {
            if (id == null)
            {
                return null;
            }

            BaseEntity? entity = type switch
            {
                ProgramAdvisoryTargetType.Program => await GetAsync<Program>(id.Value),
                ProgramAdvisoryTargetType.Module => await GetAsync<Module>(id.Value),
                ProgramAdvisoryTargetType.Course => await GetAsync<Course>(id.Value),
                ProgramAdvisoryTargetType.Activity => await GetAsync<Activity>(id.Value),
                _ => null,
            };
            return entity == null ? null : LabelOf(entity);
        }

        public static string LabelOf(BaseEntity entity) => entity switch
        {
            Program program => program.Name,
            Module module => module.Name,
            Course course => course.Name,
            Activity activity => activity.Name,
            Assignment assignment => assignment.Title,
            ResearchMilestone milestone => milestone.Title,
            Material material => material.Title,
            _ => string.Empty,
        };

        private static CurriculumPathSegment Segment(ProgramAdvisoryTargetType type, BaseEntity entity)
            => new(type, entity.Id, LabelOf(entity));
    }
}
