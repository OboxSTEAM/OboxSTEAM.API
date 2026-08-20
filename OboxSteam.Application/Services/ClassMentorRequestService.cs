using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassMentorRequestDTO;
using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassMentorRequestService : IClassMentorRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ClassMentorRequestService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public ClassMentorRequestService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassMentorRequestService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<Pagination<ClassMentorBoardItemDto>> GetMentorBoardAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? programId = null,
        bool matchMySkills = false)
    {
        ClassMentorRequestValidator.ValidatePagination(page, pageSize);

        var mentorId = await GetCurrentMentorIdAsync();

        var mentorSkillIds = (await _unitOfWork.MentorSkills.GetAllAsync(
                ms => ms.MentorId == mentorId && !ms.IsDeleted))
            .Select(ms => ms.SkillId)
            .ToHashSet();

        // Board lists unassigned ReadyForMentor classes that already have a timetable.
        var scheduledClassIds = _unitOfWork.ClassSessions
            .GetQueryable()
            .Where(s => !s.IsDeleted && s.Status != ClassSessionStatus.Cancelled)
            .Select(s => s.ClassId)
            .Distinct();

        var query = _unitOfWork.Classes
            .GetQueryable()
            .Where(c => !c.IsDeleted
                        && c.MentorId == null
                        && c.Status == ClassStatus.ReadyForMentor
                        && scheduledClassIds.Contains(c.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(lower) ||
                c.Code.ToLower().Contains(lower));
        }

        if (programId.HasValue)
        {
            query = query.Where(c => c.ProgramId == programId.Value);
        }

        // Optional filter: class has at least one RequiredSkill that the mentor also has.
        // Default (false) keeps the full board so mentors are not forced to skill-match.
        if (matchMySkills)
        {
            if (mentorSkillIds.Count == 0)
            {
                return new Pagination<ClassMentorBoardItemDto>(
                    new List<ClassMentorBoardItemDto>(), 0, page, pageSize);
            }

            var matchingClassIds = _unitOfWork.ClassSkills
                .GetQueryable()
                .Where(cs => !cs.IsDeleted && mentorSkillIds.Contains(cs.SkillId))
                .Select(cs => cs.ClassId)
                .Distinct();

            query = query.Where(c => matchingClassIds.Contains(c.Id));
        }

        query = sortBy?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "code" => isDescending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "startdate" => isDescending ? query.OrderByDescending(c => c.StartDate) : query.OrderBy(c => c.StartDate),
            _ => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
        };

        var totalCount = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var classIds = items.Select(c => c.Id).ToList();

        var classSkills = classIds.Count == 0
            ? new List<ClassSkill>()
            : await _unitOfWork.ClassSkills.GetAllAsync(cs => classIds.Contains(cs.ClassId) && !cs.IsDeleted);

        var skillIds = classSkills.Select(cs => cs.SkillId).Distinct().ToList();
        var skills = skillIds.Count == 0
            ? new List<Skill>()
            : await _unitOfWork.Skills.GetAllAsync(s => skillIds.Contains(s.Id) && !s.IsDeleted);
        var skillsById = skills.ToDictionary(s => s.Id);

        var myPending = classIds.Count == 0
            ? new HashSet<Guid>()
            : (await _unitOfWork.ClassMentorRequests.GetAllAsync(
                r => r.MentorId == mentorId
                     && classIds.Contains(r.ClassId)
                     && r.Status == ClassMentorRequestStatus.Pending
                     && !r.IsDeleted))
            .Select(r => r.ClassId)
            .ToHashSet();

        var pendingCounts = classIds.Count == 0
            ? new Dictionary<Guid, int>()
            : _unitOfWork.ClassMentorRequests
                .GetQueryable()
                .Where(r => classIds.Contains(r.ClassId)
                            && r.Status == ClassMentorRequestStatus.Pending
                            && !r.IsDeleted)
                .GroupBy(r => r.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.ClassId, x => x.Count);

        var skillsByClass = classSkills
            .GroupBy(cs => cs.ClassId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Where(cs => skillsById.ContainsKey(cs.SkillId))
                    .Select(cs => MapSkillSummary(skillsById[cs.SkillId]))
                    .ToList());

        var matchingClassIdSet = classSkills
            .Where(cs => mentorSkillIds.Contains(cs.SkillId))
            .Select(cs => cs.ClassId)
            .ToHashSet();

        var dtos = items.Select(c => new ClassMentorBoardItemDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            ProgramId = c.ProgramId,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            MaxCapacity = c.MaxCapacity,
            Status = c.Status,
            ScheduleSummary = c.ScheduleSummary,
            RequiredSkills = skillsByClass.GetValueOrDefault(c.Id, new List<SkillSummaryDto>()),
            MatchesMySkills = matchingClassIdSet.Contains(c.Id),
            HasPendingRequestFromMe = myPending.Contains(c.Id),
            PendingRequestCount = pendingCounts.GetValueOrDefault(c.Id, 0),
        }).ToList();

        return new Pagination<ClassMentorBoardItemDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ClassMentorRequestResponseDto> CreateRequestAsync(CreateClassMentorRequestDto request)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId);
        ClassMentorRequestValidator.ValidateMentorEligible(mentor, mentorId);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        ClassValidator.ValidateClassExists(classEntity, request.ClassId);
        ClassMentorRequestValidator.ValidateClassOpenForRequests(classEntity!);

        var hasActiveSessions = _unitOfWork.ClassSessions
            .GetQueryable()
            .Any(s => s.ClassId == request.ClassId
                      && !s.IsDeleted
                      && s.Status != ClassSessionStatus.Cancelled);
        ClassMentorRequestValidator.ValidateClassHasSchedule(classEntity!, hasActiveSessions);

        var existingPending = await _unitOfWork.ClassMentorRequests.FirstOrDefaultAsync(
            r => r.ClassId == request.ClassId
                 && r.MentorId == mentorId
                 && r.Status == ClassMentorRequestStatus.Pending
                 && !r.IsDeleted);
        ClassMentorRequestValidator.ValidateNoDuplicatePending(existingPending);

        await ClassMentorRequestValidator.ValidateUnderConcurrentLimitAsync(_unitOfWork, mentor!);

        // Fail fast at request time: when the class already has sessions, a mentor whose
        // calendar conflicts with them can never be approved — reject the request now
        // instead of letting it sit pending until the manager's approve attempt fails.
        await MentorScopeValidator.ValidateMentorCanTakeClassSessionsAsync(
            _unitOfWork,
            mentorId,
            classEntity!.Id);

        var entity = new ClassMentorRequest
        {
            ClassId = request.ClassId,
            MentorId = mentorId,
            Status = ClassMentorRequestStatus.Pending,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
        };

        await _unitOfWork.ClassMentorRequests.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassMentorRequestSubmitted(
                entity.Id,
                entity.ClassId,
                classEntity!.ProgramId,
                mentorId,
                classEntity.Name));

        _logger.LogInformation(
            "[CreateRequestAsync] Mentor {MentorId} requested class {ClassId} (request {RequestId}).",
            mentorId,
            request.ClassId,
            entity.Id);

        return await MapResponseAsync(entity, classEntity, mentor);
    }

    public async Task WithdrawRequestAsync(Guid requestId)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var entity = await _unitOfWork.ClassMentorRequests.GetByIdAsync(requestId);
        ClassMentorRequestValidator.ValidateRequestExists(entity, requestId);
        ClassMentorRequestValidator.ValidateOwnership(entity!, mentorId);
        ClassMentorRequestValidator.ValidatePendingForWithdraw(entity!);

        entity!.Status = ClassMentorRequestStatus.Withdrawn;
        await _unitOfWork.ClassMentorRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[WithdrawRequestAsync] Mentor {MentorId} withdrew request {RequestId}.",
            mentorId,
            requestId);
    }

    public async Task<Pagination<ClassMentorRequestResponseDto>> GetMyRequestsAsync(
        ClassMentorRequestStatus? status,
        int page,
        int pageSize)
    {
        ClassMentorRequestValidator.ValidatePagination(page, pageSize);
        var mentorId = await GetCurrentMentorIdAsync();

        var query = _unitOfWork.ClassMentorRequests
            .GetQueryable()
            .Where(r => !r.IsDeleted && r.MentorId == mentorId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        query = query.OrderByDescending(r => r.CreatedAt);

        var totalCount = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var dtos = await MapResponsesAsync(items);

        return new Pagination<ClassMentorRequestResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<Pagination<ClassMentorRequestResponseDto>> GetRequestsForManagerAsync(
        Guid? classId,
        Guid? mentorId,
        ClassMentorRequestStatus? status,
        int page,
        int pageSize)
    {
        ClassMentorRequestValidator.ValidatePagination(page, pageSize);
        await EnsureManagerOrAdminAsync();

        var query = _unitOfWork.ClassMentorRequests
            .GetQueryable()
            .Where(r => !r.IsDeleted);

        if (classId.HasValue)
        {
            query = query.Where(r => r.ClassId == classId.Value);
        }

        if (mentorId.HasValue)
        {
            query = query.Where(r => r.MentorId == mentorId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        query = query.OrderByDescending(r => r.CreatedAt);

        var totalCount = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var dtos = await MapResponsesAsync(items);

        return new Pagination<ClassMentorRequestResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ClassMentorRequestResponseDto> ApproveRequestAsync(
        Guid requestId,
        DecideClassMentorRequestDto? request)
    {
        var decider = await EnsureManagerOrAdminAsync();
        var entity = await _unitOfWork.ClassMentorRequests.GetByIdAsync(requestId);
        ClassMentorRequestValidator.ValidateRequestExists(entity, requestId);
        ClassMentorRequestValidator.ValidatePendingForDecision(entity!);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(entity!.ClassId);
        ClassValidator.ValidateClassExists(classEntity, entity.ClassId);
        ClassMentorRequestValidator.ValidateClassOpenForRequests(classEntity!);

        // The schedule may have been deleted after the request was made — never approve
        // a mentor into a class that no longer has a timetable.
        var hasActiveSessions = _unitOfWork.ClassSessions
            .GetQueryable()
            .Any(s => s.ClassId == classEntity!.Id
                      && !s.IsDeleted
                      && s.Status != ClassSessionStatus.Cancelled);
        ClassMentorRequestValidator.ValidateClassHasSchedule(classEntity!, hasActiveSessions);

        var mentor = await _unitOfWork.Users.GetByIdAsync(entity.MentorId);
        ClassMentorRequestValidator.ValidateMentorEligible(mentor, entity.MentorId);

        await ClassMentorRequestValidator.ValidateUnderConcurrentLimitAsync(
            _unitOfWork,
            mentor!,
            excludeRequestId: entity.Id);

        await MentorScopeValidator.ValidateMentorCanTakeClassSessionsAsync(
            _unitOfWork,
            entity.MentorId,
            classEntity!.Id);

        var now = DateTime.UtcNow;
        var decisionNote = string.IsNullOrWhiteSpace(request?.DecisionNote)
            ? null
            : request!.DecisionNote.Trim();

        classEntity.MentorId = entity.MentorId;
        entity.Status = ClassMentorRequestStatus.Approved;
        entity.DecidedAt = now;
        entity.DecidedBy = decider.Id;
        entity.DecisionNote = decisionNote;

        var siblings = await _unitOfWork.ClassMentorRequests.GetAllAsync(
            r => r.ClassId == classEntity.Id
                 && r.Id != entity.Id
                 && r.Status == ClassMentorRequestStatus.Pending
                 && !r.IsDeleted);

        foreach (var sibling in siblings)
        {
            sibling.Status = ClassMentorRequestStatus.Rejected;
            sibling.DecidedAt = now;
            sibling.DecidedBy = decider.Id;
            sibling.DecisionNote = "Another mentor was approved for this class.";
            await _unitOfWork.ClassMentorRequests.Update(sibling);
        }

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.ClassMentorRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var notifications = new List<NotificationCommand>
        {
            NotificationCatalog.ClassMentorRequestApproved(
                entity.Id,
                entity.ClassId,
                classEntity.ProgramId,
                entity.MentorId,
                classEntity.Name),
        };

        notifications.AddRange(siblings.Select(s =>
            NotificationCatalog.ClassMentorRequestRejected(
                s.Id,
                s.ClassId,
                classEntity.ProgramId,
                s.MentorId,
                classEntity.Name)));

        await _notificationPublisher.PublishManyAsync(notifications);

        _logger.LogInformation(
            "[ApproveRequestAsync] Request {RequestId} approved; mentor {MentorId} assigned to class {ClassId}.",
            requestId,
            entity.MentorId,
            classEntity.Id);

        return await MapResponseAsync(entity, classEntity, mentor);
    }

    public async Task<ClassMentorRequestResponseDto> RejectRequestAsync(
        Guid requestId,
        DecideClassMentorRequestDto? request)
    {
        var decider = await EnsureManagerOrAdminAsync();
        var entity = await _unitOfWork.ClassMentorRequests.GetByIdAsync(requestId);
        ClassMentorRequestValidator.ValidateRequestExists(entity, requestId);
        ClassMentorRequestValidator.ValidatePendingForDecision(entity!);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(entity!.ClassId);
        ClassValidator.ValidateClassExists(classEntity, entity.ClassId);

        var mentor = await _unitOfWork.Users.GetByIdAsync(entity.MentorId);

        entity.Status = ClassMentorRequestStatus.Rejected;
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedBy = decider.Id;
        entity.DecisionNote = string.IsNullOrWhiteSpace(request?.DecisionNote)
            ? null
            : request!.DecisionNote.Trim();

        await _unitOfWork.ClassMentorRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassMentorRequestRejected(
                entity.Id,
                entity.ClassId,
                classEntity!.ProgramId,
                entity.MentorId,
                classEntity.Name));

        _logger.LogInformation("[RejectRequestAsync] Request {RequestId} rejected.", requestId);

        return await MapResponseAsync(entity, classEntity, mentor);
    }

    private async Task<Guid> GetCurrentMentorIdAsync()
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

        if (user.Role != RoleType.Mentor)
        {
            throw ErrorHelper.Forbidden("Only mentors can perform this action.");
        }

        return userId;
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
            throw ErrorHelper.Forbidden("Only Manager or Admin can manage mentor requests.");
        }

        return user;
    }

    private async Task<List<ClassMentorRequestResponseDto>> MapResponsesAsync(List<ClassMentorRequest> items)
    {
        if (items.Count == 0)
        {
            return new List<ClassMentorRequestResponseDto>();
        }

        var classIds = items.Select(r => r.ClassId).Distinct().ToList();
        var mentorIds = items.Select(r => r.MentorId).Distinct().ToList();

        var classes = await _unitOfWork.Classes.GetAllAsync(c => classIds.Contains(c.Id));
        var mentors = await _unitOfWork.Users.GetAllAsync(u => mentorIds.Contains(u.Id));

        var classesById = classes.ToDictionary(c => c.Id);
        var mentorsById = mentors.ToDictionary(u => u.Id);

        return items.Select(r =>
        {
            classesById.TryGetValue(r.ClassId, out var clazz);
            mentorsById.TryGetValue(r.MentorId, out var mentor);
            return MapResponse(r, clazz, mentor);
        }).ToList();
    }

    private async Task<ClassMentorRequestResponseDto> MapResponseAsync(
        ClassMentorRequest entity,
        Class? classEntity,
        User? mentor)
    {
        classEntity ??= await _unitOfWork.Classes.GetByIdAsync(entity.ClassId);
        mentor ??= await _unitOfWork.Users.GetByIdAsync(entity.MentorId);
        return MapResponse(entity, classEntity, mentor);
    }

    private static ClassMentorRequestResponseDto MapResponse(
        ClassMentorRequest entity,
        Class? classEntity,
        User? mentor)
        => new()
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            ClassCode = classEntity?.Code ?? string.Empty,
            ClassName = classEntity?.Name ?? string.Empty,
            ProgramId = classEntity?.ProgramId ?? Guid.Empty,
            MentorId = entity.MentorId,
            MentorCode = mentor?.Code,
            MentorName = mentor?.FullName,
            Status = entity.Status,
            Message = entity.Message,
            DecidedAt = entity.DecidedAt,
            DecidedBy = entity.DecidedBy,
            DecisionNote = entity.DecisionNote,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };

    private static SkillSummaryDto MapSkillSummary(Skill skill)
        => new()
        {
            Id = skill.Id,
            Code = skill.Code,
            Name = skill.Name,
            Category = skill.Category,
            Subcategory = skill.Subcategory,
        };
}
