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
            items.Add(new AdvisoryMineItemDto
            {
                ProgramId = program.Id,
                Code = program.Code,
                Name = program.Name,
                IsAdvisor = isAdvisor || actor.Role is RoleType.Manager or RoleType.Admin,
                FrameworkVersionNumber = versionNumber,
                Status = program.Status,
                LatestActivityAt = latestActivity,
                NextAction = ResolveNextAction(program, isAdvisor, programThreads, pendingByProgram),
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
        var (program, actor, expert, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
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
        var workflow = await BuildWorkflowTimelineAsync(program, allSubmissions, threads);

        var read = await _unitOfWork.ProgramAdvisoryReads.FirstOrDefaultAsync(
            r => r.ProgramId == program.Id && r.UserId == actor.Id && !r.IsDeleted);
        var threadReads = await _unitOfWork.ProgramAdvisoryStreamReads.GetAllAsync(
            r => r.ProgramId == program.Id
                 && r.UserId == actor.Id
                 && r.StreamType == AdvisoryStreamType.Thread
                 && !r.IsDeleted);
        var discussionMessages = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
            m => m.ProgramId == program.Id && !m.IsDeleted);
        var discussionRead = await _unitOfWork.ProgramAdvisoryStreamReads.FirstOrDefaultAsync(
            r => r.ProgramId == program.Id
                 && r.UserId == actor.Id
                 && r.StreamType == AdvisoryStreamType.Discussion
                 && !r.IsDeleted);
        var openRequired = threads.Count(t =>
            t.Type == ProgramAdvisoryThreadType.RequiredChange
            && t.Status != ProgramAdvisoryThreadStatus.Resolved);
        var capabilities = BuildCapabilities(program, actor, isAdvisor, isBoard, canStaff);
        var reviewActionsLocked = program.Status is ProgramStatus.Approved or ProgramStatus.Active or ProgramStatus.Inactive;
        var unreadNoteCount = threads.Count(t =>
        {
            var threadRead = threadReads.FirstOrDefault(r => r.ThreadId == t.Id)?.LastReadSequence;
            return t.LatestActivitySequence > 0
                ? (!threadRead.HasValue || threadRead.Value < t.LatestActivitySequence)
                : (read == null || t.LastMessageAt > read.LastReadAt);
        });
        var unreadDiscussionCount = discussionMessages.Count(
            message => message.Sequence > (discussionRead?.LastReadSequence ?? 0));

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
            CanAdvise = canStaff || isAdvisor || isBoard,
            CanDecide = capabilities.CanDecide,
            CanEditCurriculum = capabilities.CanEditCurriculum,
            CanAssignAdvisor = capabilities.CanAssignAdvisor,
            Capabilities = capabilities,
            Workflow = workflow,
            ApprovalBlockingCount = openRequired,
            OpenRequiredChangeCount = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange
                && t.Status == ProgramAdvisoryThreadStatus.Open),
            AddressedRequiredChangeCount = threads.Count(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange
                && t.Status == ProgramAdvisoryThreadStatus.Addressed),
            UnreadNoteCount = unreadNoteCount,
            UnreadDiscussionCount = unreadDiscussionCount,
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
        var ordered = threads.OrderByDescending(t => t.LastMessageAt).ToList();
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
        var snapshotsBySubmissionId = submissionIds.Count == 0
            ? new Dictionary<Guid, CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument?>()
            : (await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
                    s => submissionIds.Contains(s.Id) && s.ProgramId == programId && !s.IsDeleted))
                .ToDictionary(
                    s => s.Id,
                    s => CurriculumReviewSnapshotBuilder.TryDeserialize(s.CurriculumSnapshotJson));

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
        dto.Events = events.OrderBy(e => e.Sequence).Select(MapThreadEvent).ToList();
        ApplyThreadCapabilities(dto, thread, actor, isAdvisor, isBoard, canStaff, program.Status);
        return dto;
    }

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
        => await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => CreateThreadCoreAsync(programId, request));

    private async Task<AdvisoryThreadDto> CreateThreadCoreAsync(Guid programId, CreateAdvisoryThreadRequest request)
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

        await NotifyAdvisoryAsync(
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
                thread.Type.ToString()));

        var result = MapThread(thread, actor, 1, messageText, label, context);
        result.Events = [MapThreadEvent(createdEvent)];
        ApplyThreadCapabilities(result, thread, actor, isAdvisor, isBoard, canStaff, program.Status);
        return result;
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
        => await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => AddMessageCoreAsync(programId, threadId, message));

    private async Task<AdvisoryMessageDto> AddMessageCoreAsync(Guid programId, Guid threadId, string message)
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
        thread.LatestActivitySequence++;
        thread.ConcurrencyVersion = Guid.NewGuid();
        var row = new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = actor.Id,
            StreamSequence = thread.LatestActivitySequence,
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
            Sequence = thread.LatestActivitySequence,
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

        await NotifyAdvisoryAsync(
            program,
            actor.Id,
            notifyManagers: actor.Role == RoleType.Expert,
            userId => NotificationCatalog.AdvisoryReply(
                userId,
                program.Id,
                thread.Id,
                actor.Id,
                program.Name,
                DisplayName(actor)));

        return MapMessage(row, actor);
    }

    public async Task<AdvisoryThreadDto> UpdateThreadStatusAsync(
        Guid programId,
        Guid threadId,
        UpdateAdvisoryThreadStatusRequest request)
        => await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => UpdateThreadStatusCoreAsync(programId, threadId, request));

    private async Task<AdvisoryThreadDto> UpdateThreadStatusCoreAsync(
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
            return await MapThreadForCurrentActorAsync(program, actor, isAdvisor, isBoard, canStaff, thread);
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

                if (priorStatus != ProgramAdvisoryThreadStatus.Open)
                {
                    throw ErrorHelper.Conflict("Only open feedback can be marked as addressed.");
                }

                break;
            case ProgramAdvisoryThreadStatus.Resolved:
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
        thread.LatestActivitySequence++;
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
                StreamSequence = thread.LatestActivitySequence,
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
            Sequence = thread.LatestActivitySequence,
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

        if (request.Status == ProgramAdvisoryThreadStatus.Addressed)
        {
            await NotifyAdvisoryAsync(
                program,
                actor.Id,
                notifyManagers: false,
                userId => NotificationCatalog.AdvisoryCorrectionAddressed(
                    userId,
                    program.Id,
                    thread.Id,
                    actor.Id,
                    program.Name,
                    DisplayName(actor)));
        }

        var count = (await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
            m => m.ThreadId == thread.Id && !m.IsDeleted)).Count;
        var latest = (await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
                m => m.ThreadId == thread.Id && !m.IsDeleted))
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();
        var author = await _unitOfWork.Users.GetByIdAsync(thread.AuthorUserId);
        var result = MapThread(thread, author, count, latest?.Message);
        result.Events = [MapThreadEvent(statusEvent)];
        ApplyThreadCapabilities(result, thread, actor, isAdvisor, isBoard, canStaff, program.Status);
        return result;
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
            if (!AllowedAnchorFields.TryGetValue(request.TargetType, out var fields)
                || !fields.Contains(request.AnchorField, StringComparer.OrdinalIgnoreCase))
            {
                throw ErrorHelper.BadRequest("AnchorField is not supported for this target type.");
            }
        }
    }

    private static readonly IReadOnlyDictionary<ProgramAdvisoryTargetType, string[]> AllowedAnchorFields =
        new Dictionary<ProgramAdvisoryTargetType, string[]>
        {
            [ProgramAdvisoryTargetType.Program] = ["name", "code", "description", "skillsGained"],
            [ProgramAdvisoryTargetType.Module] = ["name", "code", "type", "learningOutcomes"],
            [ProgramAdvisoryTargetType.Course] = ["name", "code", "description"],
            [ProgramAdvisoryTargetType.Activity] =
                ["name", "type", "description", "durationMinutes", "requireQrCheckin", "requireMediaEvidence"],
            [ProgramAdvisoryTargetType.Assignment] =
                ["title", "code", "description", "assignmentType", "maxPoints", "passScore", "isRequiredForModulePass"],
            [ProgramAdvisoryTargetType.ResearchMilestone] = ["title", "code", "description", "isCapstone"],
            [ProgramAdvisoryTargetType.Material] = ["title", "materialType", "fileName"],
            [ProgramAdvisoryTargetType.RubricCriterion] = ["name", "description", "evidenceGuidance", "maxScore"],
        };

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
            ProgramStatus.PendingReview => pendingSubmission?.ReviewRoundIntent
                == ProgramReviewSubmissionIntent.RevisionVerification
                ? AdvisoryWorkflowStage.Verification
                : AdvisoryWorkflowStage.Review,
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
            AdvisoryWorkflowStage.Review or AdvisoryWorkflowStage.Verification
                => AdvisoryResponsibleRole.Advisor,
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
            AdvisoryWorkflowStage.Review or AdvisoryWorkflowStage.Verification => pendingSubmission?.Id,
            AdvisoryWorkflowStage.Revision => requestChangesSubmission?.Id
                ?? revisionSubmission?.Id
                ?? latestSubmission?.Id,
            AdvisoryWorkflowStage.AwaitingPublication or AdvisoryWorkflowStage.Published => latestSubmission?.Id,
            _ => null,
        };

        var stageSubmissionIds = new Dictionary<AdvisoryWorkflowStage, Guid?>
        {
            [AdvisoryWorkflowStage.Preparation] = initialSubmission?.Id,
            [AdvisoryWorkflowStage.Review] = initialSubmission?.Id,
            [AdvisoryWorkflowStage.Revision] = requestChangesSubmission?.Id ?? revisionSubmission?.Id,
            [AdvisoryWorkflowStage.Verification] = revisionSubmission?.Id
                ?? (currentStage == AdvisoryWorkflowStage.Verification ? pendingSubmission?.Id : null),
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

        if (stage is (AdvisoryWorkflowStage.Revision or AdvisoryWorkflowStage.Verification)
            && !hasChangesRequested
            && !hasRevisionIntent
            && currentStage > AdvisoryWorkflowStage.Verification)
        {
            return AdvisoryWorkflowStageState.Skipped;
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

    private static string ResolveNextAction(
        Program program,
        bool isAdvisor,
        IReadOnlyList<ProgramAdvisoryThread> threads,
        IReadOnlyDictionary<Guid, ProgramReviewSubmission> pendingByProgram)
    {
        if (isAdvisor && program.Status == ProgramStatus.PendingReview && pendingByProgram.ContainsKey(program.Id))
        {
            return "ReviewSubmission";
        }

        if (isAdvisor
            && threads.Any(t =>
                t.Type == ProgramAdvisoryThreadType.RequiredChange
                && t.Status == ProgramAdvisoryThreadStatus.Addressed))
        {
            return "VerifyAddressed";
        }

        if (program.Status == ProgramStatus.Draft)
        {
            return "AdviseOptional";
        }

        return "None";
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
        var mutable = programStatus is ProgramStatus.Draft or ProgramStatus.PendingReview;
        dto.CanAddress = mutable
            && canStaff
            && thread.Status == ProgramAdvisoryThreadStatus.Open;
        dto.CanResolve = mutable
            && thread.Status is (ProgramAdvisoryThreadStatus.Open or ProgramAdvisoryThreadStatus.Addressed)
            && (thread.Type == ProgramAdvisoryThreadType.RequiredChange
                ? isAdvisor
                : isAdvisor || thread.AuthorUserId == actor.Id);
        dto.CanReopen = mutable
            && thread.Status != ProgramAdvisoryThreadStatus.Open
            && (thread.Type == ProgramAdvisoryThreadType.RequiredChange
                ? isAdvisor
                : isAdvisor || thread.AuthorUserId == actor.Id || canStaff);
        dto.CanWaive = mutable
            && thread.Type == ProgramAdvisoryThreadType.RequiredChange
            && isAdvisor
            && thread.Status != ProgramAdvisoryThreadStatus.Resolved;
        _ = isBoard;
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
        var mutableNotes = program.Status is ProgramStatus.Draft or ProgramStatus.PendingReview;
        return new AdvisoryCapabilitiesDto
        {
            CanCreateSuggestion = mutableNotes && actor.Role == RoleType.Expert && (isAdvisor || isBoard),
            CanCreateRequiredChange = mutableNotes && actor.Role == RoleType.Expert && isAdvisor,
            CanDiscuss = true,
            CanReplyToNotes = mutableNotes && (canStaff || isAdvisor || isBoard),
            CanEditCurriculum = canStaff && program.Status == ProgramStatus.Draft,
            CanAssignAdvisor = canStaff && program.Status == ProgramStatus.Draft,
            CanDecide = isAdvisor && program.Status == ProgramStatus.PendingReview,
        };
    }

    public async Task<AdvisoryWorkflowTimelineDto> GetWorkflowTimelineAsync(Guid programId)
    {
        var (program, _, _, _, _, _) = await RequireAdvisoryAccessAsync(programId);
        var submissions = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
            s => s.ProgramId == programId && !s.IsDeleted);
        var threads = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == programId && !t.IsDeleted);
        return await BuildWorkflowTimelineAsync(program, submissions, threads);
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
