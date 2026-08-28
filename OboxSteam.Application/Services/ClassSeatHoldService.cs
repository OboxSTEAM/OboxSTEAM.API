using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassSeatHoldService : IClassSeatHoldService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IProgramEnrollmentService _programEnrollmentService;
    private readonly ISyncEventPublisher _syncEventPublisher;
    private readonly IClassService _classService;
    private readonly ILogger<ClassSeatHoldService> _logger;

    public ClassSeatHoldService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IProgramEnrollmentService programEnrollmentService,
        ISyncEventPublisher syncEventPublisher,
        IClassService classService,
        ILogger<ClassSeatHoldService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _programEnrollmentService = programEnrollmentService;
        _syncEventPublisher = syncEventPublisher;
        _classService = classService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<(Guid ClassId, Guid ProgramId)>> ReleaseExpiredHoldsAsync(
        CancellationToken cancellationToken = default)
        => await ClassSeatHoldHelper.ReleaseExpiredHoldsAsync(_unitOfWork, cancellationToken);

    public async Task<SelectProgramClassResponseDto> SelectClassForCheckoutAsync(
        Guid programId,
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        ClassEnrollmentValidator.ValidateClassIdRequired(classId);
        ProgramEnrollmentValidator.ValidateProgramIdRequired(programId);

        var studentId = _claimsService.GetCurrentUserId;
        var student = await _unitOfWork.Users.GetByIdAsync(studentId)
            ?? throw ErrorHelper.NotFound("Student not found.");

        if (student.Role != RoleType.Student)
        {
            throw ErrorHelper.Forbidden("Only students can select a class for checkout.");
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(programId)
            ?? throw ErrorHelper.NotFound($"Program '{programId}' not found.");

        if (program.Price == null || program.Price <= 0)
        {
            throw ErrorHelper.BadRequest("This program cannot be purchased because it has no valid price.");
        }

        ProgramEnrollmentValidator.EnsureProgramPurchasable(program);

        await ReleaseExpiredHoldsAsync(cancellationToken);

        var enrollment = await _programEnrollmentService.GetOrCreatePendingEnrollmentAsync(studentId, programId);
        var (hold, affectedClassIds) = await CreateOrRefreshHoldAsync(
            studentId,
            enrollment,
            classId,
            cancellationToken);

        foreach (var affectedClassId in affectedClassIds.Distinct())
        {
            await PublishSeatsChangedAsync(programId, affectedClassId, cancellationToken);
        }

        return new SelectProgramClassResponseDto
        {
            ProgramEnrollmentId = enrollment.Id,
            ClassId = hold.ClassId,
            HoldExpiresAt = AppDateTime.ToUtcOffset(hold.HoldExpiresAt!.Value),
        };
    }

    public async Task<ClassEnrollment> RequireValidHoldAsync(
        Guid studentId,
        ProgramEnrollment programEnrollment,
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        ClassEnrollmentValidator.ValidateClassIdRequired(classId);
        await ReleaseExpiredHoldsAsync(cancellationToken);

        var hold = await ClassEnrollmentValidator.GetValidSeatHoldAsync(_unitOfWork, programEnrollment.Id);
        if (hold == null || hold.ClassId != classId)
        {
            throw ErrorHelper.BadRequest(
                "Select this class before checkout or your seat hold has expired.");
        }

        if (hold.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("This class seat hold does not belong to you.");
        }

        return hold;
    }

    public async Task<(ClassEnrollment Hold, IReadOnlyList<Guid> AffectedClassIds)> CreateOrRefreshHoldAsync(
        Guid studentId,
        ProgramEnrollment programEnrollment,
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        ClassEnrollmentValidator.ValidateClassIdRequired(classId);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
        var classToHold = ClassEnrollmentValidator.ValidateClassExists(classEntity, classId);
        ClassEnrollmentValidator.ValidateClassBelongsToProgram(classToHold, programEnrollment.ProgramId);
        ClassEnrollmentValidator.ValidateClassOpenForEnrollment(classToHold);

        if (classToHold.Kind != ClassKind.Standard)
        {
            throw ErrorHelper.BadRequest("Only standard open classes can be selected for program checkout.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(ProgramCheckoutPolicy.CheckoutWindowMinutes);
        var affectedClassIds = new List<Guid>();

        var activeEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == programEnrollment.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
        ClassEnrollmentValidator.ValidateNoActiveClassEnrollmentForProgram(activeEnrollment);

        var existingHold = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == programEnrollment.Id
                  && ce.Status == ClassEnrollmentStatus.Pending
                  && !ce.IsDeleted);

        if (existingHold != null)
        {
            if (existingHold.ClassId == classId
                && ClassEnrollmentValidator.OccupiesSeat(existingHold, now))
            {
                existingHold.HoldExpiresAt = expiresAt;
                await _unitOfWork.ClassEnrollments.Update(existingHold);
                await _unitOfWork.SaveChangesAsync();

                return (existingHold, affectedClassIds);
            }

            affectedClassIds.Add(existingHold.ClassId);
            await WithdrawHoldAsync(existingHold);
            await _unitOfWork.SaveChangesAsync();
        }

        await StudentLoadValidator.ValidateUnderPrimaryClassLoadAsync(_unitOfWork, studentId);

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(
            _unitOfWork,
            classId,
            classToHold.MaxCapacity);
        await ClassEnrollmentValidator.ValidateLateJoinAllowedAsync(_unitOfWork, classToHold);
        await ScheduleConflictValidator.ValidateStudentCanJoinClassAsync(
            _unitOfWork,
            studentId,
            classId);

        var reusableEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ClassId == classId
                  && ce.StudentId == studentId
                  && !ce.IsDeleted);

        if (reusableEnrollment != null)
        {
            if (reusableEnrollment.Status == ClassEnrollmentStatus.Active)
            {
                throw ErrorHelper.Conflict("Student is already enrolled in this class.");
            }

            reusableEnrollment.ProgramEnrollmentId = programEnrollment.Id;
            reusableEnrollment.Kind = ClassEnrollmentKind.Primary;
            reusableEnrollment.Status = ClassEnrollmentStatus.Pending;
            reusableEnrollment.HoldExpiresAt = expiresAt;
            reusableEnrollment.EnrolledAt = null;

            await _unitOfWork.ClassEnrollments.Update(reusableEnrollment);
            await _unitOfWork.SaveChangesAsync();
            affectedClassIds.Add(classId);

            _logger.LogInformation(
                "[CreateOrRefreshHoldAsync] Student {StudentId} re-held class {ClassId} until {ExpiresAt}.",
                studentId,
                classId,
                expiresAt);

            return (reusableEnrollment, affectedClassIds);
        }

        var hold = new ClassEnrollment
        {
            ClassId = classId,
            StudentId = studentId,
            ProgramEnrollmentId = programEnrollment.Id,
            Kind = ClassEnrollmentKind.Primary,
            Status = ClassEnrollmentStatus.Pending,
            HoldExpiresAt = expiresAt,
        };

        await _unitOfWork.ClassEnrollments.AddAsync(hold);
        await _unitOfWork.SaveChangesAsync();
        affectedClassIds.Add(classId);

        _logger.LogInformation(
            "[CreateOrRefreshHoldAsync] Student {StudentId} held class {ClassId} until {ExpiresAt}.",
            studentId,
            classId,
            expiresAt);

        return (hold, affectedClassIds);
    }

    public async Task<(Guid? ClassId, Guid? ProgramId)> WithdrawHoldForProgramEnrollmentAsync(
        Guid programEnrollmentId,
        CancellationToken cancellationToken = default)
    {
        var hold = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == programEnrollmentId
                  && ce.Status == ClassEnrollmentStatus.Pending
                  && !ce.IsDeleted);

        if (hold == null)
        {
            return (null, null);
        }

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(hold.ClassId);
        await WithdrawHoldAsync(hold);
        await _unitOfWork.SaveChangesAsync();

        return classEntity == null || classEntity.IsDeleted
            ? (hold.ClassId, null)
            : (hold.ClassId, classEntity.ProgramId);
    }

    public async Task ActivateHoldAfterPaymentAsync(
        Guid programEnrollmentId,
        CancellationToken cancellationToken = default)
    {
        var hold = await ClassEnrollmentValidator.GetValidSeatHoldAsync(_unitOfWork, programEnrollmentId)
            ?? throw ErrorHelper.BadRequest(
                "The class seat hold has expired. Select a class again before completing payment.");

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(hold.ClassId);
        if (classEntity == null || classEntity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Class '{hold.ClassId}' not found.");
        }

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(
            _unitOfWork,
            hold.ClassId,
            classEntity.MaxCapacity);

        var now = DateTime.UtcNow;
        hold.Status = ClassEnrollmentStatus.Active;
        hold.EnrolledAt = now;
        hold.HoldExpiresAt = null;

        await _unitOfWork.ClassEnrollments.Update(hold);
        await _unitOfWork.SaveChangesAsync();

        await _classService.TryAutoStartClassIfReadyAsync(hold.ClassId);

        await PublishSeatsChangedAsync(classEntity.ProgramId, hold.ClassId, cancellationToken);

        _logger.LogInformation(
            "[ActivateHoldAfterPaymentAsync] Activated class enrollment {EnrollmentId} for program enrollment {ProgramEnrollmentId}.",
            hold.Id,
            programEnrollmentId);
    }

    public async Task PublishSeatsChangedAsync(
        Guid programId,
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        await _syncEventPublisher.PublishAsync(
            SyncScopes.SeatsChanged,
            NotificationAudience.ForProgramBrowsers(programId),
            entityType: "Class",
            entityId: classId,
            cancellationToken);

        await _syncEventPublisher.PublishAsync(
            SyncScopes.SeatsChanged,
            NotificationAudience.ForManagers(),
            entityType: "Class",
            entityId: classId,
            cancellationToken);
    }

    public async Task ReleaseClassHoldForCheckoutAsync(
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        ProgramEnrollmentValidator.ValidateProgramIdRequired(programId);

        var studentId = _claimsService.GetCurrentUserId;
        var student = await _unitOfWork.Users.GetByIdAsync(studentId)
            ?? throw ErrorHelper.NotFound("Student not found.");

        if (student.Role != RoleType.Student)
        {
            throw ErrorHelper.Forbidden("Only students can release a checkout seat hold.");
        }

        var enrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == studentId
                  && pe.ProgramId == programId
                  && pe.Status == EnrollmentStatus.PendingPayment
                  && !pe.IsDeleted);

        if (enrollment == null)
        {
            _logger.LogInformation(
                "[ReleaseClassHoldForCheckoutAsync] No pending checkout enrollment for student {StudentId} on program {ProgramId}.",
                studentId,
                programId);
            return;
        }

        var result = await PendingProgramCheckoutHelper.AbandonPendingProgramCheckoutAsync(
            _unitOfWork,
            enrollment.Id,
            cancellationToken: cancellationToken);

        if (result.Abandoned && result.ClassId.HasValue)
        {
            await PublishSeatsChangedAsync(programId, result.ClassId.Value, cancellationToken);
        }

        _logger.LogInformation(
            "[ReleaseClassHoldForCheckoutAsync] Student {StudentId} released checkout hold for program {ProgramId}. Abandoned={Abandoned}.",
            studentId,
            programId,
            result.Abandoned);
    }

    private async Task WithdrawHoldAsync(ClassEnrollment hold)
    {
        hold.Status = ClassEnrollmentStatus.Withdrawn;
        hold.HoldExpiresAt = null;
        await _unitOfWork.ClassEnrollments.Update(hold);
    }
}
