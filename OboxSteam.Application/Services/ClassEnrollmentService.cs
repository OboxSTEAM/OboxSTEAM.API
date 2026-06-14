using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassEnrollmentDTO;
using OboxSteam.Application.Interfaces;
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

    public ClassEnrollmentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IClassService classService,
        ILogger<ClassEnrollmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _classService = classService;
        _logger = logger;
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

        await _classService.TryAutoStartClassIfReadyAsync(request.ClassId);

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
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Class = new ClassResponseDto
            {
                Id = classToJoin.Id,
                Code = classToJoin.Code,
                Name = classToJoin.Name,
                ProgramId = classToJoin.ProgramId,
                MentorId = classToJoin.MentorId,
                StartDate = classToJoin.StartDate,
                EndDate = classToJoin.EndDate,
                MaxCapacity = classToJoin.MaxCapacity,
                Status = classToJoin.Status,
                MinHoursBeforeAssignmentJoin = classToJoin.MinHoursBeforeAssignmentJoin,
                ScheduleSummary = classToJoin.ScheduleSummary,
                CreatedAt = classToJoin.CreatedAt,
                UpdatedAt = classToJoin.UpdatedAt,
            },
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

        enrollment.ClassId = request.ClassId;
        enrollment.EnrolledAt = DateTime.UtcNow;

        await _unitOfWork.ClassEnrollments.Update(enrollment);
        await _unitOfWork.SaveChangesAsync();

        await _classService.TryAutoStartClassIfReadyAsync(request.ClassId);

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
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Class = new ClassResponseDto
            {
                Id = targetClass.Id,
                Code = targetClass.Code,
                Name = targetClass.Name,
                ProgramId = targetClass.ProgramId,
                MentorId = targetClass.MentorId,
                StartDate = targetClass.StartDate,
                EndDate = targetClass.EndDate,
                MaxCapacity = targetClass.MaxCapacity,
                Status = targetClass.Status,
                MinHoursBeforeAssignmentJoin = targetClass.MinHoursBeforeAssignmentJoin,
                ScheduleSummary = targetClass.ScheduleSummary,
                CreatedAt = targetClass.CreatedAt,
                UpdatedAt = targetClass.UpdatedAt,
            },
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
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Class = new ClassResponseDto
            {
                Id = classEntity.Id,
                Code = classEntity.Code,
                Name = classEntity.Name,
                ProgramId = classEntity.ProgramId,
                MentorId = classEntity.MentorId,
                StartDate = classEntity.StartDate,
                EndDate = classEntity.EndDate,
                MaxCapacity = classEntity.MaxCapacity,
                Status = classEntity.Status,
                MinHoursBeforeAssignmentJoin = classEntity.MinHoursBeforeAssignmentJoin,
                ScheduleSummary = classEntity.ScheduleSummary,
                CreatedAt = classEntity.CreatedAt,
                UpdatedAt = classEntity.UpdatedAt,
            },
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
                Status = enrollment.Status,
                EnrolledAt = enrollment.EnrolledAt,
                CreatedAt = enrollment.CreatedAt,
                UpdatedAt = enrollment.UpdatedAt,
                Class = new ClassResponseDto
                {
                    Id = classInfo.Id,
                    Code = classInfo.Code,
                    Name = classInfo.Name,
                    ProgramId = classInfo.ProgramId,
                    MentorId = classInfo.MentorId,
                    StartDate = classInfo.StartDate,
                    EndDate = classInfo.EndDate,
                    MaxCapacity = classInfo.MaxCapacity,
                    Status = classInfo.Status,
                    MinHoursBeforeAssignmentJoin = classInfo.MinHoursBeforeAssignmentJoin,
                    ScheduleSummary = classInfo.ScheduleSummary,
                    CreatedAt = classInfo.CreatedAt,
                    UpdatedAt = classInfo.UpdatedAt,
                },
            });
        }

        _logger.LogInformation(
            "[GetClassEnrollmentsByProgramEnrollmentAsync] Retrieved {Count}/{Total} class enrollments.",
            dtos.Count,
            totalCount);

        return new Pagination<ClassEnrollmentResponseDto>(dtos, totalCount, page, pageSize);
    }
}
