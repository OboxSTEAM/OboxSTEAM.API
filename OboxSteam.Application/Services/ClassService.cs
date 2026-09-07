using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.DTOs.MentorDTO;
using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassService : IClassService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ClassService> _logger;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IClassRedeliveryRequestService _classRedeliveryRequestService;

    public ClassService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassService> logger,
        INotificationPublisher notificationPublisher,
        IClassRedeliveryRequestService classRedeliveryRequestService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
        _classRedeliveryRequestService = classRedeliveryRequestService;
    }

    public async Task<Pagination<ClassResponseDto>> GetAllClassesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? programId = null,
        ClassStatus? status = null,
        Guid? mentorId = null)
    {
        _logger.LogInformation(
            "[GetAllClassesAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        ClassValidator.ValidatePagination(page, pageSize);

        var query = _unitOfWork.Classes
            .GetQueryable()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(lowerSearch) ||
                c.Code.ToLower().Contains(lowerSearch));
        }

        if (programId.HasValue)
        {
            query = query.Where(c => c.ProgramId == programId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (mentorId.HasValue)
        {
            query = query.Where(c => c.MentorId == mentorId.Value);
        }

        query = sortBy?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "code" => isDescending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "startdate" => isDescending ? query.OrderByDescending(c => c.StartDate) : query.OrderBy(c => c.StartDate),
            "enddate" => isDescending ? query.OrderByDescending(c => c.EndDate) : query.OrderBy(c => c.EndDate),
            "status" => isDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "maxcapacity" => isDescending ? query.OrderByDescending(c => c.MaxCapacity) : query.OrderBy(c => c.MaxCapacity),
            "createdat" => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
            _ => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = await MapToResponseDtosAsync(items);

        _logger.LogInformation("[GetAllClassesAsync] Retrieved {Count}/{Total} classes.", dtos.Count, totalCount);

        return new Pagination<ClassResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ClassResponseDto> GetClassByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetClassByIdAsync] Fetching class with Id: {Id}", id);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateClassExists(entity, id);

        _logger.LogInformation("[GetClassByIdAsync] Class with Id {Id} retrieved successfully.", id);

        var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);

        return await MapToResponseDtoAsync(entity!, seatsTaken);
    }

    public async Task<ClassResponseDto> GetClassWithStudentsAsync(Guid classId)
    {
        _logger.LogInformation("[GetClassWithStudentsAsync] Fetching class roster for Id: {ClassId}", classId);

        var entity = await _unitOfWork.Classes.GetByIdAsync(classId);
        ClassValidator.ValidateClassExists(entity, classId);

        await ClassRosterValidator.EnsureCanViewClassRosterAsync(_unitOfWork, _claimsService, entity!);

        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var studentIds = enrollments.Select(ce => ce.StudentId).Distinct().ToList();
        var students = studentIds.Any()
            ? await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id) && !u.IsDeleted)
            : new List<User>();

        var studentsById = students.ToDictionary(u => u.Id);

        var studentDtos = enrollments
            .Where(ce => studentsById.ContainsKey(ce.StudentId))
            .OrderBy(ce => ce.EnrolledAt)
            .ThenBy(ce => ce.CreatedAt)
            .Select(ce =>
            {
                var student = studentsById[ce.StudentId];
                return new ClassStudentResponseDto
                {
                    ClassEnrollmentId = ce.Id,
                    StudentId = student.Id,
                    StudentCode = student.Code,
                    StudentName = student.FullName,
                    Email = student.Email,
                    Phone = student.Phone,
                    AvatarUrl = student.AvatarUrl,
                    EnrollmentStatus = ce.Status,
                    EnrolledAt = ce.EnrolledAt,
                };
            })
            .ToList();

        _logger.LogInformation(
            "[GetClassWithStudentsAsync] Class {ClassId} roster retrieved — {StudentCount} active student(s).",
            classId,
            studentDtos.Count);

        var dto = await MapToResponseDtoAsync(entity!, seatsTaken: enrollments.Count);
        dto.Students = studentDtos;
        return dto;
    }

    public async Task<ClassWithSessionsResponseDto> GetClassWithSessionsAsync(Guid classId)
    {
        _logger.LogInformation("[GetClassWithSessionsAsync] Fetching class sessions for Id: {ClassId}", classId);

        var entity = await _unitOfWork.Classes.GetByIdAsync(classId);
        ClassValidator.ValidateClassExists(entity, classId);

        var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, classId);

        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId && !cs.IsDeleted);

        var sessionDtos = sessions
            .OrderBy(cs => cs.StartTime)
            .ThenBy(cs => cs.CreatedAt)
            .Select(cs => new ClassSessionResponseDto
            {
                Id = cs.Id,
                ClassId = cs.ClassId,
                ModuleId = cs.ModuleId,
                ActivityId = cs.ActivityId,
                AssignmentId = cs.AssignmentId,
                SessionKind = cs.SessionKind,
                Title = cs.Title,
                Description = cs.Description,
            StartTime = cs.StartTime,
            EndTime = cs.EndTime,
            Location = cs.Location,
            MeetingUrl = cs.MeetingUrl,
            Latitude = cs.Latitude,
            Longitude = cs.Longitude,
            RequiresAttendance = cs.RequiresAttendance,
            RequiresMentorCheckIn = cs.RequiresMentorCheckIn,
            Status = cs.Status,
            CreatedAt = cs.CreatedAt,
            UpdatedAt = cs.UpdatedAt,
            ProposedStartTime = cs.ProposedStartTime,
            ProposedEndTime = cs.ProposedEndTime,
            })
            .ToList();

        _logger.LogInformation(
            "[GetClassWithSessionsAsync] Class {ClassId} retrieved — {SessionCount} session(s).",
            classId,
            sessionDtos.Count);

        return new ClassWithSessionsResponseDto
        {
            Id = entity!.Id,
            Code = entity.Code,
            Name = entity.Name,
            ProgramId = entity.ProgramId,
            MentorId = entity.MentorId,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            MaxCapacity = entity.MaxCapacity,
            SeatsTaken = seatsTaken,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Sessions = sessionDtos,
        };
    }

    public async Task<IReadOnlyList<OpenEnrollmentClassDto>> GetOpenEnrollmentClassesAsync(
        Guid programId,
        Guid? preferredClassId = null)
    {
        _logger.LogInformation(
            "[GetOpenEnrollmentClassesAsync] programId={ProgramId}, preferredClassId={PreferredClassId}",
            programId,
            preferredClassId);

        ProgramEnrollmentValidator.ValidateProgramIdRequired(programId);

        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        ProgramEnrollmentValidator.ValidateProgramExists(program, programId);

        await ClassSeatHoldHelper.ReleaseExpiredHoldsAsync(_unitOfWork);

        var openClasses = await _unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == programId
                 && c.Status == ClassStatus.Open
                 && c.Kind == ClassKind.Standard
                 && !c.IsDeleted);

        if (openClasses.Count == 0)
        {
            return Array.Empty<OpenEnrollmentClassDto>();
        }

        var classIds = openClasses.Select(c => c.Id).ToList();
        var mentorIds = openClasses
            .Where(c => c.MentorId.HasValue)
            .Select(c => c.MentorId!.Value)
            .Distinct()
            .ToList();

        var mentors = mentorIds.Count == 0
            ? []
            : await _unitOfWork.Users.GetAllAsync(u => mentorIds.Contains(u.Id) && !u.IsDeleted);
        var mentorById = mentors.ToDictionary(u => u.Id);

        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => classIds.Contains(cs.ClassId)
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);
        var sessionsByClassId = sessions
            .GroupBy(cs => cs.ClassId)
            .ToDictionary(g => g.Key, g => g.OrderBy(cs => cs.StartTime).ToList());

        var result = new List<OpenEnrollmentClassDto>();

        foreach (var openClass in openClasses.OrderBy(c => c.StartDate).ThenBy(c => c.Code))
        {
            var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(
                _unitOfWork,
                openClass.Id);
            var seatsRemaining = openClass.MaxCapacity - seatsTaken;
            if (seatsRemaining <= 0)
            {
                continue;
            }

            sessionsByClassId.TryGetValue(openClass.Id, out var classSessions);
            classSessions ??= [];

            string? mentorName = null;
            if (openClass.MentorId.HasValue
                && mentorById.TryGetValue(openClass.MentorId.Value, out var mentor))
            {
                mentorName = mentor.FullName;
            }

            result.Add(new OpenEnrollmentClassDto
            {
                ClassId = openClass.Id,
                Code = openClass.Code,
                Name = openClass.Name,
                StartDate = openClass.StartDate,
                EndDate = openClass.EndDate,
                MentorId = openClass.MentorId,
                MentorName = mentorName,
                MaxCapacity = openClass.MaxCapacity,
                SeatsTaken = seatsTaken,
                SeatsRemaining = seatsRemaining,
                ScheduleSummary = openClass.ScheduleSummary,
                IsPreferred = preferredClassId.HasValue && openClass.Id == preferredClassId.Value,
                Sessions = classSessions
                    .Select(cs => new OpenEnrollmentClassSessionDto
                    {
                        SessionId = cs.Id,
                        Title = cs.Title,
                        StartTime = cs.StartTime,
                        EndTime = cs.EndTime,
                        SessionKind = cs.SessionKind,
                        Location = cs.Location,
                    })
                    .ToList(),
            });
        }

        if (preferredClassId.HasValue)
        {
            result = result
                .OrderByDescending(c => c.IsPreferred)
                .ThenBy(c => c.StartDate)
                .ThenBy(c => c.Code)
                .ToList();
        }

        _logger.LogInformation(
            "[GetOpenEnrollmentClassesAsync] Returning {Count} open class(es) for program {ProgramId}.",
            result.Count,
            programId);

        return result;
    }

    public async Task<ClassResponseDto> CreateClassAsync(CreateClassRequestDto request)
    {
        _logger.LogInformation("[CreateClassAsync] Start creating class: {Name} (Code: {Code})",
            request.Name, request.Code);

        ClassValidator.ValidateCreateRequest(request);

        var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId);
        ClassValidator.ValidateProgramExists(program, request.ProgramId);
        ClassValidator.EnsureProgramIsActive(program!);

        if (request.MentorId.HasValue)
        {
            var mentor = await _unitOfWork.Users.GetByIdAsync(request.MentorId.Value);
            ClassValidator.ValidateMentorExists(mentor, request.MentorId.Value);
            await ClassMentorRequestValidator.ValidateUnderConcurrentLimitAsync(_unitOfWork, mentor!);
        }

        var duplicate = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code.ToLower() == request.Code.Trim().ToLower() && !c.IsDeleted);

        if (duplicate != null)
        {
            _logger.LogWarning("[CreateClassAsync] Class with code '{Code}' already exists.", request.Code);
            throw ErrorHelper.Conflict($"Class with code '{request.Code}' already exists.");
        }

        var entity = new Class
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            ProgramId = request.ProgramId,
            MentorId = request.MentorId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxCapacity = request.MaxCapacity,
            Status = ClassStatus.Draft,
            MinHoursBeforeAssignmentJoin = request.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = request.ScheduleSummary?.Trim(),
        };

        await _unitOfWork.Classes.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await SyncRequiredSkillsAsync(entity.Id, request.RequiredSkillIds);
        if (request.RequiredSkillIds is { Count: > 0 })
        {
            await _unitOfWork.SaveChangesAsync();
        }

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassCreated(entity.Id, entity.ProgramId, entity.Name));

        _logger.LogInformation("[CreateClassAsync] Class '{Code}' created with Id {Id}.", entity.Code, entity.Id);

        return await MapToResponseDtoAsync(entity, seatsTaken: 0);
    }

    public async Task<ClassResponseDto> UpdateClassAsync(Guid id, UpdateClassRequestDto request)
    {
        _logger.LogInformation("[UpdateClassAsync] Attempting to update class with Id: {Id}", id);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateClassExists(entity, id);
        var classEntity = entity!;

        ClassValidator.ValidateNotUpdatingStatusViaPatch(request.Status);

        List<NotificationCommand>? pendingDirectAssignReconcile = null;

        if (!string.IsNullOrWhiteSpace(request.Code) &&
            !classEntity.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code.ToLower() == request.Code.Trim().ToLower() &&
                     !c.IsDeleted &&
                     c.Id != id);

            if (duplicate != null)
            {
                _logger.LogWarning("[UpdateClassAsync] Code '{Code}' is already in use.", request.Code);
                throw ErrorHelper.Conflict($"Class with code '{request.Code}' already exists.");
            }
        }

        var isUpdated = false;

        if (!string.IsNullOrWhiteSpace(request.Code) && classEntity.Code != request.Code.Trim())
        {
            classEntity.Code = request.Code.Trim();
            isUpdated = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && classEntity.Name != request.Name.Trim())
        {
            classEntity.Name = request.Name.Trim();
            isUpdated = true;
        }

        if (request.ProgramId.HasValue && classEntity.ProgramId != request.ProgramId.Value)
        {
            var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId.Value);
            ClassValidator.ValidateProgramExists(program, request.ProgramId.Value);
            ClassValidator.EnsureProgramIsActive(program!);
            classEntity.ProgramId = request.ProgramId.Value;
            isUpdated = true;
        }

        if (request.MentorId.HasValue && classEntity.MentorId != request.MentorId.Value)
        {
            var mentor = await _unitOfWork.Users.GetByIdAsync(request.MentorId.Value);
            ClassValidator.ValidateMentorExists(mentor, request.MentorId.Value);

            await ClassMentorRequestValidator.ValidateUnderConcurrentLimitAsync(
                _unitOfWork,
                mentor!,
                excludeClassId: classEntity.Id);

            await MentorScopeValidator.ValidateMentorCanTakeClassSessionsAsync(
                _unitOfWork,
                request.MentorId.Value,
                classEntity.Id);

            classEntity.MentorId = request.MentorId.Value;
            isUpdated = true;
            pendingDirectAssignReconcile = await ReconcileRequestsOnDirectAssignAsync(
                classEntity,
                mentor!,
                _claimsService.GetCurrentUserId);
        }

        var startDate = request.StartDate ?? classEntity.StartDate;
        var endDate = request.EndDate ?? classEntity.EndDate;

        if (request.StartDate.HasValue || request.EndDate.HasValue)
        {
            ClassValidator.ValidateDateRange(startDate, endDate);

            // Lead time applies while the class is still pre-start (enrollment window).
            if (request.StartDate.HasValue
                && classEntity.Status is ClassStatus.Draft
                    or ClassStatus.ReadyForMentor
                    or ClassStatus.Open)
            {
                ClassValidator.ValidateStartDateLeadTime(startDate, DateTime.UtcNow);
            }

            var activeSessions = await _unitOfWork.ClassSessions.GetAllAsync(
                s => s.ClassId == id
                     && !s.IsDeleted
                     && s.Status != ClassSessionStatus.Cancelled);
            ClassValidator.ValidateDateRangeCoversSessions(startDate, endDate, activeSessions);

            classEntity.StartDate = startDate;
            classEntity.EndDate = endDate;
            isUpdated = true;
        }

        if (request.MaxCapacity.HasValue && classEntity.MaxCapacity != request.MaxCapacity.Value)
        {
            ClassValidator.ValidateMaxCapacity(request.MaxCapacity.Value);

            var enrolledCount = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);

            ClassValidator.ValidateCapacityNotBelowEnrollment(request.MaxCapacity.Value, enrolledCount);
            classEntity.MaxCapacity = request.MaxCapacity.Value;
            isUpdated = true;
        }

        if (request.MinHoursBeforeAssignmentJoin.HasValue &&
            classEntity.MinHoursBeforeAssignmentJoin != request.MinHoursBeforeAssignmentJoin.Value)
        {
            ClassValidator.ValidateMinHoursBeforeAssignmentJoin(request.MinHoursBeforeAssignmentJoin.Value);
            classEntity.MinHoursBeforeAssignmentJoin = request.MinHoursBeforeAssignmentJoin.Value;
            isUpdated = true;
        }

        if (request.ScheduleSummary != null && classEntity.ScheduleSummary != request.ScheduleSummary.Trim())
        {
            classEntity.ScheduleSummary = string.IsNullOrWhiteSpace(request.ScheduleSummary)
                ? null
                : request.ScheduleSummary.Trim();
            isUpdated = true;
        }

        if (request.RequiredSkillIds != null)
        {
            await SyncRequiredSkillsAsync(classEntity.Id, request.RequiredSkillIds);
            isUpdated = true;
        }

        if (!isUpdated)
        {
            _logger.LogInformation("[UpdateClassAsync] No changes detected for class {Id}.", id);

            var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);
            return await MapToResponseDtoAsync(classEntity, seatsTaken);
        }

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        if (pendingDirectAssignReconcile is { Count: > 0 })
        {
            await _notificationPublisher.PublishManyAsync(pendingDirectAssignReconcile);
        }

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassUpdated(classEntity.Id, classEntity.ProgramId, classEntity.Name));

        _logger.LogInformation("[UpdateClassAsync] Class {Id} updated successfully.", id);

        var updatedSeatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);

        return await MapToResponseDtoAsync(classEntity, updatedSeatsTaken);
    }

    public async Task<ClassResponseDto> MarkReadyForMentorAsync(Guid id)
    {
        _logger.LogInformation(
            "[MarkReadyForMentorAsync] class {Id} -> {Status}",
            id,
            ClassStatus.ReadyForMentor);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateTransitionToStatus(entity, id, ClassStatus.ReadyForMentor);
        var classEntity = entity!;

        var activeSessions = ClassScheduleCoverage.CountActiveSessions(_unitOfWork, id);
        var schedulableItems = await ClassScheduleCoverage.CountSchedulableItemsAsync(
            _unitOfWork,
            classEntity.ProgramId);
        ClassValidator.ValidateReadyForMentorRequirements(activeSessions, schedulableItems);

        classEntity.Status = ClassStatus.ReadyForMentor;

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[MarkReadyForMentorAsync] class {Id} is now ReadyForMentor.", id);

        var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);

        return await MapToResponseDtoAsync(classEntity, seatsTaken);
    }

    public async Task<ClassResponseDto> OpenClassAsync(Guid id)
    {
        _logger.LogInformation("[OpenClassAsync] class {Id} -> {Status}", id, ClassStatus.Open);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateTransitionToStatus(entity, id, ClassStatus.Open);
        var classEntity = entity!;
        await RequireActiveProgramAsync(classEntity.ProgramId);

        var openActiveSessions = ClassScheduleCoverage.CountActiveSessions(_unitOfWork, id);
        var openSchedulableItems = await ClassScheduleCoverage.CountSchedulableItemsAsync(
            _unitOfWork,
            classEntity.ProgramId);
        ClassValidator.ValidateOpenRequirements(classEntity, openActiveSessions, openSchedulableItems);

        classEntity.Status = ClassStatus.Open;

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassOpenForEnrollment(classEntity.Id, classEntity.ProgramId, classEntity.Name));

        if (classEntity.Kind == ClassKind.Standard)
        {
            await _classRedeliveryRequestService.NotifyPendingManagerForNewClassAsync(classEntity.Id);
        }

        _logger.LogInformation("[OpenClassAsync] class {Id} is now Open.", id);

        var openSeatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);

        return await MapToResponseDtoAsync(classEntity, openSeatsTaken);
    }

    public async Task<ClassResponseDto> StartClassAsync(Guid id)
    {
        _logger.LogInformation("[StartClassAsync] class {Id} -> {Status}", id, ClassStatus.InProgress);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateTransitionToStatus(entity, id, ClassStatus.InProgress);
        var classEntity = entity!;
        await RequireActiveProgramAsync(classEntity.ProgramId);

        // Final gate: the curriculum may have changed while the class was open but still
        // empty — never let a class start on a schedule that no longer covers it.
        var startActiveSessions = ClassScheduleCoverage.CountActiveSessions(_unitOfWork, id);
        var startSchedulableItems = await ClassScheduleCoverage.CountSchedulableItemsAsync(
            _unitOfWork,
            classEntity.ProgramId);
        ClassValidator.ValidateOpenRequirements(classEntity, startActiveSessions, startSchedulableItems);

        classEntity.Status = ClassStatus.InProgress;

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassStarted(classEntity.Id, classEntity.ProgramId, classEntity.Name));

        _logger.LogInformation("[StartClassAsync] class {Id} is now InProgress.", id);

        var startSeatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);

        return await MapToResponseDtoAsync(classEntity, startSeatsTaken);
    }

    public async Task TryAutoStartClassIfReadyAsync(Guid classId)
    {
        var entity = await _unitOfWork.Classes.GetByIdAsync(classId);
        if (entity == null || entity.IsDeleted || entity.Status != ClassStatus.Open)
        {
            return;
        }

        var activeEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var now = DateTime.UtcNow;
        if (!ClassValidator.IsReadyForAutoStart(entity, activeEnrollments.Count, now))
        {
            return;
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(entity.ProgramId);
        if (program == null || program.IsDeleted || program.Status != ProgramStatus.Active)
        {
            _logger.LogWarning(
                "[TryAutoStartClassIfReadyAsync] class {Id} skipped — program {ProgramId} is not Active.",
                classId,
                entity.ProgramId);
            return;
        }

        // Same coverage gate as the manual Start: never auto-start on a stale schedule.
        var sessionCount = ClassScheduleCoverage.CountActiveSessions(_unitOfWork, classId);
        var schedulableCount = await ClassScheduleCoverage.CountSchedulableItemsAsync(
            _unitOfWork,
            entity.ProgramId);
        if (sessionCount == 0 || sessionCount != schedulableCount)
        {
            _logger.LogWarning(
                "[TryAutoStartClassIfReadyAsync] class {Id} skipped — schedule does not cover the current curriculum ({Sessions}/{Items} sessions).",
                classId,
                sessionCount,
                schedulableCount);
            return;
        }

        entity.Status = ClassStatus.InProgress;

        await _unitOfWork.Classes.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassAutoStarted(entity.Id, entity.ProgramId, entity.Name));

        _logger.LogInformation(
            "[TryAutoStartClassIfReadyAsync] class {Id} auto-started to InProgress (capacity {Count}/{Max}, start {StartDate}).",
            classId,
            activeEnrollments.Count,
            entity.MaxCapacity,
            entity.StartDate);
    }

    // Adaptive delays for OpenClassAutoStartService (replaces fixed 30-minute polling).
    private static readonly TimeSpan AfterRunDelay = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WaitingForCapacityDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromHours(12);
    private static readonly TimeSpan MaxSleepCap = TimeSpan.FromHours(12);

    public async Task<OpenClassAutoStartSchedule> ResolveOpenClassAutoStartScheduleAsync()
    {
        var now = DateTime.UtcNow;

        var openClasses = await _unitOfWork.Classes.GetAllAsync(
            c => c.Status == ClassStatus.Open && !c.IsDeleted);

        // State A — no Open classes: nothing to auto-start; sleep long to avoid idle DB load.
        if (openClasses.Count == 0)
        {
            return new OpenClassAutoStartSchedule
            {
                ShouldRunAutoStart = false,
                NextDelay = IdleDelay,
                Reason = "Idle",
            };
        }

        // Single grouped query for active enrollment counts (avoids N+1 per Open class).
        var openClassIds = openClasses.Select(c => c.Id).ToList();
        var enrollmentCounts = _unitOfWork.ClassEnrollments
            .GetQueryable()
            .Where(ce => openClassIds.Contains(ce.ClassId)
                         && ce.Status == ClassEnrollmentStatus.Active
                         && !ce.IsDeleted)
            .GroupBy(ce => ce.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToList();

        var countByClassId = enrollmentCounts.ToDictionary(x => x.ClassId, x => x.Count);

        var hasReadyToStart = false;
        DateTime? earliestFutureStartDate = null;
        var activeProgramIds = GetActiveProgramIds(openClasses);

        foreach (var classEntity in openClasses)
        {
            if (!activeProgramIds.Contains(classEntity.ProgramId))
            {
                continue;
            }

            var activeCount = countByClassId.GetValueOrDefault(classEntity.Id, 0);
            if (activeCount < classEntity.MaxCapacity)
            {
                // State C — not full; enroll events (TryAutoStartClassIfReadyAsync) handle the happy path.
                continue;
            }

            if (now >= classEntity.StartDate)
            {
                // State D — full and StartDate reached; run auto-start on this wake.
                hasReadyToStart = true;
                break;
            }

            // State B — full but waiting for StartDate; track the nearest start time.
            if (earliestFutureStartDate == null || classEntity.StartDate < earliestFutureStartDate)
            {
                earliestFutureStartDate = classEntity.StartDate;
            }
        }

        if (hasReadyToStart)
        {
            return new OpenClassAutoStartSchedule
            {
                ShouldRunAutoStart = true,
                NextDelay = AfterRunDelay,
                Reason = "ReadyToStart",
            };
        }

        if (earliestFutureStartDate.HasValue)
        {
            var remaining = earliestFutureStartDate.Value - now;
            if (remaining <= TimeSpan.Zero)
            {
                // Safety: full class past StartDate should start immediately, not sleep again.
                return new OpenClassAutoStartSchedule
                {
                    ShouldRunAutoStart = true,
                    NextDelay = AfterRunDelay,
                    Reason = "ReadyToStart",
                };
            }

            // Sleep until StartDate when within 12h; otherwise wake every 12h to re-check (cap).
            var delay = remaining > MaxSleepCap ? MaxSleepCap : remaining;
            return new OpenClassAutoStartSchedule
            {
                ShouldRunAutoStart = false,
                NextDelay = delay,
                Reason = "WaitingForStartDate",
            };
        }

        // State C only — Open classes exist but none are full; light periodic safety net.
        return new OpenClassAutoStartSchedule
        {
            ShouldRunAutoStart = false,
            NextDelay = WaitingForCapacityDelay,
            Reason = "WaitingForCapacity",
        };
    }

    public async Task<int> AutoStartEligibleOpenClassesAsync()
    {
        var now = DateTime.UtcNow;

        var openClasses = await _unitOfWork.Classes.GetAllAsync(
            c => c.Status == ClassStatus.Open
                 && !c.IsDeleted
                 && c.StartDate <= now);

        if (openClasses.Count == 0)
        {
            return 0;
        }

        var classIds = openClasses.Select(c => c.Id).ToList();
        var activeEnrollmentCounts = _unitOfWork.ClassEnrollments
            .GetQueryable()
            .Where(ce => classIds.Contains(ce.ClassId)
                         && ce.Status == ClassEnrollmentStatus.Active
                         && !ce.IsDeleted)
            .GroupBy(ce => ce.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.ClassId, x => x.Count);

        // The Start gate applies here too: a class whose curriculum changed after its
        // schedule was generated must not auto-start on a stale schedule — skip it and
        // let the manager regenerate instead.
        var sessionCounts = _unitOfWork.ClassSessions
            .GetQueryable()
            .Where(s => classIds.Contains(s.ClassId)
                        && !s.IsDeleted
                        && s.Status != ClassSessionStatus.Cancelled)
            .GroupBy(s => s.ClassId)
            .ToDictionary(g => g.Key, g => g.Count());

        var schedulableCountByProgram = new Dictionary<Guid, int>();
        var startedCount = 0;
        var startedClasses = new List<Class>();
        var activeProgramIds = GetActiveProgramIds(openClasses);

        foreach (var classEntity in openClasses)
        {
            activeEnrollmentCounts.TryGetValue(classEntity.Id, out var activeEnrollmentCount);

            if (!ClassValidator.IsReadyForAutoStart(classEntity, activeEnrollmentCount, now))
            {
                continue;
            }

            if (!activeProgramIds.Contains(classEntity.ProgramId))
            {
                _logger.LogWarning(
                    "[AutoStartEligibleOpenClassesAsync] class {Id} skipped — program {ProgramId} is not Active.",
                    classEntity.Id,
                    classEntity.ProgramId);
                continue;
            }

            if (!schedulableCountByProgram.TryGetValue(classEntity.ProgramId, out var schedulableCount))
            {
                schedulableCount = await ClassScheduleCoverage.CountSchedulableItemsAsync(
                    _unitOfWork,
                    classEntity.ProgramId);
                schedulableCountByProgram[classEntity.ProgramId] = schedulableCount;
            }

            var sessionCount = sessionCounts.GetValueOrDefault(classEntity.Id, 0);
            if (sessionCount == 0 || sessionCount != schedulableCount)
            {
                _logger.LogWarning(
                    "[AutoStartEligibleOpenClassesAsync] class {Id} skipped — schedule does not cover the current curriculum ({Sessions}/{Items} sessions).",
                    classEntity.Id,
                    sessionCount,
                    schedulableCount);
                continue;
            }

            classEntity.Status = ClassStatus.InProgress;
            await _unitOfWork.Classes.Update(classEntity);
            startedClasses.Add(classEntity);
            startedCount++;

            _logger.LogInformation(
                "[AutoStartEligibleOpenClassesAsync] class {Id} auto-started to InProgress (capacity {Count}/{Max}, start {StartDate}).",
                classEntity.Id,
                activeEnrollmentCount,
                classEntity.MaxCapacity,
                classEntity.StartDate);
        }

        if (startedCount > 0)
        {
            await _unitOfWork.SaveChangesAsync();

            var notifications = startedClasses
                .Select(c => NotificationCatalog.ClassAutoStarted(c.Id, c.ProgramId, c.Name))
                .ToList();
            await _notificationPublisher.PublishManyAsync(notifications);
        }

        return startedCount;
    }

    public async Task<ClassResponseDto> CompleteClassAsync(Guid id)
    {
        _logger.LogInformation("[CompleteClassAsync] class {Id} -> {Status}", id, ClassStatus.Completed);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateTransitionToStatus(entity, id, ClassStatus.Completed);
        var classEntity = entity!;

        classEntity.Status = ClassStatus.Completed;

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassCompleted(classEntity.Id, classEntity.ProgramId, classEntity.Name));

        _logger.LogInformation("[CompleteClassAsync] class {Id} is now Completed.", id);

        var completeSeatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);

        return await MapToResponseDtoAsync(classEntity, completeSeatsTaken);
    }

    public async Task DeleteClassAsync(Guid id)
    {
        _logger.LogInformation("[DeleteClassAsync] Attempting to soft-delete class Id: {Id}", id);

        await EnrollmentAccessValidator.GetCurrentManagerAsync(
            _unitOfWork,
            _claimsService,
            ClassValidator.DeleteForbiddenMessage);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateClassExists(entity, id);
        var classEntity = entity!;

        ClassValidator.ValidateDeletableStatus(classEntity);

        if (classEntity.Status == ClassStatus.Open)
        {
            var activeEnrollmentCount = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, id);
            ClassValidator.ValidateOpenClassHasNoActiveStudents(classEntity, activeEnrollmentCount);
        }

        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == id && !cs.IsDeleted);

        if (sessions.Count > 0)
        {
            await _unitOfWork.ClassSessions.SoftRemoveRange(sessions);
        }

        await _unitOfWork.Classes.SoftRemove(classEntity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[DeleteClassAsync] Class Id {Id} soft-deleted successfully ({SessionCount} sessions soft-deleted).",
            id,
            sessions.Count);
    }

    private async Task RequireActiveProgramAsync(Guid programId)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        ClassValidator.ValidateProgramExists(program, programId);
        ClassValidator.EnsureProgramIsActive(program!);
    }

    private HashSet<Guid> GetActiveProgramIds(IReadOnlyCollection<Class> classes)
    {
        var programIds = classes.Select(c => c.ProgramId).Distinct().ToList();
        if (programIds.Count == 0)
        {
            return [];
        }

        return _unitOfWork.Programs
            .GetQueryable()
            .Where(p => programIds.Contains(p.Id) && !p.IsDeleted && p.Status == ProgramStatus.Active)
            .Select(p => p.Id)
            .ToHashSet();
    }

    private async Task<ClassResponseDto> MapToResponseDtoAsync(Class entity, int? seatsTaken = null)
    {
        var skills = await LoadRequiredSkillsAsync(entity.Id);
        var pendingCount = await CountPendingRequestsAsync(entity.Id);
        var mentor = await LoadMentorSummaryAsync(entity.MentorId);

        return new ClassResponseDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            ProgramId = entity.ProgramId,
            MentorId = entity.MentorId,
            Mentor = mentor,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            MaxCapacity = entity.MaxCapacity,
            SeatsTaken = seatsTaken ?? 0,
            Kind = entity.Kind,
            RemedialModuleId = entity.RemedialModuleId,
            Status = entity.Status,
            MinHoursBeforeAssignmentJoin = entity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = entity.ScheduleSummary,
            RequiredSkills = skills,
            PendingMentorRequestCount = pendingCount,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private async Task<List<ClassResponseDto>> MapToResponseDtosAsync(List<Class> items)
    {
        if (items.Count == 0)
        {
            return new List<ClassResponseDto>();
        }

        var classIds = items.Select(c => c.Id).ToList();
        var classSkills = await _unitOfWork.ClassSkills.GetAllAsync(
            cs => classIds.Contains(cs.ClassId) && !cs.IsDeleted);
        var skillIds = classSkills.Select(cs => cs.SkillId).Distinct().ToList();
        var skills = skillIds.Count == 0
            ? new List<Skill>()
            : await _unitOfWork.Skills.GetAllAsync(s => skillIds.Contains(s.Id) && !s.IsDeleted);
        var skillsById = skills.ToDictionary(s => s.Id);

        var skillsByClass = classSkills
            .GroupBy(cs => cs.ClassId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Where(cs => skillsById.ContainsKey(cs.SkillId))
                    .Select(cs => MapSkillSummary(skillsById[cs.SkillId]))
                    .ToList());

        var pendingCounts = _unitOfWork.ClassMentorRequests
            .GetQueryable()
            .Where(r => classIds.Contains(r.ClassId)
                        && r.Status == ClassMentorRequestStatus.Pending
                        && !r.IsDeleted)
            .GroupBy(r => r.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.ClassId, x => x.Count);

        var mentorSummaries = await LoadMentorSummariesAsync(
            items.Where(c => c.MentorId.HasValue).Select(c => c.MentorId!.Value).Distinct().ToList());

        return items.Select(c => new ClassResponseDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            ProgramId = c.ProgramId,
            MentorId = c.MentorId,
            Mentor = c.MentorId.HasValue
                ? mentorSummaries.GetValueOrDefault(c.MentorId.Value)
                : null,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            MaxCapacity = c.MaxCapacity,
            Kind = c.Kind,
            RemedialModuleId = c.RemedialModuleId,
            Status = c.Status,
            MinHoursBeforeAssignmentJoin = c.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = c.ScheduleSummary,
            RequiredSkills = skillsByClass.GetValueOrDefault(c.Id, new List<SkillSummaryDto>()),
            PendingMentorRequestCount = pendingCounts.GetValueOrDefault(c.Id, 0),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        }).ToList();
    }

    private async Task<MentorSummaryDto?> LoadMentorSummaryAsync(Guid? mentorId)
    {
        if (!mentorId.HasValue)
            return null;

        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId.Value);
        if (mentor == null || mentor.IsDeleted)
            return null;

        var profile = await _unitOfWork.MentorProfiles.FirstOrDefaultAsync(
            mp => mp.MentorId == mentor.Id && !mp.IsDeleted);

        return MentorSummaryMapper.ToSummary(mentor, profile);
    }

    private async Task<Dictionary<Guid, MentorSummaryDto>> LoadMentorSummariesAsync(List<Guid> mentorIds)
    {
        if (mentorIds.Count == 0)
            return new Dictionary<Guid, MentorSummaryDto>();

        var mentors = await _unitOfWork.Users.GetAllAsync(u => mentorIds.Contains(u.Id) && !u.IsDeleted);
        var profiles = await _unitOfWork.MentorProfiles.GetAllAsync(
            mp => mentorIds.Contains(mp.MentorId) && !mp.IsDeleted);
        var profilesByMentorId = profiles.ToDictionary(mp => mp.MentorId);

        return mentors.ToDictionary(
            m => m.Id,
            m => MentorSummaryMapper.ToSummary(
                m,
                profilesByMentorId.GetValueOrDefault(m.Id)));
    }

    private async Task<List<SkillSummaryDto>> LoadRequiredSkillsAsync(Guid classId)
    {
        var classSkills = await _unitOfWork.ClassSkills.GetAllAsync(
            cs => cs.ClassId == classId && !cs.IsDeleted);

        if (classSkills.Count == 0)
        {
            return new List<SkillSummaryDto>();
        }

        var skillIds = classSkills.Select(cs => cs.SkillId).Distinct().ToList();
        var skills = await _unitOfWork.Skills.GetAllAsync(s => skillIds.Contains(s.Id) && !s.IsDeleted);
        var skillsById = skills.ToDictionary(s => s.Id);

        return classSkills
            .Where(cs => skillsById.ContainsKey(cs.SkillId))
            .Select(cs => MapSkillSummary(skillsById[cs.SkillId]))
            .ToList();
    }

    private Task<int> CountPendingRequestsAsync(Guid classId)
    {
        var count = _unitOfWork.ClassMentorRequests
            .GetQueryable()
            .Count(r => r.ClassId == classId
                        && r.Status == ClassMentorRequestStatus.Pending
                        && !r.IsDeleted);
        return Task.FromResult(count);
    }

    private static SkillSummaryDto MapSkillSummary(Skill skill)
        => new()
        {
            Id = skill.Id,
            Code = skill.Code,
            Name = skill.Name,
            Category = skill.Category,
            Subcategory = skill.Subcategory,
        };

    private async Task SyncRequiredSkillsAsync(Guid classId, List<Guid>? skillIds)
    {
        if (skillIds == null)
        {
            return;
        }

        var desired = skillIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (desired.Count > 0)
        {
            var existingSkills = await _unitOfWork.Skills.GetAllAsync(
                s => desired.Contains(s.Id) && !s.IsDeleted);
            if (existingSkills.Count != desired.Count)
            {
                var found = existingSkills.Select(s => s.Id).ToHashSet();
                var missing = desired.First(id => !found.Contains(id));
                throw ErrorHelper.NotFound($"Skill with id '{missing}' not found.");
            }
        }

        var current = await _unitOfWork.ClassSkills.GetAllAsync(
            cs => cs.ClassId == classId && !cs.IsDeleted);

        var toRemove = current.Where(cs => !desired.Contains(cs.SkillId)).ToList();
        if (toRemove.Count > 0)
        {
            await _unitOfWork.ClassSkills.SoftRemoveRange(toRemove);
        }

        var currentSkillIds = current.Select(cs => cs.SkillId).ToHashSet();
        var toAdd = desired
            .Where(id => !currentSkillIds.Contains(id))
            .Select(id => new ClassSkill
            {
                ClassId = classId,
                SkillId = id,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _unitOfWork.ClassSkills.AddRangeAsync(toAdd);
        }
    }

    private async Task<List<NotificationCommand>> ReconcileRequestsOnDirectAssignAsync(
        Class classEntity,
        User mentor,
        Guid deciderId)
    {
        var pending = await _unitOfWork.ClassMentorRequests.GetAllAsync(
            r => r.ClassId == classEntity.Id
                 && r.Status == ClassMentorRequestStatus.Pending
                 && !r.IsDeleted);

        if (pending.Count == 0)
        {
            return new List<NotificationCommand>();
        }

        var now = DateTime.UtcNow;
        var notifications = new List<NotificationCommand>();

        foreach (var request in pending)
        {
            if (request.MentorId == mentor.Id)
            {
                request.Status = ClassMentorRequestStatus.Approved;
                request.DecidedAt = now;
                request.DecidedBy = deciderId == Guid.Empty ? null : deciderId;
                request.DecisionNote = "Assigned directly by manager.";
                notifications.Add(NotificationCatalog.ClassMentorRequestApproved(
                    request.Id,
                    classEntity.Id,
                    classEntity.ProgramId,
                    request.MentorId,
                    classEntity.Name));
            }
            else
            {
                request.Status = ClassMentorRequestStatus.Rejected;
                request.DecidedAt = now;
                request.DecidedBy = deciderId == Guid.Empty ? null : deciderId;
                request.DecisionNote = "Assigned directly by manager.";
                notifications.Add(NotificationCatalog.ClassMentorRequestRejected(
                    request.Id,
                    classEntity.Id,
                    classEntity.ProgramId,
                    request.MentorId,
                    classEntity.Name));
            }

            await _unitOfWork.ClassMentorRequests.Update(request);
        }

        return notifications;
    }
}
