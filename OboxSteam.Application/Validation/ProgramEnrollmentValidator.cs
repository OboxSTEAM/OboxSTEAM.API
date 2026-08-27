using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Program enrollment business rules and input validation.
/// </summary>
public static class ProgramEnrollmentValidator
{
    public const string EnrollForbiddenMessage = "Only students can enroll in a program.";
    public const string ViewListForbiddenMessage = "You do not have permission to view program enrollments.";
    public const string ViewEnrollmentForbiddenMessage = "You do not have permission to view this enrollment.";

    /// <summary>
    /// Soft product cap: Active + PendingPayment program enrollments per student.
    /// Completed/Dropped do not count. Creating PendingPayment is blocked when already at the cap.
    /// </summary>
    public const int MaxInProgressProgramsPerStudent = 2;

    public static void ValidateProgramIdRequired(Guid programId)
    {
        if (programId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ProgramId is required.");
        }
    }

    public static void ValidateStudentIdRequired(Guid studentId)
    {
        if (studentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("StudentId is required.");
        }
    }

    public static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1)
        {
            throw ErrorHelper.BadRequest("Invalid pagination parameters. Page and pageSize must be at least 1.");
        }
    }

    public static Program ValidateProgramExists(Program? program, Guid programId)
    {
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        return program;
    }

    public static void ValidateNotAlreadyEnrolled(ProgramEnrollment? existingEnrollment)
    {
        if (existingEnrollment != null)
        {
            throw ErrorHelper.Conflict("You are already enrolled in this program.");
        }
    }

    public static User ValidateStudentExists(User? student, Guid studentId)
    {
        if (student == null || student.IsDeleted || student.Role != RoleType.Student)
        {
            throw ErrorHelper.NotFound($"Student with id '{studentId}' not found.");
        }

        return student;
    }

    public static void ValidateCanListProgramEnrollments(RoleType role)
    {
        if (role is not (RoleType.Student or RoleType.Parent or RoleType.Admin or RoleType.Manager))
        {
            throw ErrorHelper.Forbidden(ViewListForbiddenMessage);
        }
    }

    /// <summary>
    /// Only Active programs accept new registration / checkout.
    /// Draft and Inactive are blocked (FE: Bản nháp / Ngừng hoạt động).
    /// </summary>
    public static void EnsureProgramPurchasable(Program program)
    {
        if (program.Status == ProgramStatus.Active)
            return;

        throw ErrorHelper.BadRequest(
            program.Status == ProgramStatus.Draft
                ? $"Program '{program.Code}' is a draft and cannot be purchased."
                : $"Program '{program.Code}' is inactive and is not accepting registrations.");
    }

    /// <summary>
    /// Ensures the student is under the in-progress program cap (Active + PendingPayment).
    /// </summary>
    public static async Task ValidateUnderInProgressProgramLimitAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeEnrollmentId = null)
    {
        var inProgress = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == studentId
                  && !pe.IsDeleted
                  && (pe.Status == EnrollmentStatus.Active
                      || pe.Status == EnrollmentStatus.PendingPayment)
                  && (!excludeEnrollmentId.HasValue || pe.Id != excludeEnrollmentId.Value));

        if (inProgress.Count >= MaxInProgressProgramsPerStudent)
        {
            throw ErrorHelper.Conflict(
                $"Student has reached the maximum of {MaxInProgressProgramsPerStudent} " +
                "in-progress programs (Active or PendingPayment). " +
                "Complete or drop a program before starting another.");
        }
    }

    /// <summary>
    /// Program tuition checkout requires at least one Standard class that is Open
    /// and has remaining seats. Soft preference / preview does not hold seats.
    /// </summary>
    public static async Task EnsureProgramHasOpenClassWithCapacityAsync(
        IUnitOfWork unitOfWork,
        Guid programId)
    {
        var openClasses = await unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == programId
                 && c.Status == ClassStatus.Open
                 && c.Kind == ClassKind.Standard
                 && !c.IsDeleted);

        foreach (var openClass in openClasses)
        {
            var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(
                unitOfWork,
                openClass.Id);
            if (seatsTaken < openClass.MaxCapacity)
            {
                return;
            }
        }

        throw ErrorHelper.BadRequest(
            "This program has no open classes with available seats. " +
            "Checkout is blocked until a recruiting class has capacity.");
    }
}
