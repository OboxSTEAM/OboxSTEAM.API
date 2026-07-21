using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassSessionDTO;
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

    public ClassService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
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

        var dtos = items.Select(c => new ClassResponseDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            ProgramId = c.ProgramId,
            MentorId = c.MentorId,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            MaxCapacity = c.MaxCapacity,
            Status = c.Status,
            MinHoursBeforeAssignmentJoin = c.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = c.ScheduleSummary,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        }).ToList();

        _logger.LogInformation("[GetAllClassesAsync] Retrieved {Count}/{Total} classes.", dtos.Count, totalCount);

        return new Pagination<ClassResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ClassResponseDto> GetClassByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetClassByIdAsync] Fetching class with Id: {Id}", id);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateClassExists(entity, id);

        _logger.LogInformation("[GetClassByIdAsync] Class with Id {Id} retrieved successfully.", id);

        var seatsTaken = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);

        return new ClassResponseDto
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
            MinHoursBeforeAssignmentJoin = entity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = entity.ScheduleSummary,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
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

        return new ClassResponseDto
        {
            Id = entity!.Id,
            Code = entity.Code,
            Name = entity.Name,
            ProgramId = entity.ProgramId,
            MentorId = entity.MentorId,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            MaxCapacity = entity.MaxCapacity,
            SeatsTaken = enrollments.Count,
            Status = entity.Status,
            MinHoursBeforeAssignmentJoin = entity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = entity.ScheduleSummary,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Students = studentDtos,
        };
    }

    public async Task<ClassWithSessionsResponseDto> GetClassWithSessionsAsync(Guid classId)
    {
        _logger.LogInformation("[GetClassWithSessionsAsync] Fetching class sessions for Id: {ClassId}", classId);

        var entity = await _unitOfWork.Classes.GetByIdAsync(classId);
        ClassValidator.ValidateClassExists(entity, classId);

        var seatsTaken = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, classId);

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
                MaxCapacity = cs.MaxCapacity,
                RequiresAttendance = cs.RequiresAttendance,
                Status = cs.Status,
                CreatedAt = cs.CreatedAt,
                UpdatedAt = cs.UpdatedAt,
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

    public async Task<ClassResponseDto> CreateClassAsync(CreateClassRequestDto request)
    {
        _logger.LogInformation("[CreateClassAsync] Start creating class: {Name} (Code: {Code})",
            request.Name, request.Code);

        ClassValidator.ValidateCreateRequest(request);

        var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId);
        ClassValidator.ValidateProgramExists(program, request.ProgramId);

        if (request.MentorId.HasValue)
        {
            var mentor = await _unitOfWork.Users.GetByIdAsync(request.MentorId.Value);
            ClassValidator.ValidateMentorExists(mentor, request.MentorId.Value);
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

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassCreated(entity.Id, entity.ProgramId, entity.Name));

        _logger.LogInformation("[CreateClassAsync] Class '{Code}' created with Id {Id}.", entity.Code, entity.Id);

        return new ClassResponseDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            ProgramId = entity.ProgramId,
            MentorId = entity.MentorId,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            MaxCapacity = entity.MaxCapacity,
            SeatsTaken = 0,
            Status = entity.Status,
            MinHoursBeforeAssignmentJoin = entity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = entity.ScheduleSummary,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public async Task<ClassResponseDto> UpdateClassAsync(Guid id, UpdateClassRequestDto request)
    {
        _logger.LogInformation("[UpdateClassAsync] Attempting to update class with Id: {Id}", id);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateClassExists(entity, id);
        var classEntity = entity!;

        ClassValidator.ValidateNotUpdatingStatusViaPatch(request.Status);

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
            classEntity.ProgramId = request.ProgramId.Value;
            isUpdated = true;
        }

        if (request.MentorId.HasValue && classEntity.MentorId != request.MentorId.Value)
        {
            var mentor = await _unitOfWork.Users.GetByIdAsync(request.MentorId.Value);
            ClassValidator.ValidateMentorExists(mentor, request.MentorId.Value);

            await MentorScopeValidator.ValidateMentorCanTakeClassSessionsAsync(
                _unitOfWork,
                request.MentorId.Value,
                classEntity.Id);

            classEntity.MentorId = request.MentorId.Value;
            isUpdated = true;
        }

        var startDate = request.StartDate ?? classEntity.StartDate;
        var endDate = request.EndDate ?? classEntity.EndDate;

        if (request.StartDate.HasValue || request.EndDate.HasValue)
        {
            ClassValidator.ValidateDateRange(startDate, endDate);
            classEntity.StartDate = startDate;
            classEntity.EndDate = endDate;
            isUpdated = true;
        }

        if (request.MaxCapacity.HasValue && classEntity.MaxCapacity != request.MaxCapacity.Value)
        {
            ClassValidator.ValidateMaxCapacity(request.MaxCapacity.Value);

            var enrolledCount = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);

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

        if (!isUpdated)
        {
            _logger.LogInformation("[UpdateClassAsync] No changes detected for class {Id}.", id);

            var seatsTaken = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);

            return new ClassResponseDto
            {
                Id = classEntity.Id,
                Code = classEntity.Code,
                Name = classEntity.Name,
                ProgramId = classEntity.ProgramId,
                MentorId = classEntity.MentorId,
                StartDate = classEntity.StartDate,
                EndDate = classEntity.EndDate,
                MaxCapacity = classEntity.MaxCapacity,
                SeatsTaken = seatsTaken,
                Status = classEntity.Status,
                MinHoursBeforeAssignmentJoin = classEntity.MinHoursBeforeAssignmentJoin,
                ScheduleSummary = classEntity.ScheduleSummary,
                CreatedAt = classEntity.CreatedAt,
                UpdatedAt = classEntity.UpdatedAt,
            };
        }

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassUpdated(classEntity.Id, classEntity.ProgramId, classEntity.Name));

        _logger.LogInformation("[UpdateClassAsync] Class {Id} updated successfully.", id);

        var updatedSeatsTaken = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);

        return new ClassResponseDto
        {
            Id = classEntity.Id,
            Code = classEntity.Code,
            Name = classEntity.Name,
            ProgramId = classEntity.ProgramId,
            MentorId = classEntity.MentorId,
            StartDate = classEntity.StartDate,
            EndDate = classEntity.EndDate,
            MaxCapacity = classEntity.MaxCapacity,
            SeatsTaken = updatedSeatsTaken,
            Status = classEntity.Status,
            MinHoursBeforeAssignmentJoin = classEntity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = classEntity.ScheduleSummary,
            CreatedAt = classEntity.CreatedAt,
            UpdatedAt = classEntity.UpdatedAt,
        };
    }

    public async Task<ClassResponseDto> OpenClassAsync(Guid id)
    {
        _logger.LogInformation("[OpenClassAsync] class {Id} -> {Status}", id, ClassStatus.Open);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateTransitionToStatus(entity, id, ClassStatus.Open);
        var classEntity = entity!;

        classEntity.Status = ClassStatus.Open;

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassOpenForEnrollment(classEntity.Id, classEntity.ProgramId, classEntity.Name));

        _logger.LogInformation("[OpenClassAsync] class {Id} is now Open.", id);

        var openSeatsTaken = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);

        return new ClassResponseDto
        {
            Id = classEntity.Id,
            Code = classEntity.Code,
            Name = classEntity.Name,
            ProgramId = classEntity.ProgramId,
            MentorId = classEntity.MentorId,
            StartDate = classEntity.StartDate,
            EndDate = classEntity.EndDate,
            MaxCapacity = classEntity.MaxCapacity,
            SeatsTaken = openSeatsTaken,
            Status = classEntity.Status,
            MinHoursBeforeAssignmentJoin = classEntity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = classEntity.ScheduleSummary,
            CreatedAt = classEntity.CreatedAt,
            UpdatedAt = classEntity.UpdatedAt,
        };
    }

    public async Task<ClassResponseDto> StartClassAsync(Guid id)
    {
        _logger.LogInformation("[StartClassAsync] class {Id} -> {Status}", id, ClassStatus.InProgress);

        var entity = await _unitOfWork.Classes.GetByIdAsync(id);
        ClassValidator.ValidateTransitionToStatus(entity, id, ClassStatus.InProgress);
        var classEntity = entity!;

        classEntity.Status = ClassStatus.InProgress;

        await _unitOfWork.Classes.Update(classEntity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassStarted(classEntity.Id, classEntity.ProgramId, classEntity.Name));

        _logger.LogInformation("[StartClassAsync] class {Id} is now InProgress.", id);

        var startSeatsTaken = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);

        return new ClassResponseDto
        {
            Id = classEntity.Id,
            Code = classEntity.Code,
            Name = classEntity.Name,
            ProgramId = classEntity.ProgramId,
            MentorId = classEntity.MentorId,
            StartDate = classEntity.StartDate,
            EndDate = classEntity.EndDate,
            MaxCapacity = classEntity.MaxCapacity,
            SeatsTaken = startSeatsTaken,
            Status = classEntity.Status,
            MinHoursBeforeAssignmentJoin = classEntity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = classEntity.ScheduleSummary,
            CreatedAt = classEntity.CreatedAt,
            UpdatedAt = classEntity.UpdatedAt,
        };
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

        foreach (var classEntity in openClasses)
        {
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

        var startedCount = 0;
        var startedClasses = new List<Class>();

        foreach (var classEntity in openClasses)
        {
            activeEnrollmentCounts.TryGetValue(classEntity.Id, out var activeEnrollmentCount);

            if (!ClassValidator.IsReadyForAutoStart(classEntity, activeEnrollmentCount, now))
            {
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

        var completeSeatsTaken = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);

        return new ClassResponseDto
        {
            Id = classEntity.Id,
            Code = classEntity.Code,
            Name = classEntity.Name,
            ProgramId = classEntity.ProgramId,
            MentorId = classEntity.MentorId,
            StartDate = classEntity.StartDate,
            EndDate = classEntity.EndDate,
            MaxCapacity = classEntity.MaxCapacity,
            SeatsTaken = completeSeatsTaken,
            Status = classEntity.Status,
            MinHoursBeforeAssignmentJoin = classEntity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = classEntity.ScheduleSummary,
            CreatedAt = classEntity.CreatedAt,
            UpdatedAt = classEntity.UpdatedAt,
        };
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
            var activeEnrollmentCount = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, id);
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
}
