using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ModuleEnrollmentService : IModuleEnrollmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ModuleEnrollmentService> _logger;

    public ModuleEnrollmentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ModuleEnrollmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<ModuleEnrollmentResponseDto> EnrollModuleAsync(CreateModuleEnrollmentRequestDto request)
    {
        ModuleEnrollmentValidator.ValidateProgramEnrollmentIdRequired(request.ProgramEnrollmentId);
        ModuleEnrollmentValidator.ValidateModuleIdRequired(request.ModuleId);

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ModuleEnrollmentValidator.EnrollForbiddenMessage);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(request.ProgramEnrollmentId);
        var programEnrollment = ModuleEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            request.ProgramEnrollmentId);
        ModuleEnrollmentValidator.ValidateProgramEnrollmentBelongsToStudent(programEnrollment, student.Id);
        ModuleEnrollmentValidator.ValidateProgramEnrollmentActiveForEnroll(programEnrollment);

        var moduleEntity = await _unitOfWork.Modules.GetByIdAsync(request.ModuleId);
        var module = ModuleEnrollmentValidator.ValidateModuleExists(moduleEntity, request.ModuleId);
        ModuleEnrollmentValidator.ValidateModuleBelongsToProgram(module, programEnrollment.ProgramId);

        await ModuleEnrollmentValidator.ValidatePrerequisiteCompletedAsync(_unitOfWork, student.Id, module);

        var activeEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == request.ModuleId
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted);

        ModuleEnrollmentValidator.ValidateNoActiveEnrollment(activeEnrollment);

        var now = DateTime.UtcNow;
        var enrollment = new ModuleEnrollment
        {
            StudentId = student.Id,
            ModuleId = request.ModuleId,
            ProgramEnrollmentId = request.ProgramEnrollmentId,
            Status = EnrollmentStatus.Active,
            ProgressPercent = 0m,
            AttemptNumber = 1,
            AssignmentFailureCount = 0,
            EnrolledAt = now,
        };

        await _unitOfWork.ModuleEnrollments.AddAsync(enrollment);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[EnrollModuleAsync] Student {StudentId} enrolled in module {ModuleId}, enrollment {EnrollmentId}.",
            student.Id,
            request.ModuleId,
            enrollment.Id);

        var programEnrollmentId = ModuleEnrollmentValidator.ValidateProgramEnrollmentLink(enrollment.ProgramEnrollmentId);

        return new ModuleEnrollmentResponseDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            ModuleId = enrollment.ModuleId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = enrollment.Status,
            ProgressPercent = enrollment.ProgressPercent,
            FinalGrade = enrollment.FinalGrade,
            AttemptNumber = enrollment.AttemptNumber,
            AssignmentFailureCount = enrollment.AssignmentFailureCount,
            EnrolledAt = enrollment.EnrolledAt,
            StartedAt = enrollment.StartedAt,
            CompletedAt = enrollment.CompletedAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Code = module.Code,
            ProgramId = module.ProgramId,
            Name = module.Name,
            ModuleType = module.ModuleType,
            ModuleOrder = module.ModuleOrder,
            PrerequisiteModuleId = module.PrerequisiteModuleId,
            IsMandatory = module.IsMandatory,
            Price = module.Price,
            RetakeFee = module.RetakeFee,
        };
    }

    public async Task<ModuleEnrollmentResponseDto> RetakeModuleAsync(UpdateModuleEnrollmentRequestDto request)
    {
        ModuleEnrollmentValidator.ValidateProgramEnrollmentIdRequired(request.ProgramEnrollmentId);
        ModuleEnrollmentValidator.ValidateModuleIdRequired(request.ModuleId);

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ModuleEnrollmentValidator.EnrollForbiddenMessage);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(request.ProgramEnrollmentId);
        var programEnrollment = ModuleEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            request.ProgramEnrollmentId);
        ModuleEnrollmentValidator.ValidateProgramEnrollmentBelongsToStudent(programEnrollment, student.Id);
        ModuleEnrollmentValidator.ValidateProgramEnrollmentActiveForRetake(programEnrollment);

        var moduleEntity = await _unitOfWork.Modules.GetByIdAsync(request.ModuleId);
        var module = ModuleEnrollmentValidator.ValidateModuleExists(moduleEntity, request.ModuleId);
        ModuleEnrollmentValidator.ValidateModuleBelongsToProgram(module, programEnrollment.ProgramId);

        var activeEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == request.ModuleId
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted);

        ModuleEnrollmentValidator.ValidateNoActiveEnrollment(activeEnrollment);

        var moduleAttempts = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == request.ModuleId
                  && me.ProgramEnrollmentId == request.ProgramEnrollmentId
                  && !me.IsDeleted);

        var pendingEnrollment = moduleAttempts
            .FirstOrDefault(me => me.Status == EnrollmentStatus.PendingPayment);

        if (pendingEnrollment != null)
        {
            _logger.LogInformation(
                "[RetakeModuleAsync] Reusing existing PendingPayment enrollment {EnrollmentId} for student {StudentId} on module {ModuleId}.",
                pendingEnrollment.Id,
                student.Id,
                request.ModuleId);

            return new ModuleEnrollmentResponseDto
            {
                Id = pendingEnrollment.Id,
                StudentId = pendingEnrollment.StudentId,
                ModuleId = pendingEnrollment.ModuleId,
                ProgramEnrollmentId = ModuleEnrollmentValidator.ValidateProgramEnrollmentLink(pendingEnrollment.ProgramEnrollmentId),
                Status = pendingEnrollment.Status,
                ProgressPercent = pendingEnrollment.ProgressPercent,
                FinalGrade = pendingEnrollment.FinalGrade,
                AttemptNumber = pendingEnrollment.AttemptNumber,
                AssignmentFailureCount = pendingEnrollment.AssignmentFailureCount,
                EnrolledAt = pendingEnrollment.EnrolledAt,
                StartedAt = pendingEnrollment.StartedAt,
                CompletedAt = pendingEnrollment.CompletedAt,
                CreatedAt = pendingEnrollment.CreatedAt,
                UpdatedAt = pendingEnrollment.UpdatedAt,
                Code = module.Code,
                ProgramId = module.ProgramId,
                Name = module.Name,
                ModuleType = module.ModuleType,
                ModuleOrder = module.ModuleOrder,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
                Price = module.Price,
                RetakeFee = module.RetakeFee,
            };
        }

        var failedEnrollment = moduleAttempts
            .Where(me => me.Status == EnrollmentStatus.Failed)
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();

        var previousFailedAttempt = ModuleEnrollmentValidator.ValidateRetakeEligibility(failedEnrollment);

        var nextAttemptNumber = previousFailedAttempt.AttemptNumber + 1;
        var now = DateTime.UtcNow;

        var newEnrollment = new ModuleEnrollment
        {
            StudentId = student.Id,
            ModuleId = request.ModuleId,
            ProgramEnrollmentId = request.ProgramEnrollmentId,
            Status = EnrollmentStatus.PendingPayment,
            ProgressPercent = 0m,
            AttemptNumber = nextAttemptNumber,
            AssignmentFailureCount = 0,
            EnrolledAt = now,
        };

        await _unitOfWork.ModuleEnrollments.AddAsync(newEnrollment);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[RetakeModuleAsync] Student {StudentId} retaking module {ModuleId}, attempt {AttemptNumber}, enrollment {EnrollmentId}.",
            student.Id,
            request.ModuleId,
            nextAttemptNumber,
            newEnrollment.Id);

        var programEnrollmentId = ModuleEnrollmentValidator.ValidateProgramEnrollmentLink(newEnrollment.ProgramEnrollmentId);

        return new ModuleEnrollmentResponseDto
        {
            Id = newEnrollment.Id,
            StudentId = newEnrollment.StudentId,
            ModuleId = newEnrollment.ModuleId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = newEnrollment.Status,
            ProgressPercent = newEnrollment.ProgressPercent,
            FinalGrade = newEnrollment.FinalGrade,
            AttemptNumber = newEnrollment.AttemptNumber,
            AssignmentFailureCount = newEnrollment.AssignmentFailureCount,
            EnrolledAt = newEnrollment.EnrolledAt,
            StartedAt = newEnrollment.StartedAt,
            CompletedAt = newEnrollment.CompletedAt,
            CreatedAt = newEnrollment.CreatedAt,
            UpdatedAt = newEnrollment.UpdatedAt,
            Code = module.Code,
            ProgramId = module.ProgramId,
            Name = module.Name,
            ModuleType = module.ModuleType,
            ModuleOrder = module.ModuleOrder,
            PrerequisiteModuleId = module.PrerequisiteModuleId,
            IsMandatory = module.IsMandatory,
            Price = module.Price,
            RetakeFee = module.RetakeFee,
        };
    }

    public async Task<ModuleEnrollmentResponseDto> GetModuleEnrollmentByIdAsync(Guid id)
    {
        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ModuleEnrollmentValidator.ViewListForbiddenMessage);

        var enrollmentEntity = await _unitOfWork.ModuleEnrollments.GetByIdAsync(id, me => me.Module);
        if (enrollmentEntity == null || enrollmentEntity.IsDeleted)
        {
            _logger.LogWarning("[GetModuleEnrollmentByIdAsync] Enrollment {Id} not found.", id);
        }

        var enrollment = ModuleEnrollmentValidator.ValidateModuleEnrollmentExists(enrollmentEntity, id);

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            ModuleEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var programEnrollmentId = ModuleEnrollmentValidator.ValidateProgramEnrollmentLink(enrollment.ProgramEnrollmentId);

        var module = enrollment.Module;
        return new ModuleEnrollmentResponseDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            ModuleId = enrollment.ModuleId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = enrollment.Status,
            ProgressPercent = enrollment.ProgressPercent,
            FinalGrade = enrollment.FinalGrade,
            AttemptNumber = enrollment.AttemptNumber,
            AssignmentFailureCount = enrollment.AssignmentFailureCount,
            EnrolledAt = enrollment.EnrolledAt,
            StartedAt = enrollment.StartedAt,
            CompletedAt = enrollment.CompletedAt,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Code = module.Code,
            ProgramId = module.ProgramId,
            Name = module.Name,
            ModuleType = module.ModuleType,
            ModuleOrder = module.ModuleOrder,
            PrerequisiteModuleId = module.PrerequisiteModuleId,
            IsMandatory = module.IsMandatory,
            Price = module.Price,
            RetakeFee = module.RetakeFee,
        };
    }

    public async Task<Pagination<ModuleEnrollmentResponseDto>> GetModuleEnrollmentsByProgramEnrollmentAsync(
        Guid programEnrollmentId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize)
    {
        _logger.LogInformation(
            "[GetModuleEnrollmentsByProgramEnrollmentAsync] Start — programEnrollmentId: {ProgramEnrollmentId}, page: {Page}, pageSize: {PageSize}",
            programEnrollmentId,
            page,
            pageSize);

        ProgramEnrollmentValidator.ValidatePagination(page, pageSize);
        ModuleEnrollmentValidator.ValidateProgramEnrollmentIdRequired(programEnrollmentId);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        var programEnrollment = ModuleEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            programEnrollmentId);

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            programEnrollment.StudentId,
            ModuleEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var query = _unitOfWork.ModuleEnrollments
            .GetQueryable()
            .Where(me => me.ProgramEnrollmentId == programEnrollmentId && !me.IsDeleted);

        query = sortBy?.ToLower() switch
        {
            "attemptnumber" => isDescending
                ? query.OrderByDescending(me => me.AttemptNumber)
                : query.OrderBy(me => me.AttemptNumber),
            "progresspercent" => isDescending
                ? query.OrderByDescending(me => me.ProgressPercent)
                : query.OrderBy(me => me.ProgressPercent),
            "status" => isDescending
                ? query.OrderByDescending(me => me.Status)
                : query.OrderBy(me => me.Status),
            "enrolledat" => isDescending
                ? query.OrderByDescending(me => me.EnrolledAt)
                : query.OrderBy(me => me.EnrolledAt),
            "createdat" => isDescending
                ? query.OrderByDescending(me => me.CreatedAt)
                : query.OrderBy(me => me.CreatedAt),
            "moduleorder" or _ => isDescending
                ? query.OrderByDescending(me => me.Module.ModuleOrder).ThenByDescending(me => me.AttemptNumber)
                : query.OrderBy(me => me.Module.ModuleOrder).ThenBy(me => me.AttemptNumber),
        };

        var totalCount = query.Count();

        var enrollments = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var moduleIds = enrollments.Select(me => me.ModuleId).Distinct().ToList();
        var modules = await _unitOfWork.Modules.GetAllAsync(m => moduleIds.Contains(m.Id) && !m.IsDeleted);
        var modulesById = modules.ToDictionary(m => m.Id);

        var dtos = new List<ModuleEnrollmentResponseDto>();
        foreach (var enrollment in enrollments)
        {
            modulesById.TryGetValue(enrollment.ModuleId, out var moduleEntity);
            var module = ModuleEnrollmentValidator.ValidateModuleExists(moduleEntity, enrollment.ModuleId);
            var linkedProgramEnrollmentId = ModuleEnrollmentValidator.ValidateProgramEnrollmentLink(
                enrollment.ProgramEnrollmentId);

            dtos.Add(new ModuleEnrollmentResponseDto
            {
                Id = enrollment.Id,
                StudentId = enrollment.StudentId,
                ModuleId = enrollment.ModuleId,
                ProgramEnrollmentId = linkedProgramEnrollmentId,
                Status = enrollment.Status,
                ProgressPercent = enrollment.ProgressPercent,
                FinalGrade = enrollment.FinalGrade,
                AttemptNumber = enrollment.AttemptNumber,
                AssignmentFailureCount = enrollment.AssignmentFailureCount,
                EnrolledAt = enrollment.EnrolledAt,
                StartedAt = enrollment.StartedAt,
                CompletedAt = enrollment.CompletedAt,
                CreatedAt = enrollment.CreatedAt,
                UpdatedAt = enrollment.UpdatedAt,
                Code = module.Code,
                ProgramId = module.ProgramId,
                Name = module.Name,
                ModuleType = module.ModuleType,
                ModuleOrder = module.ModuleOrder,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
                Price = module.Price,
                RetakeFee = module.RetakeFee,
            });
        }

        _logger.LogInformation(
            "[GetModuleEnrollmentsByProgramEnrollmentAsync] Retrieved {Count}/{Total} module enrollments.",
            dtos.Count,
            totalCount);

        return new Pagination<ModuleEnrollmentResponseDto>(dtos, totalCount, page, pageSize);
    }
}
