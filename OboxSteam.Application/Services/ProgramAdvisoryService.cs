using System.Text.Json;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ProgramAdvisoryService : IProgramAdvisoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IBlobService _blobService;
    private readonly IAdvisoryReferenceResolver? _referenceResolver;

    public ProgramAdvisoryService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        INotificationPublisher notificationPublisher,
        IBlobService blobService,
        IAdvisoryReferenceResolver? referenceResolver = null)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _notificationPublisher = notificationPublisher;
        _blobService = blobService;
        _referenceResolver = referenceResolver;
    }

    public async Task<Pagination<AdvisoryMineItemDto>> GetAdvisoryMineAsync(
        int page,
        int pageSize,
        ProgramStatus? status = null,
        bool unreadOnly = false)
    {
        var actor = await ResolveActorAsync();
        Expert? expert = null;
        if (actor.Role == RoleType.Expert)
        {
            expert = await RequireCurrentExpertAsync(actor);
        }

        var programs = await _unitOfWork.Programs.GetAllAsync(p => !p.IsDeleted);
        if (status.HasValue)
        {
            programs = programs.Where(p => p.Status == status.Value).ToList();
        }

        if (expert != null)
        {
            var boardProgramIds = (await _unitOfWork.ProgramBoards.GetAllAsync(
                    b => b.ExpertId == expert.Id && !b.IsDeleted))
                .Select(b => b.ProgramId)
                .ToHashSet();
            programs = programs
                .Where(p => p.AdvisorExpertId == expert.Id || boardProgramIds.Contains(p.Id))
                .ToList();
        }

        var programIds = programs.Select(p => p.Id).ToList();
        var versionIds = programs
            .Where(p => p.FrameworkVersionId.HasValue)
            .Select(p => p.FrameworkVersionId!.Value)
            .Distinct()
            .ToList();
        var versionsById = versionIds.Count == 0
            ? new Dictionary<Guid, ProgramFrameworkVersion>()
            : (await _unitOfWork.ProgramFrameworkVersions.GetAllAsync(v => versionIds.Contains(v.Id)))
                .ToDictionary(v => v.Id);

        var threads = programIds.Count == 0
            ? []
            : await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
                t => programIds.Contains(t.ProgramId) && !t.IsDeleted);
        var threadsByProgram = threads
            .GroupBy(t => t.ProgramId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var reads = await _unitOfWork.ProgramAdvisoryReads.GetAllAsync(
            r => r.UserId == actor.Id && programIds.Contains(r.ProgramId) && !r.IsDeleted);
        var readByProgram = reads.ToDictionary(r => r.ProgramId);

        var submissions = programIds.Count == 0
            ? []
            : await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
                s => programIds.Contains(s.ProgramId)
                     && s.Status == ProgramReviewSubmissionStatus.Pending
                     && !s.IsDeleted);
        var pendingByProgram = submissions
            .GroupBy(s => s.ProgramId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SubmissionNumber).First());

        var items = new List<AdvisoryMineItemDto>();
        foreach (var program in programs)
        {
            threadsByProgram.TryGetValue(program.Id, out var programThreads);
            programThreads ??= [];
            readByProgram.TryGetValue(program.Id, out var read);
            var lastRead = read?.LastReadAt;
            var unreadCount = programThreads.Count(t => lastRead == null || t.LastMessageAt > lastRead);
            if (unreadOnly && unreadCount == 0)
            {
                continue;
            }

            int? versionNumber = null;
            if (program.FrameworkVersionId.HasValue
                && versionsById.TryGetValue(program.FrameworkVersionId.Value, out var version))
            {
                versionNumber = version.VersionNumber;
            }

            var latestActivity = programThreads.Count == 0
                ? program.UpdatedAt ?? program.CreatedAt
                : programThreads.Max(t => t.LastMessageAt);

            var isAdvisor = expert != null && program.AdvisorExpertId == expert.Id;
            var canStaff = actor.Role is RoleType.Manager or RoleType.Admin;
            items.Add(new AdvisoryMineItemDto
            {
                ProgramId = program.Id,
                Code = program.Code,
                Name = program.Name,
                IsAdvisor = isAdvisor || canStaff,
                FrameworkVersionNumber = versionNumber,
                Status = program.Status,
                LatestActivityAt = latestActivity,
                NextAction = ResolveNextAction(program, isAdvisor, canStaff, programThreads, pendingByProgram).Code,
                UnreadFeedbackCount = unreadCount,
            });
        }

        var ordered = items
            .OrderByDescending(i => i.LatestActivityAt ?? DateTime.MinValue)
            .ThenBy(i => i.Name)
            .ToList();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new Pagination<AdvisoryMineItemDto>(pageItems, ordered.Count, page, pageSize);
    }

    public async Task<ProgramAdvisoryWorkspaceDto> GetAdvisoryWorkspaceAsync(Guid programId)
    {
        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        await EnsureGeneralThreadAsync(program, actor);
        var participants = await BuildParticipantsAsync(program);
        var threads = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == program.Id && !t.IsDeleted);
        var counts = BuildFeedbackCounts(threads);

        var allSubmissions = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
            s => s.ProgramId == program.Id && !s.IsDeleted);
        var latestSubmission = allSubmissions
            .OrderByDescending(s => s.SubmissionNumber)
            .FirstOrDefault();
        var pendingSubmission = allSubmissions
            .Where(s => s.Status == ProgramReviewSubmissionStatus.Pending)
            .OrderByDescending(s => s.SubmissionNumber)
            .FirstOrDefault();
        var workflow = await BuildWorkflowTimelineAsync(
            program, isAdvisor, canStaff, allSubmissions, threads);

        var read = await _unitOfWork.ProgramAdvisoryReads.FirstOrDefaultAsync(
            r => r.ProgramId == program.Id && r.UserId == actor.Id && !r.IsDeleted);
        var threadReads = await _unitOfWork.ProgramAdvisoryStreamReads.GetAllAsync(
            r => r.ProgramId == program.Id
                 && r.UserId == actor.Id
                 && r.StreamType == AdvisoryStreamType.Thread
                 && !r.IsDeleted);
        var openRequired = threads.Count(t =>
            t.Type == ProgramAdvisoryThreadType.RequiredChange
            && t.Status != ProgramAdvisoryThreadStatus.Resolved);
        var fixedRequired = threads.Count(t =>
            t.Type == ProgramAdvisoryThreadType.RequiredChange
            && t.Status == ProgramAdvisoryThreadStatus.Addressed);
        var capabilities = BuildCapabilities(program, actor, isAdvisor, isBoard, canStaff);
        var reviewActionsLocked = program.Status is ProgramStatus.Approved or ProgramStatus.Active or ProgramStatus.Inactive;
        var unreadNoteCount = threads.Count(t =>
        {
            if (t.Type == ProgramAdvisoryThreadType.General && t.LatestActivitySequence == 0)
            {
                return false;
            }

            var threadRead = threadReads.FirstOrDefault(r => r.ThreadId == t.Id)?.LastReadSequence;
            return t.LatestActivitySequence > 0
                ? (!threadRead.HasValue || threadRead.Value < t.LatestActivitySequence)
                : (read == null || t.LastMessageAt > read.LastReadAt);
        });

        int? versionNumber = null;
        if (program.FrameworkVersionId.HasValue)
        {
            var version = await _unitOfWork.ProgramFrameworkVersions.GetByIdAsync(program.FrameworkVersionId.Value);
            versionNumber = version?.VersionNumber;
        }

        string? advisorName = null;
        if (program.AdvisorExpertId.HasValue)
        {
            var advisor = await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value);
            advisorName = advisor?.FullName;
        }

        var hasUnread = threads.Any(t => read == null || t.LastMessageAt > read.LastReadAt);

        return new ProgramAdvisoryWorkspaceDto
        {
            ProgramId = program.Id,
            Code = program.Code,
            Name = program.Name,
            Status = program.Status,
            AdvisorExpertId = program.AdvisorExpertId,
            AdvisorName = advisorName,
            FrameworkVersionId = program.FrameworkVersionId,
            FrameworkVersionNumber = versionNumber,
            Participants = participants,
            Capabilities = capabilities,
            Workflow = workflow,
            OutstandingRequiredCount = openRequired,
            FixedRequiredCount = fixedRequired,
            OpenRequiredChangeCount = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange
                && t.Status == ProgramAdvisoryThreadStatus.Open),
            AddressedRequiredChangeCount = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange
                && t.Status == ProgramAdvisoryThreadStatus.Addressed),
            UnreadNoteCount = unreadNoteCount,
            PendingSubmission = pendingSubmission == null ? null : MapSubmissionSummary(pendingSubmission),
            ReviewActionsLocked = reviewActionsLocked,
            LatestSubmission = latestSubmission == null
                ? null
                : new ProgramReviewSubmissionSummaryDto
                {
                    Id = latestSubmission.Id,
                    SubmissionNumber = latestSubmission.SubmissionNumber,
                    Status = latestSubmission.Status,
                    ReviewRoundIntent = latestSubmission.ReviewRoundIntent,
                    AssignedAdvisorExpertId = latestSubmission.AssignedAdvisorExpertId,
                    FrameworkVersionId = latestSubmission.FrameworkVersionId,
                    SubmittedAt = latestSubmission.SubmittedAt,
                    ClosedAt = latestSubmission.ClosedAt,
                    ConcurrencyVersion = latestSubmission.ConcurrencyVersion,
                },
            FeedbackCounts = counts,
            HasUnreadFeedback = hasUnread,
        };
    }

    public async Task<IReadOnlyList<AdvisoryThreadDto>> GetThreadsAsync(
        Guid programId,
        Guid? submissionId = null,
        ProgramAdvisoryTargetType? targetType = null,
        Guid? targetId = null,
        ProgramAdvisoryThreadStatus? status = null,
        ProgramAdvisoryThreadType? type = null,
        string? scope = null)
    {
        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        var generalThread = await EnsureGeneralThreadAsync(program, actor);
        var normalizedScope = scope?.Trim().ToLowerInvariant();
        if (normalizedScope is not (null or "round" or "outstanding" or "program"))
        {
            throw ErrorHelper.BadRequest("Scope must be round, outstanding, or program.");
        }

        if (normalizedScope == "round" && !submissionId.HasValue)
        {
            throw ErrorHelper.BadRequest("SubmissionId is required when scope=round.");
        }

        if (submissionId.HasValue)
        {
            await RequireSubmissionAsync(programId, submissionId.Value);
        }

        var threads = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == programId
                 && (normalizedScope == "program"
                     || (normalizedScope == "outstanding"
                         && t.Type == ProgramAdvisoryThreadType.RequiredChange
                         && t.Status != ProgramAdvisoryThreadStatus.Resolved)
                     || ((normalizedScope == null || normalizedScope == "round")
                         && (!submissionId.HasValue || t.SubmissionId == submissionId.Value)))
                 && (!targetType.HasValue || t.TargetType == targetType.Value)
                 && (!targetId.HasValue || t.TargetId == targetId.Value)
                 && (!status.HasValue || t.Status == status.Value)
                 && (!type.HasValue || t.Type == type.Value)
                 && !t.IsDeleted);
        var includeGeneral = (type is null || type == ProgramAdvisoryThreadType.General)
            && (status is null || status == ProgramAdvisoryThreadStatus.Open)
            && (targetType is null || targetType == ProgramAdvisoryTargetType.Program)
            && (!targetId.HasValue || targetId == program.Id)
            && normalizedScope is null or "program";
        if (includeGeneral && threads.All(t => t.Id != generalThread.Id))
        {
            threads.Add(generalThread);
        }

        var ordered = threads
            .OrderByDescending(t => t.Type == ProgramAdvisoryThreadType.General)
            .ThenByDescending(t => t.LastMessageAt)
            .ThenBy(t => t.Id)
            .ToList();
        if (ordered.Count == 0)
        {
            return [];
        }

        var threadIds = ordered.Select(t => t.Id).ToList();
        var messages = await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
            m => threadIds.Contains(m.ThreadId) && !m.IsDeleted);
        var events = await _unitOfWork.ProgramAdvisoryThreadEvents.GetAllAsync(
            e => threadIds.Contains(e.ThreadId) && !e.IsDeleted);
        var counts = messages.GroupBy(m => m.ThreadId).ToDictionary(g => g.Key, g => g.Count());
        var authors = await LoadUsersAsync(ordered.Select(t => t.AuthorUserId));
        var submissionIds = ordered
            .Where(t => t.SubmissionId.HasValue)
            .Select(t => t.SubmissionId!.Value)
            .Distinct()
            .ToList();
        var submissionsById = submissionIds.Count == 0
            ? new Dictionary<Guid, ProgramReviewSubmission>()
            : (await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
                    s => submissionIds.Contains(s.Id) && s.ProgramId == programId && !s.IsDeleted))
                .ToDictionary(s => s.Id);
        var snapshotsBySubmissionId = submissionsById.ToDictionary(
            pair => pair.Key,
            pair => CurriculumReviewSnapshotBuilder.TryDeserialize(pair.Value.CurriculumSnapshotJson));
        var liveTargets = await LoadLiveTargetIndexAsync(program);

        return ordered
            .Select(t =>
            {
                var latest = messages
                    .Where(m => m.ThreadId == t.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();
                snapshotsBySubmissionId.TryGetValue(t.SubmissionId ?? Guid.Empty, out var snapshot);
                var target = ResolveSnapshotTarget(snapshot, t);
                var dto = MapThread(
                    t,
                    authors.GetValueOrDefault(t.AuthorUserId),
                    counts.GetValueOrDefault(t.Id),
                    latest?.Message,
                    target.Label,
                    target.Context);
                ApplyOriginRound(dto, t, submissionsById);
                ApplyLiveTarget(dto, t, liveTargets);
                dto.Events = events
                    .Where(e => e.ThreadId == t.Id)
                    .OrderBy(e => e.Sequence)
                    .Select(MapThreadEvent)
                    .ToList();
                ApplyThreadCapabilities(dto, t, actor, isAdvisor, isBoard, canStaff, program.Status);
                return dto;
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AdvisoryThreadPinSummaryDto>> GetPinSummariesAsync(
        Guid programId,
        Guid submissionId)
    {
        await RequireAdvisoryAccessAsync(programId);
        await RequireSubmissionAsync(programId, submissionId);
        var threads = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == programId
                 && t.SubmissionId == submissionId
                 && t.TargetId.HasValue
                 && !t.IsDeleted);

        return threads
            .GroupBy(t => new { t.TargetType, TargetId = t.TargetId!.Value })
            .Select(g => new AdvisoryThreadPinSummaryDto
            {
                TargetType = g.Key.TargetType,
                TargetId = g.Key.TargetId,
                OpenRequired = g.Count(t =>
                    t.Type == ProgramAdvisoryThreadType.RequiredChange
                    && t.Status == ProgramAdvisoryThreadStatus.Open),
                OpenSuggestions = g.Count(t =>
                    t.Type == ProgramAdvisoryThreadType.Suggestion
                    && t.Status == ProgramAdvisoryThreadStatus.Open),
                Total = g.Count(),
            })
            .OrderBy(x => x.TargetType)
            .ThenBy(x => x.TargetId)
            .ToList();
    }

    public async Task<AdvisoryThreadDto> GetThreadAsync(Guid programId, Guid threadId)
    {
        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        var thread = await RequireThreadAsync(programId, threadId);
        var messages = await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
            m => m.ThreadId == threadId && !m.IsDeleted);
        var latest = messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
        var author = await _unitOfWork.Users.GetByIdAsync(thread.AuthorUserId);
        var events = await _unitOfWork.ProgramAdvisoryThreadEvents.GetAllAsync(
            e => e.ThreadId == threadId && !e.IsDeleted);
        var snapshot = thread.SubmissionId.HasValue
            ? CurriculumReviewSnapshotBuilder.TryDeserialize(
                (await RequireSubmissionAsync(programId, thread.SubmissionId.Value)).CurriculumSnapshotJson)
            : null;
        var target = ResolveSnapshotTarget(snapshot, thread);
        var dto = MapThread(thread, author, messages.Count, latest?.Message, target.Label, target.Context);
        ApplyLiveTarget(dto, thread, await LoadLiveTargetIndexAsync(program));
        if (thread.SubmissionId.HasValue)
        {
            var origin = await RequireSubmissionAsync(programId, thread.SubmissionId.Value);
            ApplyOriginRound(dto, thread, new Dictionary<Guid, ProgramReviewSubmission> { [origin.Id] = origin });
        }

        dto.Events = events.OrderBy(e => e.Sequence).Select(MapThreadEvent).ToList();
        var messageAuthors = await LoadUsersAsync(messages.Select(m => m.AuthorUserId));
        dto.Messages = messages
            .OrderBy(m => m.StreamSequence)
            .ThenBy(m => m.CreatedAt)
            .Select(m => MapMessage(m, messageAuthors.GetValueOrDefault(m.AuthorUserId)))
            .ToList();
        ApplyThreadCapabilities(dto, thread, actor, isAdvisor, isBoard, canStaff, program.Status);
        return dto;
    }

    public Task<IReadOnlyList<AdvisoryAnchorFieldDto>> GetAnchorFieldsAsync()
        => Task.FromResult(AdvisoryAnchorFieldRegistry.ListAll());

    public async Task<AdvisoryReferenceDto> CreateReferenceAsync(
        Guid programId,
        CreateAdvisoryReferenceRequest request)
    {
        var (program, actor, _, _, _, _) = await RequireAdvisoryAccessAsync(programId);
        if (_referenceResolver == null)
        {
            throw ErrorHelper.Internal("Advisory reference support is not configured.");
        }

        var reference = await _referenceResolver.CaptureAsync(program, actor, request);
        await _unitOfWork.ProgramAdvisoryReferences.AddAsync(reference);
        await _unitOfWork.SaveChangesAsync();
        return await _referenceResolver.ResolveAsync(programId, reference.Id);
    }

    public async Task<AdvisoryReferenceDto> GetReferenceAsync(Guid programId, Guid referenceId)
    {
        await RequireAdvisoryAccessAsync(programId);
        if (_referenceResolver == null)
        {
            throw ErrorHelper.Internal("Advisory reference support is not configured.");
        }

        return await _referenceResolver.ResolveAsync(programId, referenceId);
    }

    public async Task<AdvisoryBoardDto> GetBoardAsync(Guid programId, Guid submissionId)
    {
        var (program, _, _, _, _, _) = await RequireAdvisoryAccessAsync(programId);
        var submission = await RequireSubmissionAsync(programId, submissionId);
        var snapshot = CurriculumReviewSnapshotBuilder.TryDeserialize(submission.CurriculumSnapshotJson)
            ?? new CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument();
        CurriculumReviewSnapshotBuilder.ApplyBoardPresentationTruncation(snapshot);
        await HydrateMaterialPreviewUrlsAsync(snapshot);

        var previous = (await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
                s => s.ProgramId == programId
                     && s.SubmissionNumber < submission.SubmissionNumber
                     && !s.IsDeleted))
            .OrderByDescending(s => s.SubmissionNumber)
            .FirstOrDefault();

        var threads = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == programId
                 && t.SubmissionId == submissionId
                 && !t.IsDeleted);
        var threadIds = threads.Select(t => t.Id).ToList();
        var messages = threadIds.Count == 0
            ? []
            : await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
                m => threadIds.Contains(m.ThreadId) && !m.IsDeleted);
        var messageCounts = messages.GroupBy(m => m.ThreadId).ToDictionary(g => g.Key, g => g.Count());
        var authors = await LoadUsersAsync(threads.Select(t => t.AuthorUserId));

        var changes = CurriculumReviewSnapshotBuilder.Diff(
            submission.Id,
            previous?.Id,
            previous?.CurriculumSnapshotJson,
            submission.CurriculumSnapshotJson);

        var highlights = await BuildFrameworkHighlightsAsync(submission.FrameworkVersionId, snapshot);
        return new AdvisoryBoardDto
        {
            SubmissionId = submission.Id,
            PreviousSubmissionId = previous?.Id,
            Program = new AdvisoryBoardProgramDto
            {
                Id = program.Id,
                Name = program.Name,
                Code = program.Code,
                Status = program.Status,
                Description = snapshot.Program.Description,
                SkillsGained = program.SkillsGained,
                FrameworkVersionId = submission.FrameworkVersionId,
            },
            Curriculum = snapshot,
            ThreadPins = threads
                .OrderByDescending(t => t.LastMessageAt)
                .Select(t =>
                {
                    var latest = messages
                        .Where(m => m.ThreadId == t.Id)
                        .OrderByDescending(m => m.CreatedAt)
                        .FirstOrDefault();
                    var target = ResolveSnapshotTarget(snapshot, t);
                    return new AdvisoryThreadPinDto
                    {
                        ThreadId = t.Id,
                        SubmissionId = t.SubmissionId,
                        TargetType = t.TargetType,
                        TargetId = t.TargetId,
                        TargetLabel = target.Label,
                        Type = t.Type,
                        Status = t.Status,
                        MessageCount = messageCounts.GetValueOrDefault(t.Id),
                        AuthorName = authors.GetValueOrDefault(t.AuthorUserId) is { } author
                            ? DisplayName(author)
                            : null,
                        LastMessagePreview = Preview(latest?.Message),
                        LastMessageAt = t.LastMessageAt,
                    };
                })
                .ToList(),
            ChangeSummary = changes,
            FrameworkHighlights = highlights,
        };
    }

    public async Task<AdvisoryThreadDto> CreateThreadAsync(Guid programId, CreateAdvisoryThreadRequest request)
    {
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => CreateThreadCoreAsync(programId, request));
        await RunAfterCommitNotifyAsync(result.AfterCommitNotify);
        return result.Value;
    }

    private async Task<AdvisoryMutationResult<AdvisoryThreadDto>> CreateThreadCoreAsync(
        Guid programId,
        CreateAdvisoryThreadRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var (program, actor, expert, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        EnsureReviewNotesMutable(program);
        if (canStaff)
        {
            throw ErrorHelper.Forbidden("Only an Expert can create advisory threads.");
        }

        if (!(canStaff || isAdvisor || isBoard))
        {
            throw ErrorHelper.Forbidden("You cannot advise on this program.");
        }

        if (request.Type == ProgramAdvisoryThreadType.General)
        {
            throw ErrorHelper.BadRequest("The general discussion thread is created automatically.");
        }

        if (request.Type == ProgramAdvisoryThreadType.RequiredChange && !isAdvisor)
        {
            throw ErrorHelper.Forbidden("Only the responsible advisor can create required-change threads.");
        }

        if (!canStaff && !isAdvisor && request.Type != ProgramAdvisoryThreadType.Suggestion)
        {
            throw ErrorHelper.Forbidden("Board experts may only create suggestions.");
        }

        var messageText = CurriculumReviewValidator.RequireComment(request.Message);
        ValidateAnchor(request);
        var (label, context) = await ResolveTargetAsync(program, request.TargetType, request.TargetId);
        var now = _currentTime.GetCurrentTime();

        Guid? submissionId = request.SubmissionId;
        if (submissionId.HasValue)
        {
            var submission = await _unitOfWork.ProgramReviewSubmissions.GetByIdAsync(submissionId.Value);
            if (submission == null || submission.IsDeleted || submission.ProgramId != program.Id)
            {
                throw ErrorHelper.BadRequest("Submission does not belong to this program.");
            }

            if (program.Status == ProgramStatus.PendingReview
                && submission.Status != ProgramReviewSubmissionStatus.Pending)
            {
                throw ErrorHelper.Conflict("Only the active review submission can receive advisory threads.");
            }
        }
        else if (program.Status == ProgramStatus.PendingReview)
        {
            throw ErrorHelper.BadRequest("SubmissionId is required while the program is under active review.");
        }

        var thread = new ProgramAdvisoryThread
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            AuthorUserId = actor.Id,
            SubmissionId = submissionId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            TargetLabel = label,
            TargetContext = context,
            Type = request.Type,
            Status = ProgramAdvisoryThreadStatus.Open,
            AnchorKind = request.AnchorKind,
            AnchorField = request.AnchorField,
            AnchorQuote = request.AnchorQuote,
            ConcurrencyVersion = Guid.NewGuid(),
            LatestActivitySequence = 1,
            LastMessageAt = now,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        var message = new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = actor.Id,
            StreamSequence = 1,
            Message = messageText,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };

        await _unitOfWork.ProgramAdvisoryThreads.AddAsync(thread);
        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(message);
        var createdEvent = new ProgramAdvisoryThreadEvent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            ThreadId = thread.Id,
            Sequence = 1,
            EventType = ProgramAdvisoryThreadEventType.Created,
            ActorUserId = actor.Id,
            NewStatus = thread.Status,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        await _unitOfWork.ProgramAdvisoryThreadEvents.AddAsync(createdEvent);
        await AddNotificationIntentAsync(
            program.Id,
            createdEvent.Id,
            "AdvisoryThreadCreated",
            NotificationType.AdvisoryFeedbackPublished,
            new { programId = program.Id, threadId = thread.Id },
            actor.Id,
            now);
        await _unitOfWork.SaveChangesAsync();

        var result = MapThread(thread, actor, 1, messageText, label, context);
        result.Events = [MapThreadEvent(createdEvent)];
        ApplyLiveTarget(result, thread, await LoadLiveTargetIndexAsync(program));
        ApplyThreadCapabilities(result, thread, actor, isAdvisor, isBoard, canStaff, program.Status);
        return new AdvisoryMutationResult<AdvisoryThreadDto>
        {
            Value = result,
            AfterCommitNotify = () => NotifyAdvisoryAsync(
                program,
                actor.Id,
                notifyManagers: actor.Role == RoleType.Expert,
                userId => NotificationCatalog.AdvisoryFeedbackPublished(
                    userId,
                    program.Id,
                    thread.Id,
                    actor.Id,
                    program.Name,
                    DisplayName(actor),
                    thread.Type.ToString())),
        };
    }

    public async Task<IReadOnlyList<AdvisoryMessageDto>> GetMessagesAsync(Guid programId, Guid threadId)
    {
        await RequireAdvisoryAccessAsync(programId);
        var thread = await RequireThreadAsync(programId, threadId);
        var messages = await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
            m => m.ThreadId == thread.Id && !m.IsDeleted);
        var ordered = messages.OrderBy(m => m.CreatedAt).ToList();
        var authors = await LoadUsersAsync(ordered.Select(m => m.AuthorUserId));
        return ordered.Select(m => MapMessage(m, authors.GetValueOrDefault(m.AuthorUserId))).ToList();
    }

    public async Task<AdvisoryMessageDto> AddMessageAsync(Guid programId, Guid threadId, string message)
    {
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => AddMessageCoreAsync(programId, threadId, message));
        await RunAfterCommitNotifyAsync(result.AfterCommitNotify);
        return result.Value;
    }

    private async Task<AdvisoryMutationResult<AdvisoryMessageDto>> AddMessageCoreAsync(
        Guid programId,
        Guid threadId,
        string message)
    {
        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        EnsureReviewNotesMutable(program);
        if (!(canStaff || isAdvisor || isBoard))
        {
            throw ErrorHelper.Forbidden("You cannot advise on this program.");
        }

        var thread = await RequireThreadAsync(programId, threadId);
        var text = CurriculumReviewValidator.RequireComment(message);
        var now = _currentTime.GetCurrentTime();
        var sequence = await AllocateNextActivitySequenceAsync(thread);
        thread.ConcurrencyVersion = Guid.NewGuid();
        var row = new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = actor.Id,
            StreamSequence = sequence,
            Message = text,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        thread.LastMessageAt = now;
        thread.UpdatedAt = now;
        thread.UpdatedBy = actor.Id;

        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(row);
        var messageEvent = new ProgramAdvisoryThreadEvent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            ThreadId = thread.Id,
            Sequence = sequence,
            EventType = ProgramAdvisoryThreadEventType.MessageAdded,
            ActorUserId = actor.Id,
            Message = text,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        await _unitOfWork.ProgramAdvisoryThreadEvents.AddAsync(messageEvent);
        await AddNotificationIntentAsync(
            program.Id,
            messageEvent.Id,
            "AdvisoryReply",
            NotificationType.AdvisoryReply,
            new { programId = program.Id, threadId = thread.Id },
            actor.Id,
            now);
        await _unitOfWork.ProgramAdvisoryThreads.Update(thread);
        await _unitOfWork.SaveChangesAsync();

        return new AdvisoryMutationResult<AdvisoryMessageDto>
        {
            Value = MapMessage(row, actor),
            AfterCommitNotify = () => NotifyAdvisoryAsync(
                program,
                actor.Id,
                notifyManagers: actor.Role == RoleType.Expert,
                userId => NotificationCatalog.AdvisoryReply(
                    userId,
                    program.Id,
                    thread.Id,
                    actor.Id,
                    program.Name,
                    DisplayName(actor))),
        };
    }

    public async Task<AdvisoryThreadDto> UpdateThreadStatusAsync(
        Guid programId,
        Guid threadId,
        UpdateAdvisoryThreadStatusRequest request)
    {
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => UpdateThreadStatusCoreAsync(programId, threadId, request));
        await RunAfterCommitNotifyAsync(result.AfterCommitNotify);
        return result.Value;
    }

    public async Task<AdvisoryThreadDto> PerformThreadActionAsync(
        Guid programId,
        Guid threadId,
        AdvisoryThreadActionRequest request)
    {
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => PerformThreadActionCoreAsync(programId, threadId, request));
        await RunAfterCommitNotifyAsync(result.AfterCommitNotify);
        return result.Value;
    }

    private async Task<AdvisoryMutationResult<AdvisoryThreadDto>> PerformThreadActionCoreAsync(
        Guid programId,
        Guid threadId,
        AdvisoryThreadActionRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        var thread = await RequireThreadAsync(programId, threadId);
        var existingOperation = await FindThreadEventByOperationIdAsync(programId, threadId, request.ClientOperationId);
        if (existingOperation != null)
        {
            return new AdvisoryMutationResult<AdvisoryThreadDto>
            {
                Value = await GetThreadAsync(programId, threadId),
            };
        }

        EnsureReviewNotesMutable(program);
        if (!request.ConcurrencyVersion.HasValue
            || request.ConcurrencyVersion.Value != thread.ConcurrencyVersion)
        {
            throw ErrorHelper.Conflict("Advisory thread was updated elsewhere. Reload and try again.");
        }

        if (thread.Type == ProgramAdvisoryThreadType.General)
        {
            throw ErrorHelper.BadRequest("The general discussion thread has no status actions.");
        }

        var priorStatus = thread.Status;
        var text = CurriculumReviewValidator.NormalizeOptionalComment(request.Message);
        var newStatus = thread.Status;
        ProgramAdvisoryThreadEventType eventType;
        AdvisoryResolutionKind? resolutionKind = null;
        Guid? verifiedAgainstSubmissionId = null;
        string? notificationEventType = null;
        NotificationType? notificationType = null;

        switch (request.Action)
        {
            case AdvisoryThreadAction.MarkFixed:
                if (!canStaff)
                {
                    throw ErrorHelper.Forbidden("Only Manager or Admin can mark a required change as fixed.");
                }

                if (thread.Type != ProgramAdvisoryThreadType.RequiredChange)
                {
                    throw ErrorHelper.BadRequest("MarkFixed applies only to required changes.");
                }

                if (program.Status != ProgramStatus.Draft)
                {
                    throw ErrorHelper.Conflict(
                        "Feedback can only be marked fixed while the curriculum is editable (Draft).",
                        "ADVISORY_ADDRESS_NOT_EDITABLE");
                }

                if (priorStatus != ProgramAdvisoryThreadStatus.Open)
                {
                    throw ErrorHelper.Conflict("Only open required changes can be marked fixed.");
                }

                newStatus = ProgramAdvisoryThreadStatus.Addressed;
                eventType = ProgramAdvisoryThreadEventType.CorrectionSubmitted;
                notificationEventType = "AdvisoryCorrectionAddressed";
                notificationType = NotificationType.AdvisoryCorrectionAddressed;
                break;
            case AdvisoryThreadAction.Acknowledge:
                if (thread.Type != ProgramAdvisoryThreadType.Suggestion)
                {
                    throw ErrorHelper.BadRequest("Acknowledge applies only to suggestions.");
                }

                if (!(canStaff || thread.AuthorUserId == actor.Id))
                {
                    throw ErrorHelper.Forbidden("Only the manager or the author can acknowledge a suggestion.");
                }

                if (priorStatus != ProgramAdvisoryThreadStatus.Open)
                {
                    throw ErrorHelper.Conflict("Only open suggestions can be acknowledged.");
                }

                newStatus = ProgramAdvisoryThreadStatus.Resolved;
                eventType = ProgramAdvisoryThreadEventType.StatusChanged;
                notificationEventType = "AdvisoryThreadAcknowledged";
                notificationType = NotificationType.AdvisoryFeedbackPublished;
                break;
            case AdvisoryThreadAction.Accept:
                if (!isAdvisor)
                {
                    throw ErrorHelper.Forbidden("Only the responsible advisor can accept a required change.");
                }

                if (thread.Type != ProgramAdvisoryThreadType.RequiredChange)
                {
                    throw ErrorHelper.BadRequest("Accept applies only to required changes.");
                }

                if (priorStatus != ProgramAdvisoryThreadStatus.Addressed)
                {
                    throw ErrorHelper.Conflict(
                        "Only a required change the manager has marked fixed can be accepted.",
                        "ACCEPT_REQUIRES_FIXED");
                }

                if (program.Status != ProgramStatus.PendingReview)
                {
                    throw ErrorHelper.Conflict(
                        "Accept is available only after the manager sends the program back for review.",
                        "ACCEPT_REQUIRES_RESUBMIT");
                }

                newStatus = ProgramAdvisoryThreadStatus.Resolved;
                eventType = ProgramAdvisoryThreadEventType.VerificationRecorded;
                resolutionKind = AdvisoryResolutionKind.Verified;
                verifiedAgainstSubmissionId = await ResolveCurrentSubmissionIdAsync(program.Id);
                notificationEventType = "AdvisoryRequirementAccepted";
                notificationType = NotificationType.AdvisoryFeedbackPublished;
                break;
            default:
                throw ErrorHelper.BadRequest("Unsupported advisory thread action.");
        }

        _ = isBoard;
        var now = _currentTime.GetCurrentTime().ToUniversalTime();
        thread.Status = newStatus;
        var sequence = await AllocateNextActivitySequenceAsync(thread);
        thread.ConcurrencyVersion = Guid.NewGuid();
        thread.UpdatedAt = now;
        thread.UpdatedBy = actor.Id;

        if (text != null)
        {
            var followUp = new ProgramAdvisoryMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AuthorUserId = actor.Id,
                StreamSequence = sequence,
                Message = text,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };
            thread.LastMessageAt = now;
            await _unitOfWork.ProgramAdvisoryMessages.AddAsync(followUp);
        }

        var statusEvent = new ProgramAdvisoryThreadEvent
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ThreadId = thread.Id,
            Sequence = sequence,
            EventType = eventType,
            ActorUserId = actor.Id,
            PriorStatus = priorStatus,
            NewStatus = newStatus,
            Message = text,
            ResolutionKind = resolutionKind,
            VerifiedAgainstSubmissionId = verifiedAgainstSubmissionId,
            CorrectionReferenceIdsJson = "[]",
            OperationId = NormalizeOperationId(request.ClientOperationId),
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        await _unitOfWork.ProgramAdvisoryThreadEvents.AddAsync(statusEvent);
        await AddNotificationIntentAsync(
            program.Id,
            statusEvent.Id,
            notificationEventType!,
            notificationType!.Value,
            new { programId = program.Id, threadId = thread.Id, action = request.Action.ToString() },
            actor.Id,
            now);

        await _unitOfWork.ProgramAdvisoryThreads.Update(thread);
        await _unitOfWork.SaveChangesAsync();

        var value = await GetThreadAsync(programId, threadId);
        return new AdvisoryMutationResult<AdvisoryThreadDto>
        {
            Value = value,
            AfterCommitNotify = request.Action == AdvisoryThreadAction.MarkFixed
                ? () => NotifyAdvisoryAsync(
                    program,
                    actor.Id,
                    notifyManagers: false,
                    userId => NotificationCatalog.AdvisoryCorrectionAddressed(
                        userId,
                        program.Id,
                        thread.Id,
                        actor.Id,
                        program.Name,
                        DisplayName(actor)))
                : null,
        };
    }

    private async Task<Guid?> ResolveCurrentSubmissionIdAsync(Guid programId)
    {
        var pending = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
            s => s.ProgramId == programId
                 && s.Status == ProgramReviewSubmissionStatus.Pending
                 && !s.IsDeleted);
        return pending
            .OrderByDescending(s => s.SubmissionNumber)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefault();
    }

    private async Task<AdvisoryMutationResult<AdvisoryThreadDto>> UpdateThreadStatusCoreAsync(
        Guid programId,
        Guid threadId,
        UpdateAdvisoryThreadStatusRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        var thread = await RequireThreadAsync(programId, threadId);
        var existingOperation = await FindThreadEventByOperationIdAsync(programId, threadId, request.ClientOperationId);
        if (existingOperation != null)
        {
            return new AdvisoryMutationResult<AdvisoryThreadDto>
            {
                Value = await GetThreadAsync(programId, threadId),
            };
        }

        EnsureReviewNotesMutable(program);
        if (!request.ConcurrencyVersion.HasValue
            || request.ConcurrencyVersion.Value != thread.ConcurrencyVersion)
        {
            throw ErrorHelper.Conflict("Advisory thread was updated elsewhere. Reload and try again.");
        }

        if (request.Status == thread.Status)
        {
            throw ErrorHelper.BadRequest("The advisory thread is already in the requested status.");
        }

        var priorStatus = thread.Status;
        var text = request.Status is ProgramAdvisoryThreadStatus.Addressed or ProgramAdvisoryThreadStatus.Open
            ? CurriculumReviewValidator.RequireComment(request.Message)
            : CurriculumReviewValidator.NormalizeOptionalComment(request.Message);

        switch (request.Status)
        {
            case ProgramAdvisoryThreadStatus.Addressed:
                if (!canStaff)
                {
                    throw ErrorHelper.Forbidden("Only Manager or Admin can mark feedback as addressed.");
                }

                if (program.Status != ProgramStatus.Draft)
                {
                    throw ErrorHelper.Conflict(
                        "Feedback can only be marked addressed while the curriculum is editable (Draft / Revision).",
                        "ADVISORY_ADDRESS_NOT_EDITABLE");
                }

                if (priorStatus != ProgramAdvisoryThreadStatus.Open)
                {
                    throw ErrorHelper.Conflict("Only open feedback can be marked as addressed.");
                }

                break;
            case ProgramAdvisoryThreadStatus.Resolved:
                if (request.ResolutionKind == AdvisoryResolutionKind.Waived)
                {
                    throw ErrorHelper.BadRequest(
                        "Waive is no longer supported. Accept the required change or return the round.",
                        "ADVISORY_WAIVE_REMOVED");
                }

                await ValidateResolutionAsync(program, thread, isAdvisor, actor, request);
                break;
            case ProgramAdvisoryThreadStatus.Open:
                if (thread.Type == ProgramAdvisoryThreadType.RequiredChange && !isAdvisor)
                {
                    throw ErrorHelper.Forbidden("Only the responsible advisor can reopen required changes.");
                }

                if (thread.Type == ProgramAdvisoryThreadType.Suggestion
                    && !(isAdvisor || thread.AuthorUserId == actor.Id || canStaff))
                {
                    throw ErrorHelper.Forbidden("You cannot reopen this suggestion.");
                }

                break;
            default:
                throw ErrorHelper.BadRequest("Unsupported advisory thread status.");
        }

        var correctionReferenceIds = await ValidateCorrectionReferencesAsync(programId, request.CorrectionReferenceIds);
        var now = _currentTime.GetCurrentTime().ToUniversalTime();
        thread.Status = request.Status;
        var sequence = await AllocateNextActivitySequenceAsync(thread);
        thread.ConcurrencyVersion = Guid.NewGuid();
        thread.UpdatedAt = now;
        thread.UpdatedBy = actor.Id;

        if (text != null)
        {
            var followUp = new ProgramAdvisoryMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AuthorUserId = actor.Id,
                StreamSequence = sequence,
                Message = text,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };
            thread.LastMessageAt = now;
            await _unitOfWork.ProgramAdvisoryMessages.AddAsync(followUp);
        }

        var resolutionKind = request.Status == ProgramAdvisoryThreadStatus.Resolved
            ? request.ResolutionKind
            : null;
        var statusEvent = new ProgramAdvisoryThreadEvent
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ThreadId = thread.Id,
            Sequence = sequence,
            EventType = resolutionKind == AdvisoryResolutionKind.Verified
                ? ProgramAdvisoryThreadEventType.VerificationRecorded
                : resolutionKind == AdvisoryResolutionKind.Waived
                    ? ProgramAdvisoryThreadEventType.WaiverRecorded
                    : request.Status == ProgramAdvisoryThreadStatus.Addressed
                        ? ProgramAdvisoryThreadEventType.CorrectionSubmitted
                        : ProgramAdvisoryThreadEventType.StatusChanged,
            ActorUserId = actor.Id,
            PriorStatus = priorStatus,
            NewStatus = request.Status,
            Message = text,
            ResolutionKind = resolutionKind,
            VerifiedAgainstSubmissionId = request.VerifiedAgainstSubmissionId,
            CorrectionReferenceIdsJson = JsonSerializer.Serialize(correctionReferenceIds),
            OperationId = NormalizeOperationId(request.ClientOperationId),
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        await _unitOfWork.ProgramAdvisoryThreadEvents.AddAsync(statusEvent);
        await AddNotificationIntentAsync(
            program.Id,
            statusEvent.Id,
            request.Status == ProgramAdvisoryThreadStatus.Addressed
                ? "AdvisoryCorrectionAddressed"
                : "AdvisoryThreadStatusChanged",
            request.Status == ProgramAdvisoryThreadStatus.Addressed
                ? NotificationType.AdvisoryCorrectionAddressed
                : NotificationType.AdvisoryFeedbackPublished,
            new { programId = program.Id, threadId = thread.Id, status = request.Status },
            actor.Id,
            now);

        await _unitOfWork.ProgramAdvisoryThreads.Update(thread);
        await _unitOfWork.SaveChangesAsync();

        // Contract: PATCH returns the full AdvisoryThreadDto (ordered events + messages + flags).
        var value = await GetThreadAsync(programId, threadId);
        return new AdvisoryMutationResult<AdvisoryThreadDto>
        {
            Value = value,
            AfterCommitNotify = request.Status == ProgramAdvisoryThreadStatus.Addressed
                ? () => NotifyAdvisoryAsync(
                    program,
                    actor.Id,
                    notifyManagers: false,
                    userId => NotificationCatalog.AdvisoryCorrectionAddressed(
                        userId,
                        program.Id,
                        thread.Id,
                        actor.Id,
                        program.Name,
                        DisplayName(actor)))
                : null,
        };
    }

    public async Task RecordReadAsync(Guid programId, RecordAdvisoryReadRequest? request)
    {
        var (program, actor, _, _, _, _) = await RequireAdvisoryAccessAsync(programId);
        var now = request?.LastReadAt ?? _currentTime.GetCurrentTime();
        if (now.Kind == DateTimeKind.Unspecified)
        {
            now = DateTime.SpecifyKind(now, DateTimeKind.Utc);
        }

        var existing = await _unitOfWork.ProgramAdvisoryReads.FirstOrDefaultAsync(
            r => r.ProgramId == program.Id && r.UserId == actor.Id && !r.IsDeleted);
        if (existing == null)
        {
            await _unitOfWork.ProgramAdvisoryReads.AddAsync(new ProgramAdvisoryRead
            {
                Id = Guid.NewGuid(),
                ProgramId = program.Id,
                UserId = actor.Id,
                LastReadAt = now,
                CreatedAt = now,
                CreatedBy = actor.Id,
            });
        }
        else
        {
            existing.LastReadAt = now;
            existing.UpdatedAt = now;
            existing.UpdatedBy = actor.Id;
            await _unitOfWork.ProgramAdvisoryReads.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task AddNotificationIntentAsync(
        Guid programId,
        Guid eventId,
        string eventType,
        NotificationType notificationType,
        object payload,
        Guid actorUserId,
        DateTime now)
    {
        await _unitOfWork.ProgramAdvisoryNotificationIntents.AddAsync(
            new ProgramAdvisoryNotificationIntent
            {
                Id = Guid.NewGuid(),
                ProgramId = programId,
                EventId = eventId,
                EventType = eventType,
                NotificationType = notificationType,
                PayloadJson = JsonSerializer.Serialize(payload),
                Status = AdvisoryNotificationIntentStatus.Pending,
                AttemptCount = 0,
                NextAttemptAt = now,
                CreatedAt = now,
                CreatedBy = actorUserId,
            });
    }

    /// <summary>
    /// Next stream/event sequence is max(thread counter, existing message/event sequences) + 1
    /// so a stale <see cref="ProgramAdvisoryThread.LatestActivitySequence"/> cannot collide
    /// with unique indexes on (ThreadId, StreamSequence) / (ThreadId, Sequence).
    /// </summary>
    private async Task<long> AllocateNextActivitySequenceAsync(ProgramAdvisoryThread thread)
    {
        var messageMax = (await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
                m => m.ThreadId == thread.Id && !m.IsDeleted))
            .Select(m => m.StreamSequence)
            .DefaultIfEmpty(0)
            .Max();
        var eventMax = (await _unitOfWork.ProgramAdvisoryThreadEvents.GetAllAsync(
                e => e.ThreadId == thread.Id && !e.IsDeleted))
            .Select(e => e.Sequence)
            .DefaultIfEmpty(0)
            .Max();
        var next = Math.Max(thread.LatestActivitySequence, Math.Max(messageMax, eventMax)) + 1;
        thread.LatestActivitySequence = next;
        return next;
    }

    private static async Task RunAfterCommitNotifyAsync(Func<Task>? notify)
    {
        if (notify == null)
        {
            return;
        }

        // Best-effort: reply/status already committed; pending intents remain for retry.
        try
        {
            await notify();
        }
        catch
        {
            // Intentionally swallowed — notification failure must not fail the advisory mutation.
        }
    }

    private sealed class AdvisoryMutationResult<T>
    {
        public required T Value { get; init; }
        public Func<Task>? AfterCommitNotify { get; init; }
    }

    private async Task NotifyAdvisoryAsync(
        Program program,
        Guid excludeUserId,
        bool notifyManagers,
        Func<Guid, NotificationCommand> buildForUser)
    {
        var recipientIds = new HashSet<Guid>();
        if (program.AdvisorExpertId.HasValue)
        {
            var advisor = await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value);
            if (advisor?.UserId is { } advisorUserId && advisorUserId != Guid.Empty)
            {
                recipientIds.Add(advisorUserId);
            }
        }

        var boards = await _unitOfWork.ProgramBoards.GetAllAsync(
            b => b.ProgramId == program.Id && !b.IsDeleted);
        var expertIds = boards.Select(b => b.ExpertId).Distinct().ToList();
        if (expertIds.Count > 0)
        {
            var experts = await _unitOfWork.Experts.GetAllAsync(e => expertIds.Contains(e.Id) && !e.IsDeleted);
            foreach (var expert in experts)
            {
                if (expert.UserId is { } userId && userId != Guid.Empty)
                {
                    recipientIds.Add(userId);
                }
            }
        }

        var commands = recipientIds
            .Where(id => id != excludeUserId)
            .Select(buildForUser)
            .ToList();

        if (notifyManagers)
        {
            commands.Add(buildForUser(Guid.Empty));
        }

        if (commands.Count > 0)
        {
            await _notificationPublisher.PublishManyAsync(commands);
        }
    }

    private async Task<(string Label, string? Context)> ResolveTargetAsync(
        Program program,
        ProgramAdvisoryTargetType targetType,
        Guid? targetId)
    {
        if (targetType == ProgramAdvisoryTargetType.Program)
        {
            return (program.Name, "Program");
        }

        if (!targetId.HasValue || targetId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("TargetId is required for this target type.");
        }

        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, program.Id);
        switch (targetType)
        {
            case ProgramAdvisoryTargetType.Module:
            {
                var module = tree.Modules.FirstOrDefault(m => m.Id == targetId.Value);
                if (module == null)
                {
                    throw ErrorHelper.BadRequest("Target module does not belong to this program.");
                }

                return (module.Name, $"ModuleOrder={module.ModuleOrder}");
            }
            case ProgramAdvisoryTargetType.Course:
            {
                var course = tree.CoursesByModuleId.Values.SelectMany(c => c)
                    .FirstOrDefault(c => c.Id == targetId.Value);
                if (course == null)
                {
                    throw ErrorHelper.BadRequest("Target course does not belong to this program.");
                }

                return (course.Name, $"CourseOrder={course.CourseOrder}");
            }
            case ProgramAdvisoryTargetType.Activity:
            {
                if (!tree.ActivitiesById.TryGetValue(targetId.Value, out var activity))
                {
                    throw ErrorHelper.BadRequest("Target activity does not belong to this program.");
                }

                return (activity.Name, activity.ActivityType.ToString());
            }
            case ProgramAdvisoryTargetType.Assignment:
            {
                if (!tree.AssignmentsById.TryGetValue(targetId.Value, out var assignment))
                {
                    throw ErrorHelper.BadRequest("Target assignment does not belong to this program.");
                }

                return (assignment.Title, assignment.AssignmentType.ToString());
            }
            case ProgramAdvisoryTargetType.ResearchMilestone:
            {
                var milestone = tree.MilestonesByModuleId.Values.SelectMany(m => m)
                    .FirstOrDefault(m => m.Id == targetId.Value);
                if (milestone == null)
                {
                    throw ErrorHelper.BadRequest("Target research milestone does not belong to this program.");
                }

                return (milestone.Title, milestone.IsCapstone ? "Capstone" : $"Order={milestone.MilestoneOrder}");
            }
            case ProgramAdvisoryTargetType.Material:
            {
                var material = tree.MaterialsByActivityId.Values
                    .FirstOrDefault(m => m.Id == targetId.Value && !m.IsDeleted);
                if (material == null)
                {
                    throw ErrorHelper.BadRequest("Target material does not belong to this program.");
                }

                return (material.Title, material.MaterialType.ToString());
            }
            case ProgramAdvisoryTargetType.RubricCriterion:
            {
                if (!program.FrameworkVersionId.HasValue)
                {
                    throw ErrorHelper.BadRequest("Program has no pinned framework version for rubric targets.");
                }

                var criterion = await _unitOfWork.FrameworkRubricCriteria.GetByIdAsync(targetId.Value);
                if (criterion == null
                    || criterion.IsDeleted
                    || criterion.FrameworkVersionId != program.FrameworkVersionId)
                {
                    throw ErrorHelper.BadRequest("Target rubric criterion does not belong to this program.");
                }

                return (criterion.Name, $"MaxScore={criterion.MaxScore}");
            }
            default:
                throw ErrorHelper.BadRequest("Unsupported advisory target type.");
        }
    }

    private async Task<(
        Program Program,
        User Actor,
        Expert? Expert,
        bool IsAdvisor,
        bool IsBoard,
        bool CanStaff)> RequireAdvisoryAccessAsync(Guid programId)
    {
        var actor = await ResolveActorAsync();
        var program = await GetActiveProgramAsync(programId);
        var canStaff = actor.Role is RoleType.Manager or RoleType.Admin;
        Expert? expert = null;
        var isAdvisor = false;
        var isBoard = false;
        if (actor.Role == RoleType.Expert)
        {
            expert = await RequireCurrentExpertAsync(actor);
            isAdvisor = program.AdvisorExpertId == expert.Id;
            isBoard = await IsBoardMemberAsync(program.Id, expert.Id);
            if (!isAdvisor && !isBoard)
            {
                throw ErrorHelper.Forbidden("You are not authorized to access advisory data for this program.");
            }
        }

        return (program, actor, expert, isAdvisor, isBoard, canStaff);
    }

    private async Task<ProgramReviewSubmission> RequireSubmissionAsync(Guid programId, Guid submissionId)
    {
        var submission = await _unitOfWork.ProgramReviewSubmissions.GetByIdAsync(submissionId);
        if (submission == null || submission.IsDeleted || submission.ProgramId != programId)
        {
            throw ErrorHelper.NotFound($"Review submission '{submissionId}' was not found.");
        }

        return submission;
    }

    private async Task<List<FrameworkHighlightDto>> BuildFrameworkHighlightsAsync(
        Guid? frameworkVersionId,
        CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument snapshot)
    {
        if (!frameworkVersionId.HasValue)
        {
            return [];
        }

        var version = await _unitOfWork.ProgramFrameworkVersions.GetByIdAsync(frameworkVersionId.Value);
        if (version == null || version.IsDeleted || !version.IsPublished)
        {
            return [];
        }

        var checks = CurriculumReviewService.BuildStructuredChecks(version, snapshot);
        var highlights = new List<FrameworkHighlightDto>();
        foreach (var check in checks.Where(c => !c.Passed))
        {
            if (check.AffectedCurriculumLinks.Count == 0)
            {
                highlights.Add(new FrameworkHighlightDto
                {
                    TargetType = ProgramAdvisoryTargetType.Program,
                    TargetId = snapshot.Program.Id != Guid.Empty ? snapshot.Program.Id : snapshot.ProgramId,
                    CheckCode = check.Code,
                    Label = check.Label,
                    Passed = false,
                });
                continue;
            }

            foreach (var link in check.AffectedCurriculumLinks)
            {
                highlights.Add(new FrameworkHighlightDto
                {
                    TargetType = link.TargetType,
                    TargetId = link.Id,
                    CheckCode = check.Code,
                    Label = link.Label,
                    Passed = false,
                });
            }
        }

        return highlights;
    }

    private async Task HydrateMaterialPreviewUrlsAsync(
        CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument snapshot)
    {
        var materialsById = snapshot.Modules
            .SelectMany(module => module.Courses
                .SelectMany(course => course.Activities)
                .Concat(module.Milestones.SelectMany(milestone => milestone.Activities))
                .Concat(module.Activities))
            .Where(activity => activity.Material != null)
            .Select(activity => activity.Material!)
            .Where(material => material.Id != Guid.Empty && !string.IsNullOrWhiteSpace(material.Url))
            .GroupBy(material => material.Id)
            .ToList();

        foreach (var materialGroup in materialsById)
        {
            var key = ExtractS3Key(materialGroup.First().Url!);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var previewUrl = await _blobService.GetFileUrlAsync(key);
            if (!string.IsNullOrWhiteSpace(previewUrl))
            {
                foreach (var material in materialGroup)
                {
                    material.Url = previewUrl;
                }
            }
        }
    }

    private static string? ExtractS3Key(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return fileUrl;
        }

        if (!fileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return fileUrl.TrimStart('/');
        }

        return Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri)
            ? Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
            : fileUrl;
    }

    private static (string Label, string? Context) ResolveSnapshotTarget(
        CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument? snapshot,
        ProgramAdvisoryThread thread)
    {
        if (snapshot == null)
        {
            return (thread.TargetLabel, thread.TargetContext);
        }

        if (thread.TargetType == ProgramAdvisoryTargetType.Program)
        {
            return (snapshot.Program.Name ?? snapshot.ProgramName ?? thread.TargetLabel, "Program");
        }

        if (!thread.TargetId.HasValue)
        {
            return (thread.TargetLabel, thread.TargetContext);
        }

        var id = thread.TargetId.Value;
        switch (thread.TargetType)
        {
            case ProgramAdvisoryTargetType.Module:
            {
                var module = snapshot.Modules.FirstOrDefault(m => m.Id == id);
                return module == null
                    ? (thread.TargetLabel, thread.TargetContext)
                    : (module.Name, $"ModuleOrder={module.Order}");
            }
            case ProgramAdvisoryTargetType.Course:
            {
                var course = snapshot.Modules.SelectMany(m => m.Courses).FirstOrDefault(c => c.Id == id);
                return course == null
                    ? (thread.TargetLabel, thread.TargetContext)
                    : (course.Name, $"CourseOrder={course.Order}");
            }
            case ProgramAdvisoryTargetType.Activity:
            {
                var activity = snapshot.Modules
                    .SelectMany(m => m.Courses.SelectMany(c => c.Activities).Concat(
                        m.Milestones.SelectMany(ms => ms.Activities)))
                    .FirstOrDefault(a => a.Id == id);
                return activity == null
                    ? (thread.TargetLabel, thread.TargetContext)
                    : (activity.Name, activity.Type);
            }
            case ProgramAdvisoryTargetType.Material:
            {
                var material = snapshot.Modules
                    .SelectMany(m => m.Courses.SelectMany(c => c.Activities)
                        .Concat(m.Milestones.SelectMany(ms => ms.Activities)))
                    .Select(a => a.Material)
                    .FirstOrDefault(m => m?.Id == id);
                return material == null
                    ? (thread.TargetLabel, thread.TargetContext)
                    : (material.Title, material.MaterialType);
            }
            case ProgramAdvisoryTargetType.Assignment:
            {
                var assignment = snapshot.Modules.SelectMany(m => m.Assignments).FirstOrDefault(a => a.Id == id);
                return assignment == null
                    ? (thread.TargetLabel, thread.TargetContext)
                    : (assignment.Title, assignment.AssignmentType);
            }
            case ProgramAdvisoryTargetType.ResearchMilestone:
            {
                var milestone = snapshot.Modules.SelectMany(m => m.Milestones).FirstOrDefault(m => m.Id == id);
                return milestone == null
                    ? (thread.TargetLabel, thread.TargetContext)
                    : (milestone.Title, milestone.IsCapstone ? "Capstone" : $"Order={milestone.Order}");
            }
            default:
                return (thread.TargetLabel, thread.TargetContext);
        }
    }

    private static void ValidateAnchor(CreateAdvisoryThreadRequest request)
    {
        if (request.AnchorKind == ProgramAdvisoryAnchorKind.Field
            && string.IsNullOrWhiteSpace(request.AnchorField))
        {
            throw ErrorHelper.BadRequest("AnchorField is required for a Field anchor.");
        }

        if (request.AnchorKind == ProgramAdvisoryAnchorKind.Quote
            && (string.IsNullOrWhiteSpace(request.AnchorField)
                || string.IsNullOrWhiteSpace(request.AnchorQuote)))
        {
            throw ErrorHelper.BadRequest("AnchorField and AnchorQuote are required for a Quote anchor.");
        }

        if (request.AnchorField?.Length > 100 || request.AnchorQuote?.Length > 1000)
        {
            throw ErrorHelper.BadRequest("Advisory anchor values exceed the allowed length.");
        }

        if (!string.IsNullOrWhiteSpace(request.AnchorField))
        {
            AdvisoryAnchorFieldRegistry.EnsureAllowed(request.TargetType, request.AnchorField);
        }
    }

    private async Task<ProgramAdvisoryThread> RequireThreadAsync(Guid programId, Guid threadId)
    {
        var thread = await _unitOfWork.ProgramAdvisoryThreads.GetByIdAsync(threadId);
        if (thread == null || thread.IsDeleted || thread.ProgramId != programId)
        {
            throw ErrorHelper.NotFound($"Advisory thread '{threadId}' was not found.");
        }

        return thread;
    }

    private async Task<AdvisoryThreadDto> MapThreadForCurrentActorAsync(
        Program program,
        User actor,
        bool isAdvisor,
        bool isBoard,
        bool canStaff,
        ProgramAdvisoryThread thread)
    {
        var messages = await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
            m => m.ThreadId == thread.Id && !m.IsDeleted);
        var latest = messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
        var author = await _unitOfWork.Users.GetByIdAsync(thread.AuthorUserId);
        var result = MapThread(thread, author, messages.Count, latest?.Message);
        ApplyLiveTarget(result, thread, await LoadLiveTargetIndexAsync(program));
        ApplyThreadCapabilities(result, thread, actor, isAdvisor, isBoard, canStaff, program.Status);
        return result;
    }

    private async Task<ProgramAdvisoryThreadEvent?> FindThreadEventByOperationIdAsync(
        Guid programId,
        Guid threadId,
        string? operationId)
    {
        var normalized = NormalizeOperationId(operationId);
        if (normalized == null)
        {
            return null;
        }

        var existing = await _unitOfWork.ProgramAdvisoryThreadEvents.FirstOrDefaultAsync(
            e => e.ProgramId == programId
                 && e.ThreadId == threadId
                 && e.OperationId == normalized
                 && !e.IsDeleted);
        return existing;
    }

    private async Task ValidateResolutionAsync(
        Program program,
        ProgramAdvisoryThread thread,
        bool isAdvisor,
        User actor,
        UpdateAdvisoryThreadStatusRequest request)
    {
        if (thread.Type == ProgramAdvisoryThreadType.RequiredChange)
        {
            if (!isAdvisor)
            {
                throw ErrorHelper.Forbidden("Only the responsible advisor can resolve required changes.");
            }

            if (!request.ResolutionKind.HasValue)
            {
                throw ErrorHelper.BadRequest("ResolutionKind is required when resolving a required change.");
            }

            if (request.ResolutionKind == AdvisoryResolutionKind.Verified)
            {
                if (!request.VerifiedAgainstSubmissionId.HasValue)
                {
                    throw ErrorHelper.BadRequest("VerifiedAgainstSubmissionId is required for verification.");
                }

                var submission = await _unitOfWork.ProgramReviewSubmissions.GetByIdAsync(
                    request.VerifiedAgainstSubmissionId.Value);
                if (submission == null
                    || submission.IsDeleted
                    || submission.ProgramId != program.Id
                    || submission.Status != ProgramReviewSubmissionStatus.Pending)
                {
                    throw ErrorHelper.Conflict("Verification must target the current pending submission.");
                }

                if (thread.SubmissionId.HasValue
                    && (submission.Id == thread.SubmissionId.Value
                        || submission.SubmissionNumber <= (await RequireSubmissionAsync(program.Id, thread.SubmissionId.Value)).SubmissionNumber))
                {
                    throw ErrorHelper.Conflict("A required change must be verified against a newer submission.");
                }
            }
            else if (request.VerifiedAgainstSubmissionId.HasValue)
            {
                throw ErrorHelper.BadRequest("A waived requirement cannot include verification submission context.");
            }

            if (request.ResolutionKind == AdvisoryResolutionKind.Waived
                && string.IsNullOrWhiteSpace(request.Message))
            {
                throw ErrorHelper.BadRequest("A reason is required when waiving a required change.");
            }

            return;
        }

        if (request.ResolutionKind.HasValue || request.VerifiedAgainstSubmissionId.HasValue)
        {
            throw ErrorHelper.BadRequest("Resolution metadata is only valid for required changes.");
        }

        if (!(isAdvisor || thread.AuthorUserId == actor.Id))
        {
            throw ErrorHelper.Forbidden("Only the author or advisor can resolve a suggestion.");
        }
    }

    private async Task<IReadOnlyList<Guid>> ValidateCorrectionReferencesAsync(
        Guid programId,
        IReadOnlyList<Guid>? referenceIds)
    {
        var ids = referenceIds ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        if (ids.Count > 10 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
        {
            throw ErrorHelper.BadRequest("Correction reference ids must be unique and contain at most 10 items.");
        }

        var references = await _unitOfWork.ProgramAdvisoryReferences.GetAllAsync(
            r => ids.Contains(r.Id) && r.ProgramId == programId && !r.IsDeleted);
        if (references.Count != ids.Count)
        {
            throw ErrorHelper.BadRequest("All correction references must belong to this program.");
        }

        return ids;
    }

    private static string? NormalizeOperationId(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        var normalized = operationId.Trim();
        if (normalized.Length > 100)
        {
            throw ErrorHelper.BadRequest("ClientOperationId must be at most 100 characters.");
        }

        return normalized;
    }

    private static void EnsureReviewNotesMutable(Program program)
    {
        if (program.Status is not (ProgramStatus.Draft or ProgramStatus.PendingReview))
        {
            throw ErrorHelper.Conflict("Review notes are read-only after the program decision is complete.");
        }
    }

    private async Task<List<AdvisoryParticipantDto>> BuildParticipantsAsync(Program program)
    {
        var result = new List<AdvisoryParticipantDto>();
        if (program.AdvisorExpertId.HasValue)
        {
            var advisor = await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value);
            if (advisor is { IsDeleted: false, UserId: not null })
            {
                result.Add(new AdvisoryParticipantDto
                {
                    UserId = advisor.UserId.Value,
                    ExpertId = advisor.Id,
                    DisplayName = advisor.FullName,
                    Role = "Advisor",
                    IsAdvisor = true,
                });
            }
        }

        var boards = await _unitOfWork.ProgramBoards.GetAllAsync(
            b => b.ProgramId == program.Id && !b.IsDeleted);
        var expertIds = boards.Select(b => b.ExpertId).Distinct().ToList();
        if (expertIds.Count > 0)
        {
            var experts = await _unitOfWork.Experts.GetAllAsync(e => expertIds.Contains(e.Id) && !e.IsDeleted);
            foreach (var expert in experts)
            {
                if (!expert.UserId.HasValue || result.Any(p => p.ExpertId == expert.Id))
                {
                    continue;
                }

                result.Add(new AdvisoryParticipantDto
                {
                    UserId = expert.UserId.Value,
                    ExpertId = expert.Id,
                    DisplayName = expert.FullName,
                    Role = "BoardExpert",
                    IsAdvisor = false,
                });
            }
        }

        return result;
    }

    private async Task<AdvisoryWorkflowTimelineDto> BuildWorkflowTimelineAsync(
        Program program,
        bool isAdvisor,
        bool canStaff,
        IReadOnlyList<ProgramReviewSubmission> submissions,
        IReadOnlyList<ProgramAdvisoryThread> threads)
    {
        var orderedSubmissions = submissions
            .OrderBy(s => s.SubmissionNumber)
            .ThenBy(s => s.SubmittedAt)
            .ToList();
        var reviews = await _unitOfWork.CurriculumReviews.GetAllAsync(
            r => r.ProgramId == program.Id && !r.IsDeleted);
        var orderedReviews = reviews
            .OrderBy(r => r.Round)
            .ThenBy(r => r.ReviewedAt)
            .ToList();

        var pendingSubmission = orderedSubmissions
            .Where(s => s.Status == ProgramReviewSubmissionStatus.Pending)
            .LastOrDefault();
        var latestSubmission = orderedSubmissions.LastOrDefault();
        var initialSubmission = orderedSubmissions.FirstOrDefault(
            s => s.ReviewRoundIntent == ProgramReviewSubmissionIntent.InitialReview)
            ?? orderedSubmissions.FirstOrDefault(s => s.SubmissionNumber == 1)
            ?? orderedSubmissions.FirstOrDefault();
        var requestChangesSubmission = orderedSubmissions
            .Where(s => s.Status == ProgramReviewSubmissionStatus.ChangesRequested)
            .LastOrDefault();
        var revisionSubmission = orderedSubmissions
            .Where(s => s.ReviewRoundIntent == ProgramReviewSubmissionIntent.RevisionVerification)
            .LastOrDefault();
        var hasChangesRequested = orderedReviews.Any(
            r => r.Decision == CurriculumReviewDecision.ChangesRequested)
            || orderedSubmissions.Any(s => s.Status == ProgramReviewSubmissionStatus.ChangesRequested);
        var hasRevisionIntent = orderedSubmissions.Any(
            s => s.ReviewRoundIntent == ProgramReviewSubmissionIntent.RevisionVerification);

        var currentStage = program.Status switch
        {
            ProgramStatus.PendingReview => AdvisoryWorkflowStage.Review,
            ProgramStatus.Draft => hasChangesRequested || hasRevisionIntent
                ? AdvisoryWorkflowStage.Revision
                : AdvisoryWorkflowStage.Preparation,
            ProgramStatus.Approved => AdvisoryWorkflowStage.AwaitingPublication,
            ProgramStatus.Active or ProgramStatus.Inactive => AdvisoryWorkflowStage.Published,
            _ => AdvisoryWorkflowStage.Preparation,
        };

        var outstandingRequirementCount = threads.Count(
            t => t.Type == ProgramAdvisoryThreadType.RequiredChange
                 && t.Status != ProgramAdvisoryThreadStatus.Resolved);
        var responsibleRole = currentStage switch
        {
            AdvisoryWorkflowStage.Review => AdvisoryResponsibleRole.Advisor,
            AdvisoryWorkflowStage.Preparation
                or AdvisoryWorkflowStage.Revision
                or AdvisoryWorkflowStage.AwaitingPublication
                => AdvisoryResponsibleRole.Manager,
            _ => (AdvisoryResponsibleRole?)null,
        };

        Guid? responsibleUserId = null;
        if (responsibleRole == AdvisoryResponsibleRole.Advisor
            && program.AdvisorExpertId.HasValue)
        {
            var advisor = await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value);
            responsibleUserId = advisor?.IsDeleted == false && advisor.UserId.HasValue
                ? advisor.UserId.Value
                : null;
        }
        else if (responsibleRole == AdvisoryResponsibleRole.Manager)
        {
            var managerSubmission = currentStage == AdvisoryWorkflowStage.Revision
                ? requestChangesSubmission ?? revisionSubmission ?? latestSubmission
                : latestSubmission;
            responsibleUserId = managerSubmission?.SubmittedByManagerId
                ?? (program.CreatedBy == Guid.Empty ? null : program.CreatedBy);
        }

        var currentSubmissionId = currentStage switch
        {
            AdvisoryWorkflowStage.Review => pendingSubmission?.Id,
            AdvisoryWorkflowStage.Revision => requestChangesSubmission?.Id
                ?? revisionSubmission?.Id
                ?? latestSubmission?.Id,
            AdvisoryWorkflowStage.AwaitingPublication or AdvisoryWorkflowStage.Published => latestSubmission?.Id,
            _ => null,
        };

        var stageSubmissionIds = new Dictionary<AdvisoryWorkflowStage, Guid?>
        {
            [AdvisoryWorkflowStage.Preparation] = initialSubmission?.Id,
            [AdvisoryWorkflowStage.Review] = currentStage == AdvisoryWorkflowStage.Review
                ? pendingSubmission?.Id ?? initialSubmission?.Id
                : initialSubmission?.Id,
            [AdvisoryWorkflowStage.Revision] = requestChangesSubmission?.Id ?? revisionSubmission?.Id,
            [AdvisoryWorkflowStage.AwaitingPublication] = latestSubmission?.Id,
            [AdvisoryWorkflowStage.Published] = latestSubmission?.Id,
        };

        var stages = Enum.GetValues<AdvisoryWorkflowStage>()
            .Select(stage => new AdvisoryWorkflowStageDto
            {
                Key = stage.ToString(),
                State = ResolveWorkflowStageState(
                    stage,
                    currentStage,
                    hasChangesRequested,
                    hasRevisionIntent),
                SubmissionId = stageSubmissionIds[stage],
            })
            .ToList();

        return new AdvisoryWorkflowTimelineDto
        {
            CurrentStage = currentStage,
            CurrentSubmissionId = currentSubmissionId,
            ResponsibleRole = responsibleRole,
            ResponsibleUserId = responsibleUserId,
            OutstandingRequirementCount = outstandingRequirementCount,
            Round = pendingSubmission?.SubmissionNumber
                ?? latestSubmission?.SubmissionNumber
                ?? 0,
            NextAction = ResolveNextAction(program, isAdvisor, canStaff, threads, submissions
                .Where(s => s.Status == ProgramReviewSubmissionStatus.Pending)
                .GroupBy(s => s.ProgramId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SubmissionNumber).First())),
            Stages = stages,
        };
    }

    private static AdvisoryWorkflowStageState ResolveWorkflowStageState(
        AdvisoryWorkflowStage stage,
        AdvisoryWorkflowStage currentStage,
        bool hasChangesRequested,
        bool hasRevisionIntent)
    {
        if (stage == currentStage)
        {
            return AdvisoryWorkflowStageState.Current;
        }

        if (stage == AdvisoryWorkflowStage.Revision
            && !hasChangesRequested
            && !hasRevisionIntent
            && currentStage > AdvisoryWorkflowStage.Revision)
        {
            return AdvisoryWorkflowStageState.Skipped;
        }

        if (stage == AdvisoryWorkflowStage.Revision
            && currentStage == AdvisoryWorkflowStage.Review
            && (hasChangesRequested || hasRevisionIntent))
        {
            return AdvisoryWorkflowStageState.Completed;
        }

        return stage < currentStage
            ? AdvisoryWorkflowStageState.Completed
            : AdvisoryWorkflowStageState.Upcoming;
    }

    private static AdvisoryFeedbackCountsDto BuildFeedbackCounts(IReadOnlyList<ProgramAdvisoryThread> threads)
    {
        return new AdvisoryFeedbackCountsDto
        {
            OpenSuggestions = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.Suggestion && t.Status == ProgramAdvisoryThreadStatus.Open),
            AddressedSuggestions = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.Suggestion && t.Status == ProgramAdvisoryThreadStatus.Addressed),
            ResolvedSuggestions = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.Suggestion && t.Status == ProgramAdvisoryThreadStatus.Resolved),
            OpenRequiredChanges = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange && t.Status == ProgramAdvisoryThreadStatus.Open),
            AddressedRequiredChanges = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange && t.Status == ProgramAdvisoryThreadStatus.Addressed),
            ResolvedRequiredChanges = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange && t.Status == ProgramAdvisoryThreadStatus.Resolved),
        };
    }

    private static AdvisoryNextActionDto ResolveNextAction(
        Program program,
        bool isAdvisor,
        bool canStaff,
        IReadOnlyList<ProgramAdvisoryThread> threads,
        IReadOnlyDictionary<Guid, ProgramReviewSubmission> pendingByProgram)
    {
        var openRequired = threads.Any(t =>
            t.Type == ProgramAdvisoryThreadType.RequiredChange
            && t.Status == ProgramAdvisoryThreadStatus.Open);
        var fixedRequired = threads.Any(t =>
            t.Type == ProgramAdvisoryThreadType.RequiredChange
            && t.Status == ProgramAdvisoryThreadStatus.Addressed);

        if (canStaff && program.Status == ProgramStatus.Approved)
        {
            return new AdvisoryNextActionDto { Code = "Publish", ForRole = "Manager" };
        }

        if (canStaff && program.Status == ProgramStatus.Draft && openRequired)
        {
            return new AdvisoryNextActionDto { Code = "FixRequiredChanges", ForRole = "Manager" };
        }

        if (canStaff && program.Status == ProgramStatus.Draft && fixedRequired)
        {
            return new AdvisoryNextActionDto { Code = "ResubmitReady", ForRole = "Manager" };
        }

        if (isAdvisor && program.Status == ProgramStatus.PendingReview && pendingByProgram.ContainsKey(program.Id))
        {
            return new AdvisoryNextActionDto { Code = "ReviewSubmission", ForRole = "Advisor" };
        }

        if (program.Status == ProgramStatus.Draft)
        {
            return new AdvisoryNextActionDto
            {
                Code = "AdviseOptional",
                ForRole = isAdvisor ? "Advisor" : "Manager",
            };
        }

        return new AdvisoryNextActionDto();
    }

    private async Task<Dictionary<Guid, User>> LoadUsersAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, User>();
        }

        var users = await _unitOfWork.Users.GetAllAsync(u => ids.Contains(u.Id) && !u.IsDeleted);
        return users.ToDictionary(u => u.Id);
    }

    private async Task<Program> GetActiveProgramAsync(Guid programId)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        return program;
    }

    private async Task<bool> IsBoardMemberAsync(Guid programId, Guid expertId)
    {
        var board = await _unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == programId && b.ExpertId == expertId && !b.IsDeleted);
        return board != null;
    }

    private async Task<User> ResolveActorAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user.Role is not (RoleType.Expert or RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("Only Expert, Manager, or Admin can access advisory workspaces.");
        }

        return user;
    }

    private async Task<User> GetCurrentUserAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        return user;
    }

    private async Task<Expert> RequireCurrentExpertAsync(User actor)
    {
        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(
            e => e.UserId == actor.Id && !e.IsDeleted);
        if (expert == null)
        {
            throw ErrorHelper.Forbidden("Current user is not linked to an expert profile.");
        }

        return expert;
    }

    private static string DisplayName(User user)
        => string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;

    private async Task<ProgramAdvisoryThread> EnsureGeneralThreadAsync(Program program, User actor)
    {
        var existing = await _unitOfWork.ProgramAdvisoryThreads.FirstOrDefaultAsync(
            t => t.ProgramId == program.Id
                 && t.Type == ProgramAdvisoryThreadType.General
                 && !t.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var now = _currentTime.GetCurrentTime().ToUniversalTime();
        var thread = new ProgramAdvisoryThread
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            AuthorUserId = actor.Id,
            TargetType = ProgramAdvisoryTargetType.Program,
            TargetId = program.Id,
            TargetLabel = program.Name,
            TargetContext = "General discussion",
            Type = ProgramAdvisoryThreadType.General,
            Status = ProgramAdvisoryThreadStatus.Open,
            ConcurrencyVersion = Guid.NewGuid(),
            LatestActivitySequence = 0,
            LastMessageAt = now,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        await _unitOfWork.ProgramAdvisoryThreads.AddAsync(thread);
        await _unitOfWork.SaveChangesAsync();
        return thread;
    }

    private async Task<LiveTargetIndex> LoadLiveTargetIndexAsync(Program program)
    {
        var index = new LiveTargetIndex();
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == program.Id && !m.IsDeleted);
        foreach (var module in modules)
        {
            index.Modules[module.Id] = module.Id;
        }

        var moduleIds = modules.Select(m => m.Id).ToList();
        if (moduleIds.Count == 0)
        {
            await LoadCriteriaAsync(program, index);
            return index;
        }

        var courses = await _unitOfWork.Courses.GetAllAsync(
            c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
        foreach (var course in courses)
        {
            index.Courses[course.Id] = (course.ModuleId, course.Id);
        }

        var courseIds = courses.Select(c => c.Id).ToList();
        var activities = courseIds.Count == 0
            ? []
            : await _unitOfWork.Activities.GetAllAsync(
                a => courseIds.Contains(a.CourseId) && !a.IsDeleted);
        foreach (var activity in activities)
        {
            if (!index.Courses.TryGetValue(activity.CourseId, out var course))
            {
                continue;
            }

            index.Activities[activity.Id] = (course.ModuleId, activity.CourseId, activity.Id);
        }

        var activityIds = activities.Select(a => a.Id).ToList();
        if (activityIds.Count > 0)
        {
            var materials = await _unitOfWork.Materials.GetAllAsync(
                m => activityIds.Contains(m.ActivityId) && !m.IsDeleted);
            foreach (var material in materials)
            {
                if (index.Activities.TryGetValue(material.ActivityId, out var activity))
                {
                    index.Materials[material.Id] = activity;
                }
            }
        }

        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted);
        foreach (var assignment in assignments)
        {
            index.Assignments[assignment.Id] = (assignment.ModuleId, assignment.CourseId, assignment.Id);
        }

        var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(
            m => moduleIds.Contains(m.ModuleId) && !m.IsDeleted);
        foreach (var milestone in milestones)
        {
            index.Milestones[milestone.Id] = (milestone.ModuleId, milestone.AssignmentId);
        }

        await LoadCriteriaAsync(program, index);
        return index;
    }

    private async Task LoadCriteriaAsync(Program program, LiveTargetIndex index)
    {
        if (!program.FrameworkVersionId.HasValue)
        {
            return;
        }

        var criteria = await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
            c => c.FrameworkVersionId == program.FrameworkVersionId && !c.IsDeleted);
        foreach (var criterion in criteria)
        {
            index.Criteria.Add(criterion.Id);
        }
    }

    private static void ApplyLiveTarget(
        AdvisoryThreadDto dto,
        ProgramAdvisoryThread thread,
        LiveTargetIndex index)
    {
        if (thread.Type == ProgramAdvisoryThreadType.General
            || thread.TargetType == ProgramAdvisoryTargetType.Program)
        {
            dto.TargetExists = true;
            dto.TargetPath = new AdvisoryTargetPathDto();
            return;
        }

        if (!thread.TargetId.HasValue)
        {
            dto.TargetExists = false;
            dto.TargetPath = new AdvisoryTargetPathDto();
            return;
        }

        var id = thread.TargetId.Value;
        switch (thread.TargetType)
        {
            case ProgramAdvisoryTargetType.Module when index.Modules.ContainsKey(id):
                dto.TargetExists = true;
                dto.TargetPath = new AdvisoryTargetPathDto { ModuleId = id };
                return;
            case ProgramAdvisoryTargetType.Course when index.Courses.TryGetValue(id, out var course):
                dto.TargetExists = true;
                dto.TargetPath = new AdvisoryTargetPathDto { ModuleId = course.ModuleId, CourseId = course.CourseId };
                return;
            case ProgramAdvisoryTargetType.Activity when index.Activities.TryGetValue(id, out var activity):
                dto.TargetExists = true;
                dto.TargetPath = new AdvisoryTargetPathDto
                {
                    ModuleId = activity.ModuleId,
                    CourseId = activity.CourseId,
                    ActivityId = activity.ActivityId,
                };
                return;
            case ProgramAdvisoryTargetType.Material when index.Materials.TryGetValue(id, out var material):
                dto.TargetExists = true;
                dto.TargetPath = new AdvisoryTargetPathDto
                {
                    ModuleId = material.ModuleId,
                    CourseId = material.CourseId,
                    ActivityId = material.ActivityId,
                };
                return;
            case ProgramAdvisoryTargetType.Assignment when index.Assignments.TryGetValue(id, out var assignment):
                dto.TargetExists = true;
                dto.TargetPath = new AdvisoryTargetPathDto
                {
                    ModuleId = assignment.ModuleId,
                    CourseId = assignment.CourseId,
                    AssignmentId = assignment.AssignmentId,
                };
                return;
            case ProgramAdvisoryTargetType.ResearchMilestone when index.Milestones.TryGetValue(id, out var milestone):
                dto.TargetExists = true;
                dto.TargetPath = new AdvisoryTargetPathDto
                {
                    ModuleId = milestone.ModuleId,
                    AssignmentId = milestone.AssignmentId,
                };
                return;
            case ProgramAdvisoryTargetType.RubricCriterion:
                dto.TargetExists = index.Criteria.Contains(id);
                dto.TargetPath = new AdvisoryTargetPathDto();
                return;
            default:
                dto.TargetExists = false;
                dto.TargetPath = new AdvisoryTargetPathDto();
                return;
        }
    }

    private sealed class LiveTargetIndex
    {
        public Dictionary<Guid, Guid> Modules { get; } = [];
        public Dictionary<Guid, (Guid ModuleId, Guid CourseId)> Courses { get; } = [];
        public Dictionary<Guid, (Guid ModuleId, Guid CourseId, Guid ActivityId)> Activities { get; } = [];
        public Dictionary<Guid, (Guid ModuleId, Guid CourseId, Guid ActivityId)> Materials { get; } = [];
        public Dictionary<Guid, (Guid ModuleId, Guid? CourseId, Guid AssignmentId)> Assignments { get; } = [];
        public Dictionary<Guid, (Guid ModuleId, Guid AssignmentId)> Milestones { get; } = [];
        public HashSet<Guid> Criteria { get; } = [];
    }

    private static AdvisoryThreadDto MapThread(
        ProgramAdvisoryThread thread,
        User? author,
        int messageCount,
        string? latestMessage = null,
        string? targetLabel = null,
        string? targetContext = null)
        => new()
        {
            Id = thread.Id,
            ProgramId = thread.ProgramId,
            SubmissionId = thread.SubmissionId,
            AuthorUserId = thread.AuthorUserId,
            AuthorName = author == null ? null : DisplayName(author),
            TargetType = thread.TargetType,
            TargetId = thread.TargetId,
            TargetLabel = targetLabel ?? thread.TargetLabel,
            TargetContext = targetContext ?? thread.TargetContext,
            Type = thread.Type,
            Status = thread.Status,
            AnchorKind = thread.AnchorKind,
            AnchorField = thread.AnchorField,
            AnchorQuote = thread.AnchorQuote,
            LatestMessagePreview = Preview(latestMessage),
            LastMessageAt = thread.LastMessageAt,
            CreatedAt = thread.CreatedAt,
            MessageCount = messageCount,
            ConcurrencyVersion = thread.ConcurrencyVersion,
            LatestActivitySequence = thread.LatestActivitySequence,
        };

    private static void ApplyThreadCapabilities(
        AdvisoryThreadDto dto,
        ProgramAdvisoryThread thread,
        User actor,
        bool isAdvisor,
        bool isBoard,
        bool canStaff,
        ProgramStatus programStatus)
    {
        // Address only while curriculum is editable (Draft / Revision). Frozen PendingReview cannot Address.
        var curriculumEditable = programStatus == ProgramStatus.Draft;
        var notesMutable = programStatus is ProgramStatus.Draft or ProgramStatus.PendingReview;
        var actions = new List<string>();
        if (thread.Type == ProgramAdvisoryThreadType.RequiredChange
            && thread.Status == ProgramAdvisoryThreadStatus.Open
            && curriculumEditable
            && canStaff)
        {
            actions.Add(nameof(AdvisoryThreadAction.MarkFixed));
        }

        if (thread.Type == ProgramAdvisoryThreadType.Suggestion
            && thread.Status == ProgramAdvisoryThreadStatus.Open
            && notesMutable
            && (canStaff || thread.AuthorUserId == actor.Id))
        {
            actions.Add(nameof(AdvisoryThreadAction.Acknowledge));
        }

        if (thread.Type == ProgramAdvisoryThreadType.RequiredChange
            && thread.Status == ProgramAdvisoryThreadStatus.Addressed
            && programStatus == ProgramStatus.PendingReview
            && isAdvisor)
        {
            actions.Add(nameof(AdvisoryThreadAction.Accept));
        }

        dto.AvailableActions = actions;
        _ = isBoard;
    }

    private static void ApplyOriginRound(
        AdvisoryThreadDto dto,
        ProgramAdvisoryThread thread,
        IReadOnlyDictionary<Guid, ProgramReviewSubmission> submissionsById)
    {
        if (!thread.SubmissionId.HasValue
            || !submissionsById.TryGetValue(thread.SubmissionId.Value, out var submission))
        {
            return;
        }

        dto.OriginSubmissionNumber = submission.SubmissionNumber;
        dto.OriginReviewRoundIntent = submission.ReviewRoundIntent;
        var intentLabel = submission.ReviewRoundIntent?.ToString() ?? "Review";
        dto.OriginRoundLabel = $"Round {submission.SubmissionNumber} · {intentLabel}";
    }

    private static AdvisoryThreadEventDto MapThreadEvent(ProgramAdvisoryThreadEvent row)
        => new()
        {
            Id = row.Id,
            ThreadId = row.ThreadId,
            Sequence = row.Sequence,
            EventType = row.EventType,
            ActorUserId = row.ActorUserId,
            PriorStatus = row.PriorStatus,
            NewStatus = row.NewStatus,
            Message = row.Message,
            ResolutionKind = row.ResolutionKind,
            VerifiedAgainstSubmissionId = row.VerifiedAgainstSubmissionId,
            CorrectionReferenceIds = string.IsNullOrWhiteSpace(row.CorrectionReferenceIdsJson)
                ? []
                : JsonSerializer.Deserialize<List<Guid>>(row.CorrectionReferenceIdsJson) ?? [],
            OperationId = row.OperationId,
            CreatedAt = row.CreatedAt,
        };

    private static AdvisoryCapabilitiesDto BuildCapabilities(
        Program program,
        User actor,
        bool isAdvisor,
        bool isBoard,
        bool canStaff)
    {
        var notesMutable = program.Status is ProgramStatus.Draft or ProgramStatus.PendingReview;
        var reviewActionsLocked = program.Status is ProgramStatus.Approved or ProgramStatus.Active or ProgramStatus.Inactive;
        return new AdvisoryCapabilitiesDto
        {
            // Manager/Admin never create suggestions or required changes.
            CanCreateSuggestion = notesMutable && actor.Role == RoleType.Expert && (isAdvisor || isBoard),
            CanCreateRequiredChange = notesMutable && actor.Role == RoleType.Expert && isAdvisor,
            CanReply = notesMutable && (canStaff || isAdvisor || isBoard),
            CanEditCurriculum = canStaff && program.Status == ProgramStatus.Draft,
            CanAssignAdvisor = canStaff && program.Status == ProgramStatus.Draft,
            CanDecide = isAdvisor && program.Status == ProgramStatus.PendingReview && !reviewActionsLocked,
        };
    }

    public async Task<AdvisoryWorkflowTimelineDto> GetWorkflowTimelineAsync(Guid programId)
    {
        var (program, _, _, isAdvisor, _, canStaff) = await RequireAdvisoryAccessAsync(programId);
        var submissions = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
            s => s.ProgramId == programId && !s.IsDeleted);
        var threads = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == programId && !t.IsDeleted);
        return await BuildWorkflowTimelineAsync(program, isAdvisor, canStaff, submissions, threads);
    }

    private static ProgramReviewSubmissionSummaryDto MapSubmissionSummary(ProgramReviewSubmission submission)
        => new()
        {
            Id = submission.Id,
            SubmissionNumber = submission.SubmissionNumber,
            Status = submission.Status,
            ReviewRoundIntent = submission.ReviewRoundIntent,
            AssignedAdvisorExpertId = submission.AssignedAdvisorExpertId,
            FrameworkVersionId = submission.FrameworkVersionId,
            SubmittedAt = submission.SubmittedAt,
            ClosedAt = submission.ClosedAt,
            ConcurrencyVersion = submission.ConcurrencyVersion,
        };

    private static string? Preview(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        const int maxLength = 180;
        var normalized = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..(maxLength - 1)] + "…";
    }

    private static AdvisoryMessageDto MapMessage(ProgramAdvisoryMessage message, User? author)
        => new()
        {
            Id = message.Id,
            ThreadId = message.ThreadId,
            AuthorUserId = message.AuthorUserId,
            AuthorName = author == null ? null : DisplayName(author),
            Message = message.Message,
            CreatedAt = message.CreatedAt,
        };
}
