using System.Text.Json;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ProgramApprovalService : IProgramApprovalService
{
    private const string InvalidStatusCode = "INVALID_STATUS";
    private const string VersionStaleCode = "CURRICULUM_VERSION_STALE";
    private const string ApprovalBlockedCode = "APPROVAL_BLOCKED";
    private const string FrameworkCheckFailedCode = "FRAMEWORK_CHECK_FAILED";
    private const string AdvisorRequiredCode = "ADVISOR_REQUIRED";
    private const string AdvisorLoginRequiredCode = "ADVISOR_LOGIN_REQUIRED";
    private const int MaxCommentLength = 2000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ISyncEventPublisher _syncEventPublisher;
    private readonly IProgramService _programService;

    public ProgramApprovalService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        INotificationPublisher notificationPublisher,
        ISyncEventPublisher syncEventPublisher,
        IProgramService programService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _notificationPublisher = notificationPublisher;
        _syncEventPublisher = syncEventPublisher;
        _programService = programService;
    }

    public async Task<ProgramAdvisoryWorkspaceDto> GetWorkspaceAsync(Guid programId)
    {
        var participant = await RequireParticipantAsync(programId);
        return await BuildWorkspaceAsync(participant);
    }

    public async Task<FrameworkCheckDto> GetFrameworkCheckAsync(Guid programId)
    {
        var participant = await RequireParticipantAsync(programId);
        return await ProgramFrameworkCheck.RunAsync(_unitOfWork, participant.Program);
    }

    public async Task<Pagination<AdvisoryMineItemDto>> GetAdvisoryMineAsync(
        int page,
        int pageSize,
        ProgramStatus? status = null,
        bool unreadOnly = false)
    {
        var user = await RequireAdvisoryUserAsync();
        var programs = await _unitOfWork.Programs.GetAllAsync(
            p => !p.IsDeleted && (status == null || p.Status == status));
        Expert? expert = null;
        if (user.Role == RoleType.Expert)
        {
            expert = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == user.Id && !e.IsDeleted)
                ?? throw ErrorHelper.Forbidden("Current user is not linked to an expert profile.");
            var expertId = expert.Id;
            var boardProgramIds = (await _unitOfWork.ProgramBoards.GetAllAsync(
                    b => b.ExpertId == expertId && !b.IsDeleted))
                .Select(b => b.ProgramId)
                .ToHashSet();
            programs = programs
                .Where(p => p.AdvisorExpertId == expertId || boardProgramIds.Contains(p.Id))
                .ToList();
        }

        var programIds = programs.Select(p => p.Id).ToList();
        var versionIds = programs
            .Where(p => p.FrameworkVersionId.HasValue)
            .Select(p => p.FrameworkVersionId!.Value)
            .Distinct()
            .ToList();
        var versionNumbers = (await _unitOfWork.ProgramFrameworkVersions.GetAllAsync(v => versionIds.Contains(v.Id)))
            .ToDictionary(v => v.Id, v => v.VersionNumber);
        var messagesByProgram = (await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
                m => programIds.Contains(m.ProgramId) && !m.IsDeleted))
            .ToLookup(m => m.ProgramId);
        var lastReadByProgram = (await _unitOfWork.ProgramAdvisoryStreamReads.GetAllAsync(
                r => r.UserId == user.Id
                     && programIds.Contains(r.ProgramId)
                     && !r.IsDeleted))
            .GroupBy(r => r.ProgramId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.LastReadSequence));
        var approvalsByProgram = (await _unitOfWork.ProgramApprovals.GetAllAsync(
                a => programIds.Contains(a.ProgramId) && !a.IsDeleted))
            .ToLookup(a => a.ProgramId);

        var items = new List<AdvisoryMineItemDto>();
        foreach (var program in programs)
        {
            var messages = messagesByProgram[program.Id].ToList();
            var liveMessages = messages.Where(m => m.RemovedAt == null).ToList();
            var lastRead = lastReadByProgram.GetValueOrDefault(program.Id);
            var unreadCount = liveMessages.Count(m => m.Sequence > lastRead && m.AuthorUserId != user.Id);
            if (unreadOnly && unreadCount == 0)
            {
                continue;
            }

            items.Add(new AdvisoryMineItemDto
            {
                ProgramId = program.Id,
                Code = program.Code,
                Name = program.Name,
                Status = program.Status,
                FrameworkVersionNumber = program.FrameworkVersionId.HasValue
                    && versionNumbers.TryGetValue(program.FrameworkVersionId.Value, out var versionNumber)
                        ? versionNumber
                        : null,
                IsAdvisor = expert != null && program.AdvisorExpertId == expert.Id,
                LatestActivityAt = messages.Count > 0
                    ? messages.Max(m => m.EditedAt ?? m.CreatedAt)
                    : program.UpdatedAt ?? program.CreatedAt,
                UnreadCount = unreadCount,
                OpenPinCount = liveMessages.Count(m => m.PinStatus == DiscussionPinStatus.Open),
                ApprovalState = ResolveApprovalState(approvalsByProgram[program.Id]),
            });
        }

        var ordered = items
            .OrderByDescending(i => i.LatestActivityAt ?? DateTime.MinValue)
            .ThenBy(i => i.Name)
            .ToList();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new Pagination<AdvisoryMineItemDto>(pageItems, ordered.Count, page, pageSize);
    }

    public async Task<ProgramAdvisoryWorkspaceDto> RequestApprovalAsync(Guid programId)
    {
        var sync = new AdvisorySyncBatch(programId);
        var notification = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => RequestApprovalCoreAsync(programId, sync));
        await _notificationPublisher.PublishAsync(notification);
        await sync.PublishAsync(_syncEventPublisher);
        return await GetWorkspaceAsync(programId);
    }

    private async Task<NotificationCommand> RequestApprovalCoreAsync(Guid programId, AdvisorySyncBatch sync)
    {
        sync.Reset();
        var participant = await RequireManagerAsync(programId);
        var program = participant.Program;
        EnsureStatus(program, ProgramStatus.Draft, "Approval can only be requested while the program is Draft.");

        if (!program.AdvisorExpertId.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Assign a responsible expert before requesting approval.",
                AdvisorRequiredCode);
        }

        var advisor = await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value);
        if (advisor == null || !await ProgramAdvisorResolver.HasActiveLoginAsync(_unitOfWork, advisor))
        {
            throw ErrorHelper.BadRequest(
                "The responsible expert needs an active linked login before approval can be requested.",
                AdvisorLoginRequiredCode);
        }

        var actorName = AdvisoryParticipantAccess.DisplayName(participant.User);
        await DiscussionSystemMessageWriter.AddAsync(
            _unitOfWork,
            program,
            DiscussionSystemEventCode.ApprovalRequested,
            new { requestedByName = actorName },
            Now(),
            participant.User.Id);
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();
        RecordApprovalChange(sync, program);

        return NotificationCatalog.CurriculumApprovalRequested(
            advisor.UserId!.Value,
            program.Id,
            participant.User.Id,
            program.Name,
            actorName);
    }

    public async Task<ProgramAdvisoryWorkspaceDto> ApproveAsync(Guid programId, ApproveProgramRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sync = new AdvisorySyncBatch(programId);
        var notification = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => ApproveCoreAsync(programId, request, sync));
        await _notificationPublisher.PublishAsync(notification);
        await sync.PublishAsync(_syncEventPublisher);
        return await GetWorkspaceAsync(programId);
    }

    private async Task<NotificationCommand> ApproveCoreAsync(
        Guid programId,
        ApproveProgramRequest request,
        AdvisorySyncBatch sync)
    {
        sync.Reset();
        var participant = await RequireParticipantAsync(programId);
        if (participant.Role != AdvisoryParticipantRole.Advisor)
        {
            throw ErrorHelper.Forbidden("Only the program advisor can approve the curriculum.");
        }

        var program = participant.Program;
        EnsureStatus(program, ProgramStatus.Draft, "Only Draft programs can be approved.");
        if (request.CurriculumVersion != program.CurriculumVersion)
        {
            throw ErrorHelper.Conflict(
                $"The curriculum changed after you reviewed it (current version {program.CurriculumVersion}). " +
                "Review the latest changes and approve again.",
                VersionStaleCode);
        }

        var pins = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
            m => m.ProgramId == programId
                 && m.PinStatus != null
                 && m.PinStatus != DiscussionPinStatus.Resolved
                 && m.RemovedAt == null
                 && !m.IsDeleted);
        var openPinCount = pins.Count(m => m.PinStatus == DiscussionPinStatus.Open);
        if (openPinCount > 0)
        {
            throw ErrorHelper.Conflict(
                $"Every pinned item must be addressed or resolved before approving ({openPinCount} still open).",
                ApprovalBlockedCode);
        }

        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
        var check = await ProgramFrameworkCheck.RunAsync(_unitOfWork, program, tree);
        if (!check.AllPassed)
        {
            throw ErrorHelper.Conflict(
                "The curriculum does not pass the framework check.",
                FrameworkCheckFailedCode,
                check);
        }

        var comment = NormalizeComment(request.Comment);
        var now = Now();
        var previous = (await _unitOfWork.ProgramApprovals.GetAllAsync(
                a => a.ProgramId == programId && !a.IsDeleted))
            .OrderByDescending(a => a.ApprovedAt)
            .FirstOrDefault();
        var approval = new ProgramApproval
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            CurriculumVersion = program.CurriculumVersion,
            FromVersion = Math.Min(previous?.CurriculumVersion ?? 0, program.CurriculumVersion),
            FrameworkVersionId = program.FrameworkVersionId,
            FrameworkCheckJson = JsonSerializer.Serialize(check, CurriculumChangeJson.Options),
            CurriculumSnapshotJson = CurriculumSnapshotBuilder.BuildCurriculumSnapshotJson(tree),
            ApprovedByExpertId = program.AdvisorExpertId!.Value,
            ApprovedAt = now,
            Comment = comment,
            CreatedAt = now,
            CreatedBy = participant.User.Id,
        };
        await _unitOfWork.ProgramApprovals.AddAsync(approval);

        var resolvedPins = pins.Where(m => m.PinStatus == DiscussionPinStatus.Addressed).ToList();
        foreach (var pin in resolvedPins)
        {
            pin.PinStatus = DiscussionPinStatus.Resolved;
            pin.ResolvedByUserId = participant.User.Id;
            pin.ResolvedAt = now;
            await _unitOfWork.ProgramAdvisoryDiscussionMessages.Update(pin);
        }

        var approverName = await AdvisorNameAsync(program.AdvisorExpertId.Value)
                           ?? AdvisoryParticipantAccess.DisplayName(participant.User);
        program.Status = ProgramStatus.Approved;
        await DiscussionSystemMessageWriter.AddAsync(
            _unitOfWork,
            program,
            DiscussionSystemEventCode.Approved,
            new
            {
                approvalId = approval.Id,
                curriculumVersion = approval.CurriculumVersion,
                approvedByName = approverName,
                comment,
            },
            now,
            participant.User.Id);
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();
        foreach (var pin in resolvedPins)
        {
            sync.PinChanged(pin.Id);
        }

        RecordApprovalChange(sync, program);

        return NotificationCatalog.CurriculumReviewApproved(
            program.Id,
            approval.Id,
            participant.User.Id,
            program.Name,
            approverName);
    }

    public async Task<ProgramAdvisoryWorkspaceDto> RevokeAsync(Guid programId, RevokeProgramApprovalRequest? request)
    {
        var sync = new AdvisorySyncBatch(programId);
        var notification = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => RevokeCoreAsync(programId, request, sync));
        if (notification != null)
        {
            await _notificationPublisher.PublishAsync(notification);
        }

        await sync.PublishAsync(_syncEventPublisher);
        return await GetWorkspaceAsync(programId);
    }

    private async Task<NotificationCommand?> RevokeCoreAsync(
        Guid programId,
        RevokeProgramApprovalRequest? request,
        AdvisorySyncBatch sync)
    {
        sync.Reset();
        var participant = await RequireParticipantAsync(programId);
        var isAdvisor = participant.Role == AdvisoryParticipantRole.Advisor;
        if (!participant.IsManager && !isAdvisor)
        {
            throw ErrorHelper.Forbidden("Only a manager or the program advisor can revoke the approval.");
        }

        var program = participant.Program;
        EnsureStatus(program, ProgramStatus.Approved, "Only Approved programs can have their approval revoked.");

        var comment = NormalizeComment(request?.Reason);
        var reason = participant.IsManager
            ? ProgramApprovalRevokeReason.ManagerReopened
            : ProgramApprovalRevokeReason.ExpertRevoked;
        var actorName = AdvisoryParticipantAccess.DisplayName(participant.User);
        await RevokeApprovalAsync(program, participant.User, actorName, reason, comment, Now());
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();
        RecordApprovalChange(sync, program);

        if (isAdvisor)
        {
            return NotificationCatalog.CurriculumApprovalRevokedByAdvisor(
                program.Id,
                participant.User.Id,
                program.Name,
                actorName);
        }

        var advisorUserId = await AdvisorUserIdAsync(program);
        return advisorUserId.HasValue && advisorUserId.Value != participant.User.Id
            ? NotificationCatalog.CurriculumApprovalReopened(
                advisorUserId.Value,
                program.Id,
                participant.User.Id,
                program.Name,
                actorName)
            : null;
    }

    public async Task<ProgramsResponseDto> PublishAsync(Guid programId)
    {
        var sync = new AdvisorySyncBatch(programId);
        var notification = await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => PublishCoreAsync(programId, sync));
        await _notificationPublisher.PublishAsync(notification);
        await sync.PublishAsync(_syncEventPublisher);
        return await _programService.GetProgramByIdAsync(programId);
    }

    private async Task<NotificationCommand> PublishCoreAsync(Guid programId, AdvisorySyncBatch sync)
    {
        sync.Reset();
        var participant = await RequireManagerAsync(programId);
        var program = participant.Program;
        EnsureStatus(program, ProgramStatus.Approved, "Only Approved programs can be published.");

        var approval = await FindActiveApprovalAsync(programId);
        if (approval == null || approval.CurriculumVersion != program.CurriculumVersion)
        {
            throw ErrorHelper.Conflict(
                "The approval does not cover the current curriculum version. Ask the advisor to approve again.",
                VersionStaleCode);
        }

        var actorName = AdvisoryParticipantAccess.DisplayName(participant.User);
        program.Status = ProgramStatus.Active;
        await DiscussionSystemMessageWriter.AddAsync(
            _unitOfWork,
            program,
            DiscussionSystemEventCode.Published,
            new { publishedByName = actorName },
            Now(),
            participant.User.Id);
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();
        RecordApprovalChange(sync, program);

        return NotificationCatalog.CurriculumReviewPublished(
            program.Id,
            participant.User.Id,
            program.Name,
            actorName);
    }

    public async Task<ProgramsResponseDto> AssignAdvisorAsync(Guid programId, AssignProgramAdvisorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sync = new AdvisorySyncBatch(programId);
        await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => AssignAdvisorCoreAsync(programId, request, sync));
        await sync.PublishAsync(_syncEventPublisher);
        return await _programService.GetProgramByIdAsync(programId);
    }

    private async Task<bool> AssignAdvisorCoreAsync(
        Guid programId,
        AssignProgramAdvisorRequest request,
        AdvisorySyncBatch sync)
    {
        sync.Reset();
        var participant = await RequireManagerAsync(programId);
        var program = participant.Program;
        if (program.Status is not (ProgramStatus.Draft or ProgramStatus.Approved))
        {
            throw ErrorHelper.Conflict(
                "The responsible expert can only be changed while the program is Draft or Approved.",
                InvalidStatusCode);
        }

        var advisor = await ProgramAdvisorResolver.ResolveAsync(_unitOfWork, request.AdvisorExpertId)
                      ?? throw ErrorHelper.BadRequest("AdvisorExpertId is required.");
        var board = await _unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == program.Id && b.ExpertId == advisor.Id && !b.IsDeleted);
        if (board == null)
        {
            await _unitOfWork.ProgramBoards.AddAsync(new ProgramBoard
            {
                Id = Guid.NewGuid(),
                ProgramId = program.Id,
                ExpertId = advisor.Id,
                RoleInBoard = "Responsible advisor",
            });
        }

        var previousAdvisorId = program.AdvisorExpertId;
        var advisorChanged = previousAdvisorId != advisor.Id;
        var revoked = false;
        if (advisorChanged)
        {
            var now = Now();
            var actorName = AdvisoryParticipantAccess.DisplayName(participant.User);
            var previousName = previousAdvisorId.HasValue ? await AdvisorNameAsync(previousAdvisorId.Value) : null;
            program.AdvisorExpertId = advisor.Id;
            await DiscussionSystemMessageWriter.AddAsync(
                _unitOfWork,
                program,
                DiscussionSystemEventCode.AdvisorChanged,
                new { previousAdvisorName = previousName, newAdvisorName = advisor.FullName },
                now,
                participant.User.Id);

            if (program.Status == ProgramStatus.Approved)
            {
                await RevokeApprovalAsync(
                    program,
                    participant.User,
                    actorName,
                    ProgramApprovalRevokeReason.AdvisorChanged,
                    comment: null,
                    now);
                revoked = true;
            }
        }

        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();
        if (revoked)
        {
            RecordApprovalChange(sync, program);
        }
        else if (advisorChanged)
        {
            sync.DiscussionChanged(program.AdvisoryDiscussionSequence);
        }

        return true;
    }

    /// <summary>Queues approvalChanged plus discussionChanged for the lifecycle system message just written.</summary>
    private static void RecordApprovalChange(AdvisorySyncBatch sync, Program program)
    {
        sync.ApprovalChanged(program.Status, program.CurriculumVersion);
        sync.DiscussionChanged(program.AdvisoryDiscussionSequence);
    }

    /// <summary>Revokes the active approval (if any), returns the program to Draft, and posts ApprovalRevoked.</summary>
    private async Task RevokeApprovalAsync(
        Program program,
        User actor,
        string actorName,
        ProgramApprovalRevokeReason reason,
        string? comment,
        DateTime now)
    {
        var approval = await FindActiveApprovalAsync(program.Id);
        if (approval != null)
        {
            approval.RevokedAt = now;
            approval.RevokedByUserId = actor.Id;
            approval.RevokeReason = reason;
            approval.RevokeComment = comment;
            approval.UpdatedAt = now;
            approval.UpdatedBy = actor.Id;
            await _unitOfWork.ProgramApprovals.Update(approval);
        }

        program.Status = ProgramStatus.Draft;
        await DiscussionSystemMessageWriter.AddAsync(
            _unitOfWork,
            program,
            DiscussionSystemEventCode.ApprovalRevoked,
            new
            {
                approvalId = approval?.Id,
                reason = reason.ToString(),
                actorName,
                comment,
            },
            now,
            actor.Id);
    }

    // ---- Workspace ----

    private async Task<ProgramAdvisoryWorkspaceDto> BuildWorkspaceAsync(AdvisoryParticipant participant)
    {
        var program = participant.Program;
        var user = participant.User;
        var advisor = program.AdvisorExpertId.HasValue
            ? await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value)
            : null;
        var advisorHasLogin = advisor != null && await ProgramAdvisorResolver.HasActiveLoginAsync(_unitOfWork, advisor);
        var approval = await FindActiveApprovalAsync(program.Id);

        var messages = _unitOfWork.ProgramAdvisoryDiscussionMessages
            .GetQueryable()
            .Where(m => m.ProgramId == program.Id && !m.IsDeleted);
        var liveMessages = messages.Where(m => m.RemovedAt == null);
        var read = await _unitOfWork.ProgramAdvisoryStreamReads.FirstOrDefaultAsync(
            r => r.ProgramId == program.Id
                 && r.UserId == user.Id
                 && !r.IsDeleted);
        var lastRead = read?.LastReadSequence ?? 0;

        var (changesSinceApproval, unseenChanges) = await CountChangesAsync(program, user.Id);
        var isManager = participant.IsManager;
        var isAdvisor = participant.Role == AdvisoryParticipantRole.Advisor;
        var status = program.Status;
        var curriculumLocked = await CurriculumEditGuard.IsLockedAsync(_unitOfWork, program.Id);

        return new ProgramAdvisoryWorkspaceDto
        {
            ProgramId = program.Id,
            Status = status,
            CurriculumVersion = program.CurriculumVersion,
            CurriculumLocked = curriculumLocked,
            AdvisorExpertId = program.AdvisorExpertId,
            AdvisorName = advisor?.FullName,
            Participants = await AdvisoryParticipantAccess.ListAsync(_unitOfWork, program, advisor),
            Capabilities = new AdvisoryCapabilitiesDto
            {
                CanPost = true,
                CanPin = participant.IsExpertParticipant,
                CanResolvePin = participant.IsExpertParticipant,
                CanEditCurriculum = isManager && !curriculumLocked,
                CanRequestApproval = isManager && status == ProgramStatus.Draft && advisorHasLogin,
                CanApprove = isAdvisor && status == ProgramStatus.Draft,
                CanRevokeApproval = status == ProgramStatus.Approved && (isManager || isAdvisor),
                CanPublish = isManager
                             && status == ProgramStatus.Approved
                             && approval?.CurriculumVersion == program.CurriculumVersion,
            },
            Approval = approval == null
                ? null
                : new ProgramApprovalSummaryDto
                {
                    Id = approval.Id,
                    CurriculumVersion = approval.CurriculumVersion,
                    ApprovedAt = approval.ApprovedAt,
                    ApprovedByName = await AdvisorNameAsync(approval.ApprovedByExpertId),
                    Comment = approval.Comment,
                },
            OpenPinCount = liveMessages.Count(m => m.PinStatus == DiscussionPinStatus.Open),
            AddressedPinCount = liveMessages.Count(m => m.PinStatus == DiscussionPinStatus.Addressed),
            UnreadCount = liveMessages.Count(m => m.Sequence > lastRead && m.AuthorUserId != user.Id),
            FrameworkCheckPassed = await IsFrameworkCheckPassedAsync(program),
            ChangesSinceApprovalCount = changesSinceApproval,
            UnseenChangeCount = unseenChanges,
            LatestSequence = messages.Select(m => (long?)m.Sequence).Max() ?? 0,
        };
    }

    private async Task<(int SinceApproval, int Unseen)> CountChangesAsync(Program program, Guid userId)
    {
        var baseVersion = (await _unitOfWork.ProgramApprovals.GetAllAsync(
                a => a.ProgramId == program.Id && !a.IsDeleted))
            .OrderByDescending(a => a.ApprovedAt)
            .FirstOrDefault()?.CurriculumVersion ?? 0;
        if (baseVersion >= program.CurriculumVersion)
        {
            return (0, 0);
        }

        var rows = await _unitOfWork.CurriculumChanges.GetAllAsync(
            c => c.ProgramId == program.Id
                 && c.Version > baseVersion
                 && c.Version <= program.CurriculumVersion
                 && !c.IsDeleted);
        var items = CurriculumChangeConsolidator.Consolidate(rows);
        var seen = await _unitOfWork.CurriculumChangeSeens.FirstOrDefaultAsync(
            s => s.ProgramId == program.Id && s.UserId == userId && !s.IsDeleted);
        var seenVersion = seen?.SeenVersion ?? 0;
        return (items.Count, items.Count(i => i.LastVersion > seenVersion));
    }

    private async Task<bool> IsFrameworkCheckPassedAsync(Program program)
    {
        try
        {
            return (await ProgramFrameworkCheck.RunAsync(_unitOfWork, program)).AllPassed;
        }
        catch (ConflictException)
        {
            return false;
        }
    }

    private static AdvisoryApprovalState ResolveApprovalState(IEnumerable<ProgramApproval> approvals)
    {
        var list = approvals.ToList();
        if (list.Count == 0)
        {
            return AdvisoryApprovalState.None;
        }

        return list.Any(a => a.RevokedAt == null) ? AdvisoryApprovalState.Approved : AdvisoryApprovalState.Revoked;
    }

    // ---- Helpers ----

    private Task<AdvisoryParticipant> RequireParticipantAsync(Guid programId)
        => AdvisoryParticipantAccess.RequireAsync(_unitOfWork, _claimsService, programId);

    private async Task<User> RequireAdvisoryUserAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Authenticated user id is unavailable.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.Unauthorized("Authenticated user was not found.");
        }

        return user.Role is RoleType.Manager or RoleType.Admin or RoleType.Expert
            ? user
            : throw ErrorHelper.Forbidden("Only managers, admins, and experts can access advisory programs.");
    }

    private async Task<AdvisoryParticipant> RequireManagerAsync(Guid programId)
    {
        var participant = await RequireParticipantAsync(programId);
        return participant.IsManager
            ? participant
            : throw ErrorHelper.Forbidden("Only a manager or admin can perform this action.");
    }

    private static void EnsureStatus(Program program, ProgramStatus expected, string message)
    {
        if (program.Status != expected)
        {
            throw ErrorHelper.Conflict($"{message} Current status: {program.Status}.", InvalidStatusCode);
        }
    }

    private Task<ProgramApproval?> FindActiveApprovalAsync(Guid programId)
        => _unitOfWork.ProgramApprovals.FirstOrDefaultAsync(
            a => a.ProgramId == programId && a.RevokedAt == null && !a.IsDeleted);

    private async Task<string?> AdvisorNameAsync(Guid expertId)
        => (await _unitOfWork.Experts.GetByIdAsync(expertId))?.FullName;

    private async Task<Guid?> AdvisorUserIdAsync(Program program)
        => program.AdvisorExpertId.HasValue
            ? (await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value))?.UserId
            : null;

    private static string? NormalizeComment(string? comment)
    {
        var trimmed = comment?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxCommentLength
            ? trimmed
            : throw ErrorHelper.BadRequest($"Comment must be at most {MaxCommentLength} characters.");
    }

    private DateTime Now() => _currentTime.GetCurrentTime().ToUniversalTime();
}
