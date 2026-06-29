using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private static readonly (string ClassCode, string[] StudentCodes)[] OpenClassEnrollmentPlan =
    [
        ("CLS-OPEN-001", ["STD-001", "STD-002", "STD-003", "STD-004", "STD-005"]),
        ("CLS-OPEN-002", ["STD-006", "STD-007", "STD-008", "STD-009", "STD-010"]),
        ("CLS-OPEN-003", ["STD-011", "STD-012", "STD-013", "STD-014", "STD-015"]),
        ("CLS-OPEN-004", ["STD-016", "STD-017", "STD-018", "STD-019", "STD-020"]),
        ("CLS-OPEN-005", ["STD-021", "STD-022", "STD-023", "STD-024"]),
    ];

    private static readonly string[] RoboticsProgramOnlyStudentCodes =
    [
        "STD-025",
    ];

    private async Task SeedClassEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed class enrollments for open classes");

        var existingEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => OpenClassCodes.Contains(ce.Class.Code) && !ce.IsDeleted,
            ce => ce.Class);

        if (existingEnrollment != null)
        {
            _loggerService.LogInformation("Open class enrollments already seeded, skipping");
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == OpenClassProgramCode);
        if (program == null)
        {
            _loggerService.LogWarning(
                "Program {ProgramCode} not found. Skipping open class enrollment seeding.",
                OpenClassProgramCode);
            return;
        }

        await EnsureRoboticsProgramEnrollmentsForOpenClassesAsync(program.Id);

        var seedTime = DateTime.UtcNow;
        var enrollmentsToAdd = new List<ClassEnrollment>();

        foreach (var plan in OpenClassEnrollmentPlan)
        {
            var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == plan.ClassCode);
            if (classEntity == null)
            {
                _loggerService.LogWarning("Class {ClassCode} not found. Skipping enrollments.", plan.ClassCode);
                continue;
            }

            foreach (var studentCode in plan.StudentCodes)
            {
                var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
                if (student == null)
                {
                    _loggerService.LogWarning(
                        "Student {StudentCode} not found for class {ClassCode}. Skipping.",
                        studentCode,
                        plan.ClassCode);
                    continue;
                }

                var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student.Id
                          && pe.ProgramId == program.Id
                          && !pe.IsDeleted);

                if (programEnrollment == null)
                {
                    _loggerService.LogWarning(
                        "Program enrollment not found for student {StudentCode}. Skipping.",
                        studentCode);
                    continue;
                }

                enrollmentsToAdd.Add(new ClassEnrollment
                {
                    Id = Guid.NewGuid(),
                    ClassId = classEntity.Id,
                    StudentId = student.Id,
                    ProgramEnrollmentId = programEnrollment.Id,
                    Status = ClassEnrollmentStatus.Active,
                    EnrolledAt = seedTime.AddDays(-3),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        if (enrollmentsToAdd.Count == 0)
        {
            _loggerService.LogWarning("No open class enrollments created.");
            return;
        }

        await _unitOfWork.ClassEnrollments.AddRangeAsync(enrollmentsToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed class enrollments — {Count} enrollment(s) created.",
            enrollmentsToAdd.Count);
    }

    private async Task EnsureRoboticsProgramEnrollmentsForOpenClassesAsync(Guid programId)
    {
        var requiredStudentCodes = OpenClassEnrollmentPlan
            .SelectMany(plan => plan.StudentCodes)
            .Concat(RoboticsProgramOnlyStudentCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seedTime = DateTime.UtcNow;
        var enrollmentsToAdd = new List<ProgramEnrollment>();

        foreach (var studentCode in requiredStudentCodes)
        {
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
            if (student == null)
            {
                continue;
            }

            var existingEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student.Id && pe.ProgramId == programId && !pe.IsDeleted);

            if (existingEnrollment != null)
            {
                continue;
            }

            enrollmentsToAdd.Add(new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = programId,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 0m,
                EnrolledAt = seedTime.AddDays(-7),
                StartedAt = seedTime.AddDays(-5),
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (enrollmentsToAdd.Count == 0)
        {
            return;
        }

        await _unitOfWork.ProgramEnrollments.AddRangeAsync(enrollmentsToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Backfilled {Count} robotics program enrollment(s) for open class seeding.",
            enrollmentsToAdd.Count);
    }
}
