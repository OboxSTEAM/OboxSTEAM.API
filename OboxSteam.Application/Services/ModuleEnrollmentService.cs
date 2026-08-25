using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
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
    private readonly INotificationPublisher _notificationPublisher;

    public ModuleEnrollmentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ModuleEnrollmentService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
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
        };
    }
}
