using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassEnrollmentDTO;
using OboxSteam.Application.DTOs.MentorDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassEnrollmentService : IClassEnrollmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IClassService _classService;
    private readonly ILogger<ClassEnrollmentService> _logger;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IClassSeatHoldService _classSeatHoldService;

    public ClassEnrollmentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IClassService classService,
        ILogger<ClassEnrollmentService> logger,
        INotificationPublisher notificationPublisher,
        IClassSeatHoldService classSeatHoldService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _classService = classService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
        _classSeatHoldService = classSeatHoldService;
    }

    public async Task<ClassEnrollmentResponseDto> EnrollClassAsync(CreateClassEnrollmentRequestDto request)
    {
        ClassEnrollmentValidator.ValidateProgramEnrollmentIdRequired(request.ProgramEnrollmentId);
        ClassEnrollmentValidator.ValidateClassIdRequired(request.ClassId);

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ClassEnrollmentValidator.EnrollForbiddenMessage);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(request.ProgramEnrollmentId);
        var programEnrollment = ClassEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            request.ProgramEnrollmentId);
        ClassEnrollmentValidator.ValidateProgramEnrollmentBelongsToStudent(programEnrollment, student.Id);
        ClassEnrollmentValidator.ValidateProgramEnrollmentActiveForEnroll(programEnrollment);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        var classToJoin = ClassEnrollmentValidator.ValidateClassExists(classEntity, request.ClassId);
        ClassEnrollmentValidator.ValidateClassBelongsToProgram(classToJoin, programEnrollment.ProgramId);
        ClassEnrollmentValidator.ValidateClassOpenForEnrollment(classToJoin);

        var activeEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == request.ProgramEnrollmentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
        ClassEnrollmentValidator.ValidateNoActiveClassEnrollmentForProgram(activeEnrollment);

        await ClassEnrollmentValidator.ValidateUnderActiveClassLimitAsync(
            _unitOfWork,
            student.Id);

        var existingInClass = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ClassId == request.ClassId
                  && ce.StudentId == student.Id
                  && !ce.IsDeleted);
        if (existingInClass != null)
        {
            throw ErrorHelper.Conflict("You are already enrolled in this class.");
        }

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(
            _unitOfWork,
            request.ClassId,
            classToJoin.MaxCapacity);
        await ClassEnrollmentValidator.ValidateLateJoinAllowedAsync(_unitOfWork, classToJoin);
        await ScheduleConflictValidator.ValidateStudentCanJoinClassAsync(
            _unitOfWork,
            student.Id,
            request.ClassId);

        var now = DateTime.UtcNow;
        var enrollment = new ClassEnrollment
        {
            ClassId = request.ClassId,
            StudentId = student.Id,
            ProgramEnrollmentId = request.ProgramEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = now,
        };

        await _unitOfWork.ClassEnrollments.AddAsync(enrollment);
        await _unitOfWork.SaveChangesAsync();

        var nextActivityId = await NotificationDeeplinkResolver.ResolveCurrentActivityIdAsync(
            _unitOfWork,
            programEnrollment.ProgramId,
            programEnrollment.Id);

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassEnrolled(
                student.Id,
                request.ClassId,
                enrollment.Id,
                programEnrollment.ProgramId,
                classToJoin.Name,
                programEnrollment.Id,
                nextActivityId));

        await _classService.TryAutoStartClassIfReadyAsync(request.ClassId);

        await _classSeatHoldService.PublishSeatsChangedAsync(
            programEnrollment.ProgramId,
            request.ClassId);

        var classEntityAfterStart = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        classToJoin = ClassEnrollmentValidator.ValidateClassExists(classEntityAfterStart, request.ClassId);

        _logger.LogInformation(
            "[EnrollClassAsync] Student {StudentId} enrolled in class {ClassId}, enrollment {EnrollmentId}.",
            student.Id,
            request.ClassId,
            enrollment.Id);

        return new ClassEnrollmentResponseDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            ProgramEnrollmentId = enrollment.ProgramEnrollmentId,
            Kind = enrollment.Kind,
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Class = await MapClassResponseAsync(classToJoin),
        };
    }

    public async Task<ClassEnrollmentResponseDto> TransferClassAsync(
        Guid id,
        UpdateClassEnrollmentRequestDto request)
    {
        ClassEnrollmentValidator.ValidateClassIdRequired(request.ClassId);

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ClassEnrollmentValidator.EnrollForbiddenMessage);

        var enrollmentEntity = await _unitOfWork.ClassEnrollments.GetByIdAsync(id, ce => ce.Class);
        var enrollment = ClassEnrollmentValidator.ValidateClassEnrollmentExists(enrollmentEntity, id);
        ClassEnrollmentValidator.ValidateClassEnrollmentBelongsToStudent(enrollment, student.Id);
        ClassEnrollmentValidator.ValidateEnrollmentActive(enrollment);
        ClassEnrollmentValidator.ValidateTransferTargetDifferent(enrollment.ClassId, request.ClassId);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollment.ProgramEnrollmentId);
        var programEnrollment = ClassEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            enrollment.ProgramEnrollmentId);
        ClassEnrollmentValidator.ValidateProgramEnrollmentActiveForEnroll(programEnrollment);

        var targetClassEntity = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        var targetClass = ClassEnrollmentValidator.ValidateClassExists(targetClassEntity, request.ClassId);
        ClassEnrollmentValidator.ValidateClassBelongsToProgram(targetClass, programEnrollment.ProgramId);
        ClassEnrollmentValidator.ValidateClassOpenForEnrollment(targetClass);

        var existingInTargetClass = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ClassId == request.ClassId
                  && ce.StudentId == student.Id
                  && !ce.IsDeleted);
        ClassEnrollmentValidator.ValidateNotAlreadyEnrolledInClass(existingInTargetClass, enrollment.Id);

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(
            _unitOfWork,
            request.ClassId,
            targetClass.MaxCapacity);
        await ClassEnrollmentValidator.ValidateLateJoinAllowedAsync(_unitOfWork, targetClass);
        await ScheduleConflictValidator.ValidateStudentCanJoinClassAsync(
            _unitOfWork,
            student.Id,
            request.ClassId,
            excludeClassId: enrollment.ClassId);

        var sourceClassId = enrollment.ClassId;
        enrollment.ClassId = request.ClassId;
        enrollment.EnrolledAt = DateTime.UtcNow;

        await _unitOfWork.ClassEnrollments.Update(enrollment);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassTransferred(
                student.Id,
                request.ClassId,
                enrollment.Id,
                programEnrollment.ProgramId,
                targetClass.Name,
                programEnrollment.Id));

        await _classService.TryAutoStartClassIfReadyAsync(request.ClassId);

        await _classSeatHoldService.PublishSeatsChangedAsync(programEnrollment.ProgramId, sourceClassId);
        await _classSeatHoldService.PublishSeatsChangedAsync(programEnrollment.ProgramId, request.ClassId);

        var targetClassAfterStart = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        targetClass = ClassEnrollmentValidator.ValidateClassExists(targetClassAfterStart, request.ClassId);

        _logger.LogInformation(
            "[TransferClassAsync] Student {StudentId} transferred enrollment {EnrollmentId} to class {ClassId}.",
            student.Id,
            enrollment.Id,
            request.ClassId);

        return new ClassEnrollmentResponseDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            ProgramEnrollmentId = enrollment.ProgramEnrollmentId,
            Kind = enrollment.Kind,
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Class = await MapClassResponseAsync(targetClass),
        };
    }

    public async Task<ClassEnrollmentResponseDto> TransferClassByManagerAsync(
        Guid id,
        ManagerTransferClassRequestDto request)
    {
        ClassEnrollmentValidator.ValidateStudentIdRequired(id);
        ClassEnrollmentValidator.ValidateClassIdRequired(request.ClassId);

        await EnrollmentAccessValidator.GetCurrentManagerAsync(
            _unitOfWork,
            _claimsService,
            ClassEnrollmentValidator.ManagerTransferForbiddenMessage);

        var studentEntity = await _unitOfWork.Users.GetByIdAsync(id);
        var student = ProgramEnrollmentValidator.ValidateStudentExists(studentEntity, id);

        var targetClassEntity = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        var targetClass = ClassEnrollmentValidator.ValidateClassExists(targetClassEntity, request.ClassId);
        ClassEnrollmentValidator.ValidateClassOpenForManagerTransfer(targetClass);

        var enrollmentEntity = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == student.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted
                  && ce.Class.ProgramId == targetClass.ProgramId,
            ce => ce.Class);
        var enrollment = ClassEnrollmentValidator.ValidateActiveClassEnrollmentForProgram(
            enrollmentEntity,
            student.Id,
            targetClass.ProgramId);
        ClassEnrollmentValidator.ValidateTransferTargetDifferent(enrollment.ClassId, request.ClassId);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollment.ProgramEnrollmentId);
        var programEnrollment = ClassEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            enrollment.ProgramEnrollmentId);
        ClassEnrollmentValidator.ValidateProgramEnrollmentActiveForEnroll(programEnrollment);
        ClassEnrollmentValidator.ValidateClassBelongsToProgram(targetClass, programEnrollment.ProgramId);

        var existingInTargetClass = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ClassId == request.ClassId
                  && ce.StudentId == student.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
        ClassEnrollmentValidator.ValidateNotAlreadyEnrolledInClass(existingInTargetClass, enrollment.Id);

        await ClassEnrollmentValidator.ValidateUnderActiveClassLimitAsync(
            _unitOfWork,
            student.Id,
            excludeEnrollmentId: enrollment.Id);

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(
            _unitOfWork,
            request.ClassId,
            targetClass.MaxCapacity);
        await ScheduleConflictValidator.ValidateStudentCanJoinClassAsync(
            _unitOfWork,
            student.Id,
            request.ClassId,
            excludeClassId: enrollment.ClassId);

        var sourceClassId = enrollment.ClassId;
        enrollment.Status = ClassEnrollmentStatus.Transferred;

        var newEnrollment = new ClassEnrollment
        {
            ClassId = request.ClassId,
            StudentId = student.Id,
            ProgramEnrollmentId = enrollment.ProgramEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow,
        };

        await _unitOfWork.ClassEnrollments.Update(enrollment);
        await _unitOfWork.ClassEnrollments.AddAsync(newEnrollment);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassTransferred(
                student.Id,
                request.ClassId,
                newEnrollment.Id,
                programEnrollment.ProgramId,
                targetClass.Name,
                programEnrollment.Id));

        await _classService.TryAutoStartClassIfReadyAsync(request.ClassId);

        await _classSeatHoldService.PublishSeatsChangedAsync(programEnrollment.ProgramId, sourceClassId);
        await _classSeatHoldService.PublishSeatsChangedAsync(programEnrollment.ProgramId, request.ClassId);

        var targetClassAfterStart = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        targetClass = ClassEnrollmentValidator.ValidateClassExists(targetClassAfterStart, request.ClassId);

        _logger.LogInformation(
            "[TransferClassByManagerAsync] Manager transferred student {StudentId} from enrollment {OldEnrollmentId} to new enrollment {NewEnrollmentId} in class {ClassId}.",
            student.Id,
            enrollment.Id,
            newEnrollment.Id,
            request.ClassId);

        return new ClassEnrollmentResponseDto
        {
            Id = newEnrollment.Id,
            StudentId = newEnrollment.StudentId,
            ProgramEnrollmentId = newEnrollment.ProgramEnrollmentId,
            Kind = newEnrollment.Kind,
            Status = newEnrollment.Status,
            EnrolledAt = newEnrollment.EnrolledAt,
            CreatedAt = newEnrollment.CreatedAt,
            UpdatedAt = newEnrollment.UpdatedAt,
            Class = await MapClassResponseAsync(targetClass),
        };
    }

    public async Task<ClassEnrollmentResponseDto> GetClassEnrollmentByIdAsync(Guid id)
    {
        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ClassEnrollmentValidator.ViewListForbiddenMessage);

        var enrollmentEntity = await _unitOfWork.ClassEnrollments.GetByIdAsync(id, ce => ce.Class);
        var enrollment = ClassEnrollmentValidator.ValidateClassEnrollmentExists(enrollmentEntity, id);

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            ClassEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var classEntity = ClassEnrollmentValidator.ValidateClassExists(enrollment.Class, enrollment.ClassId);

        return new ClassEnrollmentResponseDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            ProgramEnrollmentId = enrollment.ProgramEnrollmentId,
            Kind = enrollment.Kind,
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Class = await MapClassResponseAsync(classEntity),
        };
    }

    public async Task<Pagination<ClassEnrollmentResponseDto>> GetClassEnrollmentsByProgramEnrollmentAsync(
        Guid programEnrollmentId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize)
    {
        _logger.LogInformation(
            "[GetClassEnrollmentsByProgramEnrollmentAsync] Start — programEnrollmentId: {ProgramEnrollmentId}, page: {Page}, pageSize: {PageSize}",
            programEnrollmentId,
            page,
            pageSize);

        ClassValidator.ValidatePagination(page, pageSize);
        ClassEnrollmentValidator.ValidateProgramEnrollmentIdRequired(programEnrollmentId);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        var programEnrollment = ClassEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            programEnrollmentId);

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            programEnrollment.StudentId,
            ClassEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var query = _unitOfWork.ClassEnrollments
            .GetQueryable()
            .Where(ce => ce.ProgramEnrollmentId == programEnrollmentId && !ce.IsDeleted);

        query = sortBy?.ToLower() switch
        {
            "status" => isDescending
                ? query.OrderByDescending(ce => ce.Status)
                : query.OrderBy(ce => ce.Status),
            "enrolledat" => isDescending
                ? query.OrderByDescending(ce => ce.EnrolledAt)
                : query.OrderBy(ce => ce.EnrolledAt),
            "createdat" => isDescending
                ? query.OrderByDescending(ce => ce.CreatedAt)
                : query.OrderBy(ce => ce.CreatedAt),
            "classname" => isDescending
                ? query.OrderByDescending(ce => ce.Class.Name)
                : query.OrderBy(ce => ce.Class.Name),
            "classcode" => isDescending
                ? query.OrderByDescending(ce => ce.Class.Code)
                : query.OrderBy(ce => ce.Class.Code),
            _ => isDescending
                ? query.OrderByDescending(ce => ce.EnrolledAt).ThenByDescending(ce => ce.CreatedAt)
                : query.OrderBy(ce => ce.EnrolledAt).ThenBy(ce => ce.CreatedAt),
        };

        var totalCount = query.Count();

        var enrollments = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var classIds = enrollments.Select(ce => ce.ClassId).Distinct().ToList();
        var classes = await _unitOfWork.Classes.GetAllAsync(c => classIds.Contains(c.Id) && !c.IsDeleted);
        var classesById = classes.ToDictionary(c => c.Id);

        var dtos = new List<ClassEnrollmentResponseDto>();
        foreach (var enrollment in enrollments)
        {
            classesById.TryGetValue(enrollment.ClassId, out var classEntity);
            var classInfo = ClassEnrollmentValidator.ValidateClassExists(classEntity, enrollment.ClassId);
            dtos.Add(new ClassEnrollmentResponseDto
            {
                Id = enrollment.Id,
                StudentId = enrollment.StudentId,
                ProgramEnrollmentId = enrollment.ProgramEnrollmentId,
                Kind = enrollment.Kind,
                Status = enrollment.Status,
                EnrolledAt = enrollment.EnrolledAt,
                CreatedAt = enrollment.CreatedAt,
                UpdatedAt = enrollment.UpdatedAt,
                Class = await MapClassResponseAsync(classInfo),
            });
        }

        _logger.LogInformation(
            "[GetClassEnrollmentsByProgramEnrollmentAsync] Retrieved {Count}/{Total} class enrollments.",
            dtos.Count,
            totalCount);

        return new Pagination<ClassEnrollmentResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<List<StudentScheduleIntervalDto>> GetMyScheduleAsync()
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            "Only students can view their class schedule.");

        var sessions = await ScheduleConflictValidator.GetStudentBusySessionsAsync(_unitOfWork, student.Id);
        var classIds = sessions.Select(cs => cs.ClassId).Distinct().ToList();
        var classes = classIds.Count == 0
            ? []
            : await _unitOfWork.Classes.GetAllAsync(c => classIds.Contains(c.Id));
        var classesById = classes.ToDictionary(c => c.Id);

        return sessions
            .OrderBy(cs => cs.StartTime)
            .ThenBy(cs => cs.Title)
            .Select(cs =>
            {
                classesById.TryGetValue(cs.ClassId, out var classEntity);
                return new StudentScheduleIntervalDto
                {
                    ClassSessionId = cs.Id,
                    ClassId = cs.ClassId,
                    ClassCode = classEntity?.Code ?? string.Empty,
                    ClassName = classEntity?.Name ?? string.Empty,
                    Title = cs.Title,
                    StartTime = cs.StartTime,
                    EndTime = cs.EndTime,
                    SessionKind = cs.SessionKind,
                    Status = cs.Status,
                };
            })
            .ToList();
    }

    private async Task<ClassResponseDto> MapClassResponseAsync(Class classEntity)
    {
        MentorSummaryDto? mentor = null;
        if (classEntity.MentorId.HasValue)
        {
            var mentorUser = await _unitOfWork.Users.GetByIdAsync(classEntity.MentorId.Value);
            if (mentorUser != null && !mentorUser.IsDeleted)
            {
                var profile = await _unitOfWork.MentorProfiles.FirstOrDefaultAsync(
                    mp => mp.MentorId == mentorUser.Id && !mp.IsDeleted);
                mentor = MentorSummaryMapper.ToSummary(mentorUser, profile);
            }
        }

        var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, classEntity.Id);

        return new ClassResponseDto
        {
            Id = classEntity.Id,
            Code = classEntity.Code,
            Name = classEntity.Name,
            ProgramId = classEntity.ProgramId,
            MentorId = classEntity.MentorId,
            Mentor = mentor,
            StartDate = classEntity.StartDate,
            EndDate = classEntity.EndDate,
            MaxCapacity = classEntity.MaxCapacity,
            SeatsTaken = seatsTaken,
            Kind = classEntity.Kind,
            RemedialModuleId = classEntity.RemedialModuleId,
            Status = classEntity.Status,
            MinHoursBeforeAssignmentJoin = classEntity.MinHoursBeforeAssignmentJoin,
            ScheduleSummary = classEntity.ScheduleSummary,
            CreatedAt = classEntity.CreatedAt,
            UpdatedAt = classEntity.UpdatedAt,
        };
    }
}
