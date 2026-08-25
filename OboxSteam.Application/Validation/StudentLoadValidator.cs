using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Soft product caps on concurrent student load: programs in progress, primary
/// class seats, and at most one active retake (remedial) class seat.
/// </summary>
public static class StudentLoadValidator
{
    public const int MaxInProgressProgramsPerStudent =
        ProgramEnrollmentValidator.MaxInProgressProgramsPerStudent;

    public const int MaxPrimaryActiveClassesPerStudent =
        ClassEnrollmentValidator.MaxActiveClassesPerStudent;

    public const int MaxRetakeActiveClassesPerStudent = 1;

    public static Task ValidateUnderProgramLoadAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeEnrollmentId = null)
        => ProgramEnrollmentValidator.ValidateUnderInProgressProgramLimitAsync(
            unitOfWork,
            studentId,
            excludeEnrollmentId);

    /// <summary>
    /// Primary active class enrollments only (Retake seats are outside this cap).
    /// </summary>
    public static async Task ValidateUnderPrimaryClassLoadAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeEnrollmentId = null)
    {
        var active = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && !ce.IsDeleted
                  && ce.Status == ClassEnrollmentStatus.Active
                  && ce.Kind == ClassEnrollmentKind.Primary
                  && (!excludeEnrollmentId.HasValue || ce.Id != excludeEnrollmentId.Value));

        if (active.Count >= MaxPrimaryActiveClassesPerStudent)
        {
            throw ErrorHelper.Conflict(
                $"Student has reached the maximum of {MaxPrimaryActiveClassesPerStudent} active primary classes. " +
                "Leave or complete a class before joining another.");
        }
    }

    public static async Task ValidateUnderRetakeClassLoadAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeEnrollmentId = null)
    {
        var active = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && !ce.IsDeleted
                  && ce.Status == ClassEnrollmentStatus.Active
                  && ce.Kind == ClassEnrollmentKind.Retake
                  && (!excludeEnrollmentId.HasValue || ce.Id != excludeEnrollmentId.Value));

        if (active.Count >= MaxRetakeActiveClassesPerStudent)
        {
            throw ErrorHelper.Conflict(
                $"Student has reached the maximum of {MaxRetakeActiveClassesPerStudent} active retake class. " +
                "Complete or leave the current retake class before joining another.");
        }
    }
}
