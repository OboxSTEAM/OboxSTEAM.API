using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedProgramEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed program enrollments");
        var existingProgramEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync();
        if (existingProgramEnrollments.Any())
        {
            _loggerService.LogInformation("Program enrollments already exist, skipping seeding");
            return;
        }

        var students = (await _unitOfWork.Users.GetAllAsync(u => u.Role == RoleType.Student && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);
        var programs = (await _unitOfWork.Programs.GetAllAsync(p => !p.IsDeleted))
            .ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        if (!programs.TryGetValue("PRG-ROBOTICS", out var robotics)
            || !programs.TryGetValue("PRG-WEBDEV", out var webDev)
            || !programs.TryGetValue("PRG-IOT", out var iot)
            || !programs.TryGetValue("PRG-GAMEDEV", out var gameDev)
            || !programs.TryGetValue("PRG-AIBASIC", out var aiBasic))
        {
            _loggerService.LogWarning("Hero programs missing. Skipping program enrollment seeding.");
            return;
        }

        var programEnrollments = new List<ProgramEnrollment>();

        void Add(
            string studentCode,
            Program program,
            EnrollmentStatus status,
            decimal progressPercent,
            DateTime enrolledAt,
            DateTime? startedAt,
            DateTime? completedAt)
        {
            if (!students.TryGetValue(studentCode, out var student))
            {
                _loggerService.LogWarning("Student {StudentCode} not found. Skipping program enrollment.", studentCode);
                return;
            }

            programEnrollments.Add(new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = program.Id,
                Status = status,
                ProgressPercent = progressPercent,
                EnrolledAt = enrolledAt,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        var roboticsActiveEnrolledAt = AtDays(-49);
        var roboticsActiveStartedAt = AtDays(-42);
        foreach (var studentCode in RoboticsCurrentStudentCodes.Concat(RoboticsOpenStudentCodes))
        {
            var enrolledAt = RoboticsOpenStudentCodes.Contains(studentCode, StringComparer.OrdinalIgnoreCase)
                ? AtDays(-10)
                : roboticsActiveEnrolledAt;
            var startedAt = RoboticsOpenStudentCodes.Contains(studentCode, StringComparer.OrdinalIgnoreCase)
                ? AtDays(-8)
                : roboticsActiveStartedAt;
            Add(studentCode, robotics, EnrollmentStatus.Active, 35m, enrolledAt, startedAt, null);
        }

        foreach (var studentCode in RoboticsPastStudentCodes)
        {
            Add(
                studentCode,
                robotics,
                EnrollmentStatus.Completed,
                100m,
                AtMonths(-8),
                AtMonths(-8).AddDays(3),
                AtMonths(-3));
        }

        foreach (var studentCode in IotCurrentStudentCodes)
        {
            Add(studentCode, iot, EnrollmentStatus.Active, 40m, AtDays(-35), AtDays(-28), null);
        }

        foreach (var studentCode in WebDevPastStudentCodes)
        {
            Add(
                studentCode,
                webDev,
                EnrollmentStatus.Completed,
                100m,
                AtMonths(-5),
                AtMonths(-5).AddDays(4),
                AtDays(-32));
        }

        foreach (var studentCode in GameDevPendingStudentCodes)
        {
            Add(studentCode, gameDev, EnrollmentStatus.PendingPayment, 0m, AtDays(-12), null, null);
        }

        foreach (var studentCode in GameDevJustEnrolledStudentCodes)
        {
            Add(studentCode, gameDev, EnrollmentStatus.Active, 0m, AtDays(-3), null, null);
        }

        foreach (var studentCode in AiDroppedStudentCodes)
        {
            Add(
                studentCode,
                aiBasic,
                EnrollmentStatus.Dropped,
                15m,
                AtMonths(-5),
                AtMonths(-5).AddDays(2),
                null);
        }

        if (programEnrollments.Count == 0)
        {
            _loggerService.LogWarning("No program enrollments seeded.");
            return;
        }

        await _unitOfWork.ProgramEnrollments.AddRangeAsync(programEnrollments);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed program enrollments — {Count} record(s).",
            programEnrollments.Count);
    }
}
