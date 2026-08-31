using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedClassEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed academic-year class enrollments");

        var existing = await _unitOfWork.ClassEnrollments.GetAllAsync(ce => !ce.IsDeleted);
        if (existing.Count > 0)
        {
            _loggerService.LogInformation("Class enrollments already exist, skipping");
            return;
        }

        var students = (await _unitOfWork.Users.GetAllAsync(u => u.Role == RoleType.Student && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);
        var classes = (await _unitOfWork.Classes.GetAllAsync(c => !c.IsDeleted))
            .ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);
        var programEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(pe => !pe.IsDeleted);

        var enrollmentsToAdd = new List<ClassEnrollment>();

        foreach (var plan in AcademicYearClassEnrollmentPlan)
        {
            if (!classes.TryGetValue(plan.ClassCode, out var classEntity))
            {
                _loggerService.LogWarning("Class {ClassCode} not found. Skipping enrollments.", plan.ClassCode);
                continue;
            }

            foreach (var studentCode in plan.StudentCodes)
            {
                if (!students.TryGetValue(studentCode, out var student))
                {
                    _loggerService.LogWarning(
                        "Student {StudentCode} not found for class {ClassCode}. Skipping.",
                        studentCode,
                        plan.ClassCode);
                    continue;
                }

                var programEnrollment = programEnrollments
                    .Where(pe => pe.StudentId == student.Id
                                 && pe.ProgramId == classEntity.ProgramId
                                 && !pe.IsDeleted)
                    .OrderByDescending(pe =>
                        pe.Status is EnrollmentStatus.Active or EnrollmentStatus.PendingPayment)
                    .ThenByDescending(pe => pe.EnrolledAt)
                    .FirstOrDefault();
                if (programEnrollment == null)
                {
                    _loggerService.LogWarning(
                        "Program enrollment not found for student {StudentCode} on class {ClassCode}. Skipping.",
                        studentCode,
                        plan.ClassCode);
                    continue;
                }

                var enrolledAt = programEnrollment.EnrolledAt ?? classEntity.StartDate;
                enrollmentsToAdd.Add(new ClassEnrollment
                {
                    Id = Guid.NewGuid(),
                    ClassId = classEntity.Id,
                    StudentId = student.Id,
                    ProgramEnrollmentId = programEnrollment.Id,
                    Kind = ClassEnrollmentKind.Primary,
                    Status = plan.Status,
                    HoldExpiresAt = plan.Status == ClassEnrollmentStatus.Pending
                        ? _seedNow.AddHours(24)
                        : null,
                    EnrolledAt = enrolledAt,
                    CreatedAt = enrolledAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        if (enrollmentsToAdd.Count == 0)
        {
            _loggerService.LogWarning("No class enrollments created.");
            return;
        }

        await _unitOfWork.ClassEnrollments.AddRangeAsync(enrollmentsToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed class enrollments — {Count} enrollment(s) created.",
            enrollmentsToAdd.Count);
    }
}
