using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Interfaces;
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

        var module = enrollment.Module;
        return MapToResponseDto(enrollment, module);
    }

    public async Task<List<ModuleEnrollmentResponseDto>> GetModuleEnrollmentsByProgramEnrollmentIdAsync(
        Guid programEnrollmentId)
    {
        ModuleEnrollmentValidator.ValidateProgramEnrollmentIdRequired(programEnrollmentId);

        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ModuleEnrollmentValidator.ViewListForbiddenMessage);

        var programEnrollmentEntity = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        var programEnrollment = ModuleEnrollmentValidator.ValidateProgramEnrollmentExists(
            programEnrollmentEntity,
            programEnrollmentId);

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            programEnrollment.StudentId,
            ModuleEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId && !me.IsDeleted,
            me => me.Module);

        var latestPerModule = moduleEnrollments
            .Where(me => me.Module != null && !me.Module.IsDeleted)
            .GroupBy(me => me.ModuleId)
            .Select(group => group.OrderByDescending(me => me.AttemptNumber).First())
            .OrderBy(me => me.Module!.ModuleOrder)
            .ToList();

        _logger.LogInformation(
            "[GetModuleEnrollmentsByProgramEnrollmentIdAsync] Retrieved {Count} module enrollments for program enrollment {ProgramEnrollmentId}.",
            latestPerModule.Count,
            programEnrollmentId);

        return latestPerModule
            .Select(enrollment => MapToResponseDto(enrollment, enrollment.Module!))
            .ToList();
    }

    private static ModuleEnrollmentResponseDto MapToResponseDto(ModuleEnrollment enrollment, Module module)
    {
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
}
