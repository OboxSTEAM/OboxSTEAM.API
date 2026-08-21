using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassSessionService : IClassSessionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ClassSessionService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public ClassSessionService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassSessionService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<Pagination<ClassSessionResponseDto>> GetClassSessionsByClassIdAsync(
        Guid classId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? moduleId = null,
        SessionKind? sessionKind = null,
        ClassSessionStatus? status = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        _logger.LogInformation(
            "[GetClassSessionsByClassIdAsync] Start — classId: {ClassId}, page: {Page}, pageSize: {PageSize}",
            classId,
            page,
            pageSize);

        ClassSessionValidator.ValidatePagination(page, pageSize);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
        ClassValidator.ValidateClassExists(classEntity, classId);

        var query = _unitOfWork.ClassSessions
            .GetQueryable()
            .Where(cs => cs.ClassId == classId && !cs.IsDeleted);

        if (moduleId.HasValue)
        {
            query = query.Where(cs => cs.ModuleId == moduleId.Value);
        }

        if (sessionKind.HasValue)
        {
            query = query.Where(cs => cs.SessionKind == sessionKind.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(cs => cs.Status == status.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(cs => cs.EndTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(cs => cs.StartTime <= to.Value);
        }

        query = sortBy?.ToLower() switch
        {
            "title" => isDescending ? query.OrderByDescending(cs => cs.Title) : query.OrderBy(cs => cs.Title),
            "starttime" => isDescending ? query.OrderByDescending(cs => cs.StartTime) : query.OrderBy(cs => cs.StartTime),
            "endtime" => isDescending ? query.OrderByDescending(cs => cs.EndTime) : query.OrderBy(cs => cs.EndTime),
            "sessionkind" => isDescending ? query.OrderByDescending(cs => cs.SessionKind) : query.OrderBy(cs => cs.SessionKind),
            "status" => isDescending ? query.OrderByDescending(cs => cs.Status) : query.OrderBy(cs => cs.Status),
            "createdat" => isDescending ? query.OrderByDescending(cs => cs.CreatedAt) : query.OrderBy(cs => cs.CreatedAt),
            _ => isDescending ? query.OrderByDescending(cs => cs.StartTime) : query.OrderBy(cs => cs.StartTime),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = items.Select(cs => new ClassSessionResponseDto
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
        }).ToList();

        _logger.LogInformation(
            "[GetClassSessionsByClassIdAsync] Retrieved {Count}/{Total} sessions for class {ClassId}.",
            dtos.Count,
            totalCount,
            classId);

        return new Pagination<ClassSessionResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ClassSessionResponseDto> GetClassSessionByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetClassSessionByIdAsync] Fetching class session with Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);
        ClassSessionValidator.ValidateClassSessionExists(entity, id);

        _logger.LogInformation("[GetClassSessionByIdAsync] Class session with Id {Id} retrieved successfully.", id);

        return new ClassSessionResponseDto
        {
            Id = entity!.Id,
            ClassId = entity.ClassId,
            ModuleId = entity.ModuleId,
            ActivityId = entity.ActivityId,
            AssignmentId = entity.AssignmentId,
            SessionKind = entity.SessionKind,
            Title = entity.Title,
            Description = entity.Description,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Location = entity.Location,
            MeetingUrl = entity.MeetingUrl,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            RequiresAttendance = entity.RequiresAttendance,
            RequiresMentorCheckIn = entity.RequiresMentorCheckIn,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public async Task<ClassSessionWithStudentsResponseDto> GetClassSessionWithStudentsAsync(Guid id)
    {
        _logger.LogInformation("[GetClassSessionWithStudentsAsync] Fetching class session roster for Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);
        ClassSessionValidator.ValidateClassSessionExists(entity, id);

        var session = entity!;

        var currentUser = await SessionAttendanceValidator.EnsureCanViewSessionRosterAsync(
            _unitOfWork,
            _claimsService,
            session);

        var classEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == session.ClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (currentUser.Role == RoleType.Student)
        {
            classEnrollments = classEnrollments
                .Where(ce => ce.StudentId == currentUser.Id)
                .ToList();
        }

        var attendances = await _unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.ClassSessionId == id && !sa.IsDeleted);
        var attendanceByStudentId = attendances.ToDictionary(sa => sa.StudentId);

        var studentIds = classEnrollments.Select(ce => ce.StudentId).Distinct().ToList();
        var students = studentIds.Any()
            ? await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id) && !u.IsDeleted)
            : new List<User>();

        var studentsById = students.ToDictionary(u => u.Id);

        var programEnrollmentIds = classEnrollments.Select(ce => ce.ProgramEnrollmentId).Distinct().ToList();
        var moduleEnrollments = studentIds.Any()
            ? await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => studentIds.Contains(me.StudentId)
                      && me.ModuleId == session.ModuleId
                      && me.ProgramEnrollmentId.HasValue
                      && programEnrollmentIds.Contains(me.ProgramEnrollmentId.Value)
                      && me.Status == EnrollmentStatus.Active
                      && !me.IsDeleted)
            : new List<ModuleEnrollment>();

        var moduleEnrollmentByStudentAndProgram = moduleEnrollments
            .Where(me => me.ProgramEnrollmentId.HasValue)
            .ToDictionary(me => (me.StudentId, me.ProgramEnrollmentId!.Value));

        var studentDtos = classEnrollments
            .Where(ce => studentsById.ContainsKey(ce.StudentId))
            .OrderBy(ce => studentsById[ce.StudentId].FullName)
            .ThenBy(ce => studentsById[ce.StudentId].Code)
            .Select(ce =>
            {
                var student = studentsById[ce.StudentId];
                attendanceByStudentId.TryGetValue(ce.StudentId, out var attendance);

                var moduleEnrollmentId = attendance?.ModuleEnrollmentId
                    ?? (moduleEnrollmentByStudentAndProgram.TryGetValue(
                            (ce.StudentId, ce.ProgramEnrollmentId),
                            out var moduleEnrollment)
                        ? moduleEnrollment.Id
                        : Guid.Empty);

                return new ClassSessionStudentResponseDto
                {
                    ClassSessionId = session.Id,
                    StudentId = student.Id,
                    StudentCode = student.Code,
                    StudentName = student.FullName,
                    Email = student.Email,
                    Phone = student.Phone,
                    AvatarUrl = student.AvatarUrl,
                    ModuleEnrollmentId = moduleEnrollmentId,
                    AttendanceStatus = attendance?.Status ?? AttendanceStatus.Expected,
                    CheckedInAt = attendance?.CheckedInAt,
                    RecordedBy = attendance?.RecordedBy,
                };
            })
            .ToList();

        _logger.LogInformation(
            "[GetClassSessionWithStudentsAsync] Class session {Id} roster retrieved — {StudentCount} student(s).",
            id,
            studentDtos.Count);

        return new ClassSessionWithStudentsResponseDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            ModuleId = session.ModuleId,
            ActivityId = session.ActivityId,
            AssignmentId = session.AssignmentId,
            SessionKind = session.SessionKind,
            Title = session.Title,
            Description = session.Description,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Location = session.Location,
            MeetingUrl = session.MeetingUrl,
            Latitude = session.Latitude,
            Longitude = session.Longitude,
            RequiresAttendance = session.RequiresAttendance,
            RequiresMentorCheckIn = session.RequiresMentorCheckIn,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            Students = studentDtos,
        };
    }

    public async Task<ClassSessionResponseDto> CreateClassSessionAsync(CreateClassSessionRequestDto request)
    {
        _logger.LogInformation(
            "[CreateClassSessionAsync] Start creating session '{Title}' for class {ClassId}",
            request.Title,
            request.ClassId);

        ClassSessionValidator.ValidateCreateRequest(request);
        ClassSessionValidator.ValidateCoordinates(request.Latitude, request.Longitude);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        ClassValidator.ValidateClassExists(classEntity, request.ClassId);
        ClassSessionValidator.ValidateClassSchedulable(classEntity!);

        await ClassSessionValidator.ValidateReferencesAsync(
            _unitOfWork,
            classEntity!,
            request.ModuleId,
            request.ActivityId,
            request.AssignmentId);

        // Activity: EndTime = StartTime + DurationMinutes (client EndTime ignored).
        // Assignment: client must supply EndTime (no curriculum duration).
        DateTime endTime;
        if (request.ActivityId.HasValue)
        {
            var activity = await _unitOfWork.Activities.GetByIdAsync(request.ActivityId.Value);
            endTime = ClassSessionValidator.ResolveActivitySessionEnd(request.StartTime, activity!);
        }
        else
        {
            endTime = request.EndTime!.Value;
        }

        ClassSessionValidator.ValidateSessionWithinClassDateRange(
            classEntity!,
            request.StartTime,
            endTime);

        // Sessions may be scheduled before a mentor is assigned — the schedule is what
        // mentors review when requesting the class. Overlap is only checkable (and only
        // matters) once a mentor exists.
        if (classEntity!.MentorId.HasValue)
        {
            await MentorScopeValidator.ValidateMentorSessionNoOverlapAsync(
                _unitOfWork,
                classEntity.MentorId.Value,
                request.StartTime,
                endTime);
        }

        var entity = new ClassSession
        {
            ClassId = request.ClassId,
            ModuleId = request.ModuleId,
            ActivityId = request.ActivityId,
            AssignmentId = request.AssignmentId,
            SessionKind = request.SessionKind,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StartTime = request.StartTime,
            EndTime = endTime,
            Location = request.Location?.Trim(),
            MeetingUrl = string.IsNullOrWhiteSpace(request.MeetingUrl) ? null : request.MeetingUrl.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RequiresAttendance = request.RequiresAttendance,
            RequiresMentorCheckIn = request.RequiresMentorCheckIn,
            Status = ClassSessionStatus.Scheduled,
        };

        await _unitOfWork.ClassSessions.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionScheduled(entity.ClassId, entity.Id, classEntity!.ProgramId));

        await SyncReadyForMentorStatusAsync(classEntity);

        _logger.LogInformation(
            "[CreateClassSessionAsync] Class session '{Title}' created with Id {Id}.",
            entity.Title,
            entity.Id);

        return new ClassSessionResponseDto
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            ModuleId = entity.ModuleId,
            ActivityId = entity.ActivityId,
            AssignmentId = entity.AssignmentId,
            SessionKind = entity.SessionKind,
            Title = entity.Title,
            Description = entity.Description,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Location = entity.Location,
            MeetingUrl = entity.MeetingUrl,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            RequiresAttendance = entity.RequiresAttendance,
            RequiresMentorCheckIn = entity.RequiresMentorCheckIn,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public async Task<List<ClassSessionResponseDto>> GenerateClassSessionsAsync(
        Guid classId,
        GenerateClassSessionsRequestDto request)
    {
        _logger.LogInformation("[GenerateClassSessionsAsync] Start — classId: {ClassId}", classId);

        ClassSessionValidator.ValidateGenerateRequest(request);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
        ClassValidator.ValidateClassExists(classEntity, classId);
        ClassSessionValidator.ValidateClassSchedulable(classEntity!);

        // Generating while students are already enrolled would silently move sessions
        // they can see. Draft classes and open-but-empty classes may (re)generate.
        if (classEntity!.Status is ClassStatus.Open or ClassStatus.InProgress)
        {
            var activeEnrollments = await ClassEnrollmentValidator.GetActiveSeatsTakenAsync(_unitOfWork, classId);
            if (activeEnrollments > 0)
            {
                throw ErrorHelper.Conflict(
                    "Cannot generate sessions while the class has enrolled students.");
            }
        }

        var existingSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);

        if (existingSessions.Count > 0)
        {
            throw ErrorHelper.Conflict(
                "Class already has scheduled sessions. Cancel or delete them before generating a new schedule.");
        }

        var items = await BuildCurriculumScheduleItemsAsync(classEntity.ProgramId);
        if (items.Count == 0)
        {
            throw ErrorHelper.BadRequest(
                "The program curriculum has no LiveOnline/Offline activities or assignments to schedule.");
        }

        var slotStarts = BuildWeeklySlotStarts(classEntity, request, items.Count);

        // Each session's end comes from its own length: the activity's DurationMinutes,
        // or the request's start/end window as the default length for assignment sessions
        // (assignments have no activity to carry a duration).
        var assignmentDuration = request.SessionEndTime - request.SessionStartTime;
        var scheduled = items
            .Zip(slotStarts, (item, start) =>
            {
                var duration = item.DurationMinutes is > 0
                    ? TimeSpan.FromMinutes(item.DurationMinutes.Value)
                    : assignmentDuration;
                return (Item: item, Start: start, End: start.Add(duration));
            })
            .ToList();

        // Late-generation guard: slots are chronological, so the first one is the earliest
        // commitment (lesson or assignment window alike). It must be in the future and leave
        // the class's join buffer for students to enroll before it happens.
        var now = DateTime.UtcNow;
        var firstStart = scheduled[0].Start;

        if (firstStart <= now)
        {
            throw ErrorHelper.BadRequest(
                $"The first session would start at {firstStart:yyyy-MM-dd HH:mm} UTC, which is in the past. " +
                "Move the class start date forward before generating the schedule.");
        }

        var bufferHours = classEntity.MinHoursBeforeAssignmentJoin;
        if (firstStart < now.AddHours(bufferHours))
        {
            throw ErrorHelper.BadRequest(
                $"The first session starts at {firstStart:yyyy-MM-dd HH:mm} UTC, less than the class's " +
                $"{bufferHours}-hour enrollment buffer from now. Move the class start date forward " +
                "so students have time to enroll.");
        }

        // No mentor yet: overlap is enforced later, when a mentor requests the class
        // (request-time and approve-time checks).
        if (classEntity.MentorId.HasValue)
        {
            foreach (var slot in scheduled)
            {
                await MentorScopeValidator.ValidateMentorSessionNoOverlapAsync(
                    _unitOfWork,
                    classEntity.MentorId.Value,
                    slot.Start,
                    slot.End);
            }
        }

        var entities = scheduled
            .Select(slot => new ClassSession
            {
                ClassId = classId,
                ModuleId = slot.Item.ModuleId,
                ActivityId = slot.Item.ActivityId,
                AssignmentId = slot.Item.AssignmentId,
                SessionKind = slot.Item.Kind,
                Title = slot.Item.Title,
                StartTime = slot.Start,
                EndTime = slot.End,
                // Venue/join link are set per session by the manager afterwards — the
                // curriculum template no longer carries them.
                Location = null,
                MeetingUrl = null,
                RequiresAttendance = slot.Item.RequiresAttendance,
                Status = ClassSessionStatus.Scheduled,
            })
            .ToList();

        await _unitOfWork.ClassSessions.AddRangeAsync(entities);
        await _unitOfWork.SaveChangesAsync();

        var notifications = entities
            .Select(e => NotificationCatalog.ClassSessionScheduled(classId, e.Id, classEntity.ProgramId))
            .ToList();
        await _notificationPublisher.PublishManyAsync(notifications);

        await SyncReadyForMentorStatusAsync(classEntity);

        _logger.LogInformation(
            "[GenerateClassSessionsAsync] Generated {Count} sessions for class {ClassId}.",
            entities.Count,
            classId);

        return entities.Select(MapToResponseDto).ToList();
    }

    private sealed record CurriculumScheduleItem(
        Guid ModuleId,
        Guid? ActivityId,
        Guid? AssignmentId,
        SessionKind Kind,
        string Title,
        int? DurationMinutes,
        bool RequiresAttendance);

    private async Task<List<CurriculumScheduleItem>> BuildCurriculumScheduleItemsAsync(Guid programId)
    {
        var modules = (await _unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == programId && !m.IsDeleted))
            .OrderBy(m => m.ModuleOrder)
            .ToList();

        if (modules.Count == 0)
        {
            return new List<CurriculumScheduleItem>();
        }

        var moduleIds = modules.Select(m => m.Id).ToList();

        var courses = await _unitOfWork.Courses.GetAllAsync(
            c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
        var courseIds = courses.Select(c => c.Id).ToList();

        var activities = await _unitOfWork.Activities.GetAllAsync(
            a => courseIds.Contains(a.CourseId)
                 && !a.IsDeleted
                 && (a.ActivityType == ActivityType.LiveOnline || a.ActivityType == ActivityType.Offline));

        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted);

        var items = new List<CurriculumScheduleItem>();

        foreach (var module in modules)
        {
            foreach (var course in courses
                         .Where(c => c.ModuleId == module.Id)
                         .OrderBy(c => c.CourseOrder)
                         .ThenBy(c => c.Code))
            {
                foreach (var activity in activities
                             .Where(a => a.CourseId == course.Id)
                             .OrderBy(a => a.ActivityOrder))
                {
                    if (activity.DurationMinutes is null or <= 0)
                    {
                        throw ErrorHelper.BadRequest(
                            $"Activity '{activity.Name}' ({activity.Code}) has no DurationMinutes. " +
                            "Set a session length on the curriculum activity before generating the schedule.");
                    }

                    var isOffline = activity.ActivityType == ActivityType.Offline;
                    items.Add(new CurriculumScheduleItem(
                        module.Id,
                        activity.Id,
                        null,
                        isOffline ? SessionKind.FieldTrip : SessionKind.Lesson,
                        activity.Name,
                        activity.DurationMinutes,
                        RequiresAttendance: true));
                }
            }

            foreach (var assignment in assignments
                         .Where(a => a.ModuleId == module.Id)
                         .OrderBy(a => a.CreatedAt)
                         .ThenBy(a => a.Code))
            {
                items.Add(new CurriculumScheduleItem(
                    module.Id,
                    null,
                    assignment.Id,
                    SessionKind.AssignmentWindow,
                    assignment.Title,
                    null,
                    RequiresAttendance: false));
            }
        }

        return items;
    }

    private static List<DateTime> BuildWeeklySlotStarts(
        Class classEntity,
        GenerateClassSessionsRequestDto request,
        int count)
    {
        var daysOfWeek = request.DaysOfWeek.Distinct().ToHashSet();
        var starts = new List<DateTime>(count);

        for (var day = classEntity.StartDate.Date; starts.Count < count; day = day.AddDays(1))
        {
            if (day > classEntity.EndDate.Date)
            {
                throw ErrorHelper.BadRequest(
                    $"The class date range only fits {starts.Count} of {count} sessions with the selected weekly pattern. " +
                    "Extend the class end date or add more session days per week.");
            }

            if (!daysOfWeek.Contains(day.DayOfWeek))
            {
                continue;
            }

            starts.Add(day.Add(request.SessionStartTime.ToTimeSpan()));
        }

        return starts;
    }

    private static ClassSessionResponseDto MapToResponseDto(ClassSession session) => new()
    {
        Id = session.Id,
        ClassId = session.ClassId,
        ModuleId = session.ModuleId,
        ActivityId = session.ActivityId,
        AssignmentId = session.AssignmentId,
        SessionKind = session.SessionKind,
        Title = session.Title,
        Description = session.Description,
        StartTime = session.StartTime,
        EndTime = session.EndTime,
        Location = session.Location,
        MeetingUrl = session.MeetingUrl,
        Latitude = session.Latitude,
        Longitude = session.Longitude,
        RequiresAttendance = session.RequiresAttendance,
        RequiresMentorCheckIn = session.RequiresMentorCheckIn,
        Status = session.Status,
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.UpdatedAt,
    };

    public async Task<ClassSessionResponseDto> UpdateClassSessionAsync(
        Guid id,
        UpdateClassSessionRequestDto request)
    {
        _logger.LogInformation("[UpdateClassSessionAsync] Attempting to update class session with Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);
        ClassSessionValidator.ValidateClassSessionExists(entity, id);
        var session = entity!;
        ClassSessionValidator.ValidateSessionModifiable(session);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(session.ClassId);
        ClassValidator.ValidateClassExists(classEntity, session.ClassId);

        var originalStatus = session.Status;
        var originalActivityId = session.ActivityId;

        var targetModuleId = session.ModuleId;
        var targetActivityId = session.ActivityId;
        var targetAssignmentId = session.AssignmentId;
        var targetStartTime = session.StartTime;
        var targetEndTime = session.EndTime;
        var timeChanged = false;

        if (request.ModuleId.HasValue)
        {
            targetModuleId = request.ModuleId.Value;
            session.ModuleId = request.ModuleId.Value;
        }

        if (request.ActivityId.HasValue)
        {
            targetActivityId = request.ActivityId;
            session.ActivityId = request.ActivityId;
            // Relinking to an activity clears any prior assignment link so XOR holds.
            if (session.AssignmentId.HasValue)
            {
                session.AssignmentId = null;
                targetAssignmentId = null;
            }
        }

        if (request.AssignmentId.HasValue)
        {
            targetAssignmentId = request.AssignmentId;
            session.AssignmentId = request.AssignmentId;
            if (session.ActivityId.HasValue)
            {
                session.ActivityId = null;
                targetActivityId = null;
            }
        }

        ClassSessionValidator.ValidateExactlyOneCurriculumItem(targetActivityId, targetAssignmentId);

        await ClassSessionValidator.ValidateReferencesAsync(
            _unitOfWork,
            classEntity!,
            targetModuleId,
            targetActivityId,
            targetAssignmentId,
            excludeSessionId: session.Id);

        if (request.SessionKind.HasValue)
        {
            session.SessionKind = request.SessionKind.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            session.Title = request.Title.Trim();
        }

        if (request.Description != null)
        {
            session.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        }

        if (targetActivityId.HasValue)
        {
            // Activity sessions: EndTime is never client-controlled. Moving StartTime (or
            // relinking the activity) recomputes End from DurationMinutes.
            ClassSessionValidator.ValidateActivitySessionEndNotOverridden(request.EndTime);

            var activityRelinked = request.ActivityId.HasValue
                                  && request.ActivityId != originalActivityId;
            if (request.StartTime.HasValue || activityRelinked)
            {
                if (request.StartTime.HasValue)
                {
                    targetStartTime = request.StartTime.Value;
                    session.StartTime = request.StartTime.Value;
                }

                var activity = await _unitOfWork.Activities.GetByIdAsync(targetActivityId.Value);
                targetEndTime = ClassSessionValidator.ResolveActivitySessionEnd(
                    targetStartTime, activity!);
                session.EndTime = targetEndTime;
                timeChanged = true;
            }
        }
        else
        {
            // Assignment windows: Start/End remain client-editable.
            if (request.StartTime.HasValue)
            {
                targetStartTime = request.StartTime.Value;
                session.StartTime = request.StartTime.Value;
                timeChanged = true;
            }

            if (request.EndTime.HasValue)
            {
                targetEndTime = request.EndTime.Value;
                session.EndTime = request.EndTime.Value;
                timeChanged = true;
            }

            if (timeChanged && targetEndTime <= targetStartTime)
            {
                throw ErrorHelper.BadRequest("EndTime must be after StartTime.");
            }
        }

        if (timeChanged)
        {
            ClassSessionValidator.ValidateSessionWithinClassDateRange(
                classEntity!,
                targetStartTime,
                targetEndTime);

            if (classEntity!.MentorId.HasValue)
            {
                await MentorScopeValidator.ValidateMentorSessionNoOverlapAsync(
                    _unitOfWork,
                    classEntity.MentorId.Value,
                    targetStartTime,
                    targetEndTime,
                    excludeSessionId: session.Id);
            }
        }

        if (request.Location != null)
        {
            session.Location = string.IsNullOrWhiteSpace(request.Location)
                ? null
                : request.Location.Trim();
        }

        if (request.MeetingUrl != null)
        {
            session.MeetingUrl = string.IsNullOrWhiteSpace(request.MeetingUrl)
                ? null
                : request.MeetingUrl.Trim();
        }

        if (request.Latitude.HasValue || request.Longitude.HasValue)
        {
            ClassSessionValidator.ValidateCoordinates(request.Latitude, request.Longitude);
            session.Latitude = request.Latitude;
            session.Longitude = request.Longitude;
        }

        if (request.RequiresAttendance.HasValue)
        {
            session.RequiresAttendance = request.RequiresAttendance.Value;
        }

        if (request.RequiresMentorCheckIn.HasValue)
        {
            session.RequiresMentorCheckIn = request.RequiresMentorCheckIn.Value;
        }

        if (request.Status.HasValue)
        {
            ClassSessionValidator.ValidateStatusTransition(session.Status, request.Status.Value);
            session.Status = request.Status.Value;
        }

        await _unitOfWork.ClassSessions.Update(session);
        await _unitOfWork.SaveChangesAsync();

        var sessionNotifications = new List<NotificationCommand>();

        if (request.Status.HasValue && session.Status != originalStatus)
        {
            switch (session.Status)
            {
                case ClassSessionStatus.InProgress:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionStarted(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
                case ClassSessionStatus.Completed:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionCompleted(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
                case ClassSessionStatus.Cancelled:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionCancelled(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
                default:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionRescheduled(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
            }
        }
        else if (timeChanged)
        {
            sessionNotifications.Add(
                NotificationCatalog.ClassSessionRescheduled(session.ClassId, session.Id, classEntity!.ProgramId));
        }

        if (sessionNotifications.Count > 0)
        {
            await _notificationPublisher.PublishManyAsync(sessionNotifications);
        }

        await SyncReadyForMentorStatusAsync(classEntity!);

        _logger.LogInformation("[UpdateClassSessionAsync] Class session Id {Id} updated successfully.", id);

        return new ClassSessionResponseDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            ModuleId = session.ModuleId,
            ActivityId = session.ActivityId,
            AssignmentId = session.AssignmentId,
            SessionKind = session.SessionKind,
            Title = session.Title,
            Description = session.Description,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Location = session.Location,
            MeetingUrl = session.MeetingUrl,
            Latitude = session.Latitude,
            Longitude = session.Longitude,
            RequiresAttendance = session.RequiresAttendance,
            RequiresMentorCheckIn = session.RequiresMentorCheckIn,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
        };
    }

    public async Task<bool> DeleteClassSessionAsync(Guid id)
    {
        _logger.LogInformation("[DeleteClassSessionAsync] Attempting to soft-delete class session Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);

        if (entity == null || entity.IsDeleted)
        {
            _logger.LogWarning("[DeleteClassSessionAsync] Class session with Id {Id} not found.", id);
            return false;
        }

        var classId = entity.ClassId;
        var sessionId = entity.Id;

        await _unitOfWork.ClassSessions.SoftRemove(entity);
        await _unitOfWork.SaveChangesAsync();

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionCancelled(classId, sessionId, classEntity?.ProgramId));

        if (classEntity != null)
        {
            await SyncReadyForMentorStatusAsync(classEntity);
        }

        _logger.LogInformation("[DeleteClassSessionAsync] Class session Id {Id} soft-deleted successfully.", id);

        return true;
    }

    /// <summary>
    /// Draft becomes ReadyForMentor when the timetable covers the curriculum.
    /// ReadyForMentor falls back to Draft when coverage is lost.
    /// </summary>
    private async Task SyncReadyForMentorStatusAsync(Class classEntity)
    {
        if (classEntity.IsDeleted
            || classEntity.Status is not (ClassStatus.Draft or ClassStatus.ReadyForMentor))
        {
            return;
        }

        var activeSessions = ClassScheduleCoverage.CountActiveSessions(_unitOfWork, classEntity.Id);
        var schedulableItems = await ClassScheduleCoverage.CountSchedulableItemsAsync(
            _unitOfWork,
            classEntity.ProgramId);
        var covers = ClassScheduleCoverage.CoversCurriculum(activeSessions, schedulableItems);

        if (classEntity.Status == ClassStatus.Draft && covers)
        {
            if (classEntity.StartDate <= DateTime.UtcNow)
            {
                return;
            }

            classEntity.Status = ClassStatus.ReadyForMentor;
            await _unitOfWork.Classes.Update(classEntity);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "[SyncReadyForMentorStatusAsync] class {Id} promoted to ReadyForMentor.",
                classEntity.Id);
            return;
        }

        if (classEntity.Status == ClassStatus.ReadyForMentor && !covers)
        {
            classEntity.Status = ClassStatus.Draft;
            await _unitOfWork.Classes.Update(classEntity);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "[SyncReadyForMentorStatusAsync] class {Id} returned to Draft — schedule no longer covers the curriculum.",
                classEntity.Id);
        }
    }
}
