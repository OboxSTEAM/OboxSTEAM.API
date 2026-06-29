using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private const string OpenClassProgramCode = "PRG-ROBOTICS";

    private static readonly string[] OpenClassCodes =
    [
        "CLS-OPEN-001",
        "CLS-OPEN-002",
        "CLS-OPEN-003",
        "CLS-OPEN-004",
        "CLS-OPEN-005",
    ];

    private static readonly (string Code, string Name, string MentorCode, int StartDaysOffset, int EndDaysOffset, string ScheduleSummary)[]
        OpenClassDefinitions =
        [
            ("CLS-OPEN-001", "Robotics Open Cohort 1", "MNT-001", 14, 98, "Every Monday 09:00-11:30"),
            ("CLS-OPEN-002", "Robotics Open Cohort 2", "MNT-002", 21, 105, "Every Tuesday 14:00-16:30"),
            ("CLS-OPEN-003", "Robotics Open Cohort 3", "MNT-003", 28, 112, "Every Wednesday 09:00-11:30"),
            ("CLS-OPEN-004", "Robotics Open Cohort 4", "MNT-004", 35, 119, "Every Thursday 14:00-16:30"),
            ("CLS-OPEN-005", "Robotics Open Cohort 5", "MNT-005", 42, 126, "Every Friday 09:00-11:30"),
        ];

    private async Task SeedClassesAsync()
    {
        _loggerService.LogInformation("Starting seed open classes");

        var existingClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => OpenClassCodes.Contains(c.Code) && !c.IsDeleted);

        if (existingClass != null)
        {
            _loggerService.LogInformation("Open classes already seeded, skipping");
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == OpenClassProgramCode);
        if (program == null)
        {
            _loggerService.LogWarning(
                "Program {ProgramCode} not found. Skipping open class seeding.",
                OpenClassProgramCode);
            return;
        }

        var seedTime = DateTime.UtcNow;
        var classesToAdd = new List<Class>();

        foreach (var definition in OpenClassDefinitions)
        {
            var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == definition.MentorCode);
            if (mentor == null)
            {
                _loggerService.LogWarning(
                    "Skipping class {ClassCode}: mentor {MentorCode} not found.",
                    definition.Code,
                    definition.MentorCode);
                continue;
            }

            classesToAdd.Add(new Class
            {
                Id = Guid.NewGuid(),
                Code = definition.Code,
                Name = definition.Name,
                ProgramId = program.Id,
                MentorId = mentor.Id,
                StartDate = seedTime.AddDays(definition.StartDaysOffset),
                EndDate = seedTime.AddDays(definition.EndDaysOffset),
                MaxCapacity = 5,
                Status = ClassStatus.Open,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = definition.ScheduleSummary,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (classesToAdd.Count == 0)
        {
            _loggerService.LogWarning("No open classes created.");
            return;
        }

        await _unitOfWork.Classes.AddRangeAsync(classesToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed open classes — {Count} class(es) created.",
            classesToAdd.Count);
    }
}
