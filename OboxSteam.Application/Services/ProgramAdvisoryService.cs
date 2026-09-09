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

    public ProgramAdvisoryService(
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

        var latestSubmission = (await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
                s => s.ProgramId == program.Id && !s.IsDeleted))
            .OrderByDescending(s => s.SubmissionNumber)
            .FirstOrDefault();

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

        var read = await _unitOfWork.ProgramAdvisoryReads.FirstOrDefaultAsync(
            r => r.ProgramId == program.Id && r.UserId == actor.Id && !r.IsDeleted);
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
            CanDecide = isAdvisor && program.Status == ProgramStatus.PendingReview,
            CanEditCurriculum = canStaff && program.Status is not (ProgramStatus.PendingReview or ProgramStatus.Approved),
            CanAssignAdvisor = canStaff && program.Status is not (ProgramStatus.PendingReview or ProgramStatus.Approved),
            LatestSubmission = latestSubmission == null
                ? null
                : new ProgramReviewSubmissionSummaryDto
                {
                    Id = latestSubmission.Id,
                    SubmissionNumber = latestSubmission.SubmissionNumber,
                    Status = latestSubmission.Status,
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

    public async Task<IReadOnlyList<AdvisoryThreadDto>> GetThreadsAsync(Guid programId)
    {
        await RequireAdvisoryAccessAsync(programId);
        var threads = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == programId && !t.IsDeleted);
        var ordered = threads.OrderByDescending(t => t.LastMessageAt).ToList();
        if (ordered.Count == 0)
        {
            return [];
        }

        var threadIds = ordered.Select(t => t.Id).ToList();
        var messages = await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
            m => threadIds.Contains(m.ThreadId) && !m.IsDeleted);
        var counts = messages.GroupBy(m => m.ThreadId).ToDictionary(g => g.Key, g => g.Count());
        var authors = await LoadUsersAsync(ordered.Select(t => t.AuthorUserId));

        return ordered
            .Select(t => MapThread(t, authors.GetValueOrDefault(t.AuthorUserId), counts.GetValueOrDefault(t.Id)))
            .ToList();
    }

    public async Task<AdvisoryThreadDto> CreateThreadAsync(Guid programId, CreateAdvisoryThreadRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var (program, actor, expert, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
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
            LastMessageAt = now,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        var message = new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = actor.Id,
            Message = messageText,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };

        await _unitOfWork.ProgramAdvisoryThreads.AddAsync(thread);
        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(message);
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

        return MapThread(thread, actor, 1);
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
        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        if (!(canStaff || isAdvisor || isBoard))
        {
            throw ErrorHelper.Forbidden("You cannot advise on this program.");
        }

        var thread = await RequireThreadAsync(programId, threadId);
        var text = CurriculumReviewValidator.RequireComment(message);
        var now = _currentTime.GetCurrentTime();
        var row = new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = actor.Id,
            Message = text,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        thread.LastMessageAt = now;
        thread.UpdatedAt = now;
        thread.UpdatedBy = actor.Id;

        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(row);
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
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var (program, actor, _, isAdvisor, isBoard, canStaff) = await RequireAdvisoryAccessAsync(programId);
        var thread = await RequireThreadAsync(programId, threadId);
        var now = _currentTime.GetCurrentTime();

        switch (request.Status)
        {
            case ProgramAdvisoryThreadStatus.Addressed:
                if (!canStaff)
                {
                    throw ErrorHelper.Forbidden("Only Manager or Admin can mark feedback as addressed.");
                }

                break;
            case ProgramAdvisoryThreadStatus.Resolved:
                if (thread.Type == ProgramAdvisoryThreadType.RequiredChange)
                {
                    if (!isAdvisor)
                    {
                        throw ErrorHelper.Forbidden("Only the responsible advisor can resolve required changes.");
                    }
                }
                else if (!(isAdvisor || thread.AuthorUserId == actor.Id))
                {
                    throw ErrorHelper.Forbidden("Only the author or advisor can resolve a suggestion.");
                }

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
                throw ErrorHelper.BadRequest("Unsupported thread status.");
        }

        thread.Status = request.Status;
        thread.UpdatedAt = now;
        thread.UpdatedBy = actor.Id;

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            var text = CurriculumReviewValidator.RequireComment(request.Message);
            var followUp = new ProgramAdvisoryMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AuthorUserId = actor.Id,
                Message = text,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };
            thread.LastMessageAt = now;
            await _unitOfWork.ProgramAdvisoryMessages.AddAsync(followUp);
        }

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
        return MapThread(thread, actor, count);
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

    private async Task<ProgramAdvisoryThread> RequireThreadAsync(Guid programId, Guid threadId)
    {
        var thread = await _unitOfWork.ProgramAdvisoryThreads.GetByIdAsync(threadId);
        if (thread == null || thread.IsDeleted || thread.ProgramId != programId)
        {
            throw ErrorHelper.NotFound($"Advisory thread '{threadId}' was not found.");
        }

        return thread;
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

    private static AdvisoryThreadDto MapThread(ProgramAdvisoryThread thread, User? author, int messageCount)
        => new()
        {
            Id = thread.Id,
            ProgramId = thread.ProgramId,
            SubmissionId = thread.SubmissionId,
            AuthorUserId = thread.AuthorUserId,
            AuthorName = author == null ? null : DisplayName(author),
            TargetType = thread.TargetType,
            TargetId = thread.TargetId,
            TargetLabel = thread.TargetLabel,
            TargetContext = thread.TargetContext,
            Type = thread.Type,
            Status = thread.Status,
            LastMessageAt = thread.LastMessageAt,
            CreatedAt = thread.CreatedAt,
            MessageCount = messageCount,
        };

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
