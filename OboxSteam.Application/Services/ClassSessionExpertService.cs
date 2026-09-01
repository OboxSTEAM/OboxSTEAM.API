using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionExpertDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassSessionExpertService : IClassSessionExpertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ClassSessionExpertService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public ClassSessionExpertService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassSessionExpertService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<ClassSessionExpertResponseDto> InviteAsync(InviteClassSessionExpertDto request)
    {
        var actor = await EnsureManagerOrAdminAsync();

        var session = await _unitOfWork.ClassSessions.GetByIdAsync(request.ClassSessionId);
        ClassSessionValidator.ValidateClassSessionExists(session, request.ClassSessionId);
        ClassSessionExpertValidator.ValidateOfflineScheduledSession(session!);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(session!.ClassId);
        ClassValidator.ValidateClassExists(classEntity, session.ClassId);

        var expert = await _unitOfWork.Experts.GetByIdAsync(request.ExpertId);
        if (expert == null || expert.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Expert with id '{request.ExpertId}' not found.");
        }

        ClassSessionExpertValidator.ValidateExpertCanLogin(expert);

        var board = await _unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == classEntity!.ProgramId
                 && b.ExpertId == expert.Id
                 && !b.IsDeleted);
        ClassSessionExpertValidator.ValidateExpertOnProgramBoard(board, expert);

        var activeOnSession = await _unitOfWork.ClassSessionExperts.FirstOrDefaultAsync(
            e => e.ClassSessionId == session.Id
                 && !e.IsDeleted
                 && (e.Status == ClassSessionExpertStatus.Invited
                     || e.Status == ClassSessionExpertStatus.Accepted));
        ClassSessionExpertValidator.ValidateNoActiveExpertOnSession(activeOnSession);

        var warning = await ScheduleConflictValidator.BuildExpertOverlapWarningAsync(
            _unitOfWork,
            expert.Id,
            session.StartTime,
            session.EndTime,
            excludeSessionId: session.Id);

        var entity = new ClassSessionExpert
        {
            ClassSessionId = session.Id,
            ExpertId = expert.Id,
            Status = ClassSessionExpertStatus.Invited,
        };

        await _unitOfWork.ClassSessionExperts.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionExpertInvited(
                expert.UserId!.Value,
                entity.Id,
                session.Id,
                classEntity!.Id,
                classEntity.ProgramId,
                actor.Id,
                classEntity.Name,
                programName: null,
                session.Title,
                AppDateTime.FormatVietnamDateTime(session.StartTime),
                actor.FullName));

        _logger.LogInformation(
            "[InviteAsync] Expert {ExpertId} invited to session {SessionId} as {InvitationId}.",
            expert.Id,
            session.Id,
            entity.Id);

        return await MapResponseAsync(entity, session, classEntity, expert, warning);
    }

    public async Task<ClassSessionExpertResponseDto> GetByIdAsync(Guid id)
    {
        var invitation = await LoadInvitationAsync(id);
        var (session, classEntity, expert) = await LoadGraphAsync(invitation);

        var currentUserId = _claimsService.GetCurrentUserId;
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId);
        if (currentUser == null || currentUser.IsDeleted)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        if (currentUser.Role is RoleType.Manager or RoleType.Admin)
        {
            return await MapResponseAsync(invitation, session, classEntity, expert);
        }

        if (currentUser.Role == RoleType.Expert && expert.UserId == currentUserId)
        {
            return await MapResponseAsync(invitation, session, classEntity, expert);
        }

        throw ErrorHelper.Forbidden("You cannot view this co-teach invitation.");
    }

    public async Task<Pagination<ClassSessionExpertResponseDto>> GetMineAsync(
        ClassSessionExpertStatus? status,
        int page,
        int pageSize)
    {
        ClassSessionExpertValidator.ValidatePagination(page, pageSize);
        var expert = await GetCurrentExpertAsync();

        var query = _unitOfWork.ClassSessionExperts
            .GetQueryable()
            .Where(e => !e.IsDeleted && e.ExpertId == expert.Id);

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        query = query.OrderByDescending(e => e.CreatedAt);

        var totalCount = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var dtos = new List<ClassSessionExpertResponseDto>(items.Count);
        foreach (var item in items)
        {
            var (session, classEntity, loadedExpert) = await LoadGraphAsync(item);
            dtos.Add(await MapResponseAsync(item, session, classEntity, loadedExpert));
        }

        return new Pagination<ClassSessionExpertResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<Pagination<ClassSessionExpertResponseDto>> GetForManagerAsync(
        Guid? classId,
        Guid? sessionId,
        Guid? expertId,
        ClassSessionExpertStatus? status,
        int page,
        int pageSize)
    {
        ClassSessionExpertValidator.ValidatePagination(page, pageSize);
        await EnsureManagerOrAdminAsync();

        var query = _unitOfWork.ClassSessionExperts
            .GetQueryable()
            .Where(e => !e.IsDeleted);

        if (sessionId.HasValue)
        {
            query = query.Where(e => e.ClassSessionId == sessionId.Value);
        }

        if (expertId.HasValue)
        {
            query = query.Where(e => e.ExpertId == expertId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (classId.HasValue)
        {
            var sessionIds = _unitOfWork.ClassSessions
                .GetQueryable()
                .Where(s => s.ClassId == classId.Value && !s.IsDeleted)
                .Select(s => s.Id);
            query = query.Where(e => sessionIds.Contains(e.ClassSessionId));
        }

        query = query.OrderByDescending(e => e.CreatedAt);

        var totalCount = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var dtos = new List<ClassSessionExpertResponseDto>(items.Count);
        foreach (var item in items)
        {
            var (session, classEntity, expert) = await LoadGraphAsync(item);
            dtos.Add(await MapResponseAsync(item, session, classEntity, expert));
        }

        return new Pagination<ClassSessionExpertResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ClassSessionExpertResponseDto> AcceptAsync(Guid id)
    {
        var expert = await GetCurrentExpertAsync();
        var invitation = await LoadInvitationAsync(id);
        ClassSessionExpertValidator.ValidateOwnership(invitation, expert.Id);
        ClassSessionExpertValidator.ValidateInvitedForDecision(invitation);

        var (session, classEntity, loadedExpert) = await LoadGraphAsync(invitation);
        ClassSessionExpertValidator.ValidateSessionStillScheduled(session);

        await ScheduleConflictValidator.ValidateExpertSessionNoOverlapAsync(
            _unitOfWork,
            expert.Id,
            session.StartTime,
            session.EndTime,
            excludeSessionId: session.Id);

        invitation.Status = ClassSessionExpertStatus.Accepted;
        await _unitOfWork.ClassSessionExperts.Update(invitation);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionExpertAccepted(
                invitation.Id,
                session.Id,
                classEntity.Id,
                classEntity.ProgramId,
                expert.UserId,
                classEntity.Name,
                programName: null,
                session.Title,
                loadedExpert.FullName));

        _logger.LogInformation("[AcceptAsync] Invitation {InvitationId} accepted.", id);

        return await MapResponseAsync(invitation, session, classEntity, loadedExpert);
    }

    public async Task<ClassSessionExpertResponseDto> DeclineAsync(Guid id)
    {
        var expert = await GetCurrentExpertAsync();
        var invitation = await LoadInvitationAsync(id);
        ClassSessionExpertValidator.ValidateOwnership(invitation, expert.Id);
        ClassSessionExpertValidator.ValidateInvitedForDecision(invitation);

        var (session, classEntity, loadedExpert) = await LoadGraphAsync(invitation);

        invitation.Status = ClassSessionExpertStatus.Declined;
        await _unitOfWork.ClassSessionExperts.Update(invitation);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionExpertDeclined(
                invitation.Id,
                session.Id,
                classEntity.Id,
                classEntity.ProgramId,
                expert.UserId,
                classEntity.Name,
                programName: null,
                session.Title,
                loadedExpert.FullName));

        _logger.LogInformation("[DeclineAsync] Invitation {InvitationId} declined.", id);

        return await MapResponseAsync(invitation, session, classEntity, loadedExpert);
    }

    public async Task WithdrawAsync(Guid id)
    {
        var actor = await EnsureManagerOrAdminAsync();
        var invitation = await LoadInvitationAsync(id);
        ClassSessionExpertValidator.ValidateInvitedForWithdraw(invitation);

        var (session, classEntity, expert) = await LoadGraphAsync(invitation);

        await _unitOfWork.ClassSessionExperts.SoftRemove(invitation);
        await _unitOfWork.SaveChangesAsync();

        if (expert.UserId.HasValue)
        {
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassSessionExpertInvitationWithdrawn(
                    expert.UserId.Value,
                    invitation.Id,
                    session.Id,
                    classEntity.Id,
                    classEntity.ProgramId,
                    actor.Id,
                    classEntity.Name,
                    programName: null,
                    session.Title,
                    actor.FullName));
        }

        _logger.LogInformation("[WithdrawAsync] Invitation {InvitationId} withdrawn.", id);
    }

    public async Task<ClassSessionExpertResponseDto> ApproveRescheduleAsync(Guid id)
    {
        var expert = await GetCurrentExpertAsync();
        var invitation = await LoadInvitationAsync(id);
        ClassSessionExpertValidator.ValidateOwnership(invitation, expert.Id);
        ClassSessionExpertValidator.ValidateAcceptedForRescheduleDecision(invitation);

        var (session, classEntity, loadedExpert) = await LoadGraphAsync(invitation);
        ClassSessionValidator.ValidateSessionModifiable(session);
        ClassSessionExpertValidator.ValidatePendingReschedule(session);

        var proposedStart = session.ProposedStartTime!.Value;
        var proposedEnd = session.ProposedEndTime!.Value;

        ClassSessionValidator.ValidateSessionWithinClassDateRange(classEntity, proposedStart, proposedEnd);

        if (classEntity.MentorId.HasValue && session.SessionKind != SessionKind.AssignmentWindow)
        {
            await MentorScopeValidator.ValidateMentorSessionNoOverlapAsync(
                _unitOfWork,
                classEntity.MentorId.Value,
                proposedStart,
                proposedEnd,
                excludeSessionId: session.Id);
        }

        await ScheduleConflictValidator.ValidateExpertSessionNoOverlapAsync(
            _unitOfWork,
            expert.Id,
            proposedStart,
            proposedEnd,
            excludeSessionId: session.Id);

        session.StartTime = proposedStart;
        session.EndTime = proposedEnd;
        session.ProposedStartTime = null;
        session.ProposedEndTime = null;

        await _unitOfWork.ClassSessions.Update(session);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionRescheduled(
                classEntity.Id,
                session.Id,
                classEntity.ProgramId,
                classEntity.Name));

        _logger.LogInformation(
            "[ApproveRescheduleAsync] Invitation {InvitationId} approved pending reschedule for session {SessionId}.",
            id,
            session.Id);

        return await MapResponseAsync(invitation, session, classEntity, loadedExpert);
    }

    public async Task<ClassSessionExpertResponseDto> DeclineRescheduleAsync(Guid id)
    {
        var expert = await GetCurrentExpertAsync();
        var invitation = await LoadInvitationAsync(id);
        ClassSessionExpertValidator.ValidateOwnership(invitation, expert.Id);
        ClassSessionExpertValidator.ValidateAcceptedForRescheduleDecision(invitation);

        var (session, classEntity, loadedExpert) = await LoadGraphAsync(invitation);
        ClassSessionExpertValidator.ValidatePendingReschedule(session);

        session.ProposedStartTime = null;
        session.ProposedEndTime = null;
        await _unitOfWork.ClassSessions.Update(session);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionExpertRescheduleDeclined(
                invitation.Id,
                session.Id,
                classEntity.Id,
                classEntity.ProgramId,
                expert.UserId,
                classEntity.Name,
                programName: null,
                session.Title,
                loadedExpert.FullName));

        _logger.LogInformation(
            "[DeclineRescheduleAsync] Invitation {InvitationId} declined pending reschedule.",
            id);

        return await MapResponseAsync(invitation, session, classEntity, loadedExpert);
    }

    private async Task<ClassSessionExpert> LoadInvitationAsync(Guid id)
    {
        var invitation = await _unitOfWork.ClassSessionExperts.GetByIdAsync(id);
        ClassSessionExpertValidator.ValidateInvitationExists(invitation, id);
        return invitation!;
    }

    private async Task<(ClassSession Session, Class ClassEntity, Expert Expert)> LoadGraphAsync(
        ClassSessionExpert invitation)
    {
        var session = await _unitOfWork.ClassSessions.GetByIdAsync(invitation.ClassSessionId);
        ClassSessionValidator.ValidateClassSessionExists(session, invitation.ClassSessionId);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(session!.ClassId);
        ClassValidator.ValidateClassExists(classEntity, session.ClassId);

        var expert = await _unitOfWork.Experts.GetByIdAsync(invitation.ExpertId);
        if (expert == null || expert.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Expert with id '{invitation.ExpertId}' not found.");
        }

        return (session, classEntity!, expert);
    }

    private static Task<ClassSessionExpertResponseDto> MapResponseAsync(
        ClassSessionExpert invitation,
        ClassSession session,
        Class classEntity,
        Expert expert,
        string? warning = null)
    {
        return Task.FromResult(new ClassSessionExpertResponseDto
        {
            Id = invitation.Id,
            ClassSessionId = session.Id,
            ClassId = classEntity.Id,
            ClassName = classEntity.Name,
            ProgramId = classEntity.ProgramId,
            ExpertId = expert.Id,
            ExpertUserId = expert.UserId,
            ExpertCode = expert.Code,
            ExpertName = expert.FullName,
            Status = invitation.Status,
            SessionTitle = session.Title,
            SessionKind = session.SessionKind,
            SessionStatus = session.Status,
            SessionStartTime = session.StartTime,
            SessionEndTime = session.EndTime,
            ProposedStartTime = session.ProposedStartTime,
            ProposedEndTime = session.ProposedEndTime,
            ScheduleConflictWarning = warning,
            CreatedAt = invitation.CreatedAt,
            UpdatedAt = invitation.UpdatedAt,
        });
    }

    private async Task<Expert> GetCurrentExpertAsync()
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

        if (user.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden("Only experts can perform this action.");
        }

        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(
            e => e.UserId == userId && !e.IsDeleted);
        if (expert == null)
        {
            throw ErrorHelper.NotFound("Expert profile not found for the current user.");
        }

        return expert;
    }

    private async Task<User> EnsureManagerOrAdminAsync()
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

        if (user.Role is not (RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("Only Manager or Admin can manage co-teach invitations.");
        }

        return user;
    }
}
