using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Program enrollment business rules and input validation.
/// </summary>
public static class ProgramEnrollmentValidator
{
    public const string EnrollForbiddenMessage = "Only students can enroll in a program.";
    public const string ViewListForbiddenMessage = "You do not have permission to view program enrollments.";
    public const string ViewEnrollmentForbiddenMessage = "You do not have permission to view this enrollment.";

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

    public static void ValidateStudentExists(User? student, Guid studentId)
    {
        if (student == null || student.IsDeleted || student.Role != RoleType.Student)
        {
            throw ErrorHelper.NotFound($"Student with id '{studentId}' not found.");
        }
    }

    public static void ValidateCanListProgramEnrollments(RoleType role)
    {
        if (role is not (RoleType.Student or RoleType.Parent or RoleType.SuperAdmin or RoleType.Manager))
        {
            throw ErrorHelper.Forbidden(ViewListForbiddenMessage);
        }
    }
}
