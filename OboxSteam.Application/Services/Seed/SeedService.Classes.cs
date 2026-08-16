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

    private const string CertificateTestProgramCode = "PRG-CERT-TEST";
    private const string CertificateTestClassCode = "CLS-CERT-TEST-01";

    private static readonly (string ProgramCode, string[] SkillCodes)[] SeedClassSkillPlan =
    [
        ("PRG-ROBOTICS", ["SKL-TECH-ROBOTICS-IOT", "SKL-TECH-PROG-PYTHON"]),
        ("PRG-WEBDEV", ["SKL-TECH-PROG-JS", "SKL-ART-UXUI"]),
        ("PRG-IOT", ["SKL-TECH-ROBOTICS-IOT", "SKL-ENG-SYSTEMS"]),
        ("PRG-GAMEDEV", ["SKL-TECH-SOFTWARE", "SKL-ART-VISUAL"]),
        ("PRG-AIBASIC", ["SKL-TECH-PROG-PYTHON", "SKL-MATH-STATS"]),
        ("PRG-3DDESIGN", ["SKL-TECH-SOFTWARE", "SKL-ART-VISUAL"]),
        ("PRG-CERT-TEST", ["SKL-TECH-DIGITAL-LIT"]),
    ];

    private async Task SeedClassesAsync()
    {
        _loggerService.LogInformation("Starting seed open classes");

        var existingClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => OpenClassCodes.Contains(c.Code) && !c.IsDeleted);

        if (existingClass != null)
        {
            _loggerService.LogInformation("Open classes already seeded, skipping");
        }
        else
        {
            var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == OpenClassProgramCode);
            if (program == null)
            {
                _loggerService.LogWarning(
                    "Program {ProgramCode} not found. Skipping open class seeding.",
                    OpenClassProgramCode);
            }
            else
            {
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
                }
                else
                {
                    await _unitOfWork.Classes.AddRangeAsync(classesToAdd);
                    await _unitOfWork.SaveChangesAsync();

                    _loggerService.LogInformation(
                        "Finished seed open classes — {Count} class(es) created.",
                        classesToAdd.Count);
                }
            }
        }

        await SeedCertificateTestClassAsync();
        await EnsureMissingProgramClassesForEnrollmentsAsync();
    }

    private async Task SeedCertificateTestClassAsync()
    {
        var existing = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == CertificateTestClassCode && !c.IsDeleted);
        if (existing != null)
        {
            _loggerService.LogInformation("Certificate test class already seeded, skipping");
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == CertificateTestProgramCode);
        if (program == null)
        {
            _loggerService.LogWarning(
                "Program {ProgramCode} not found. Skipping certificate test class seeding.",
                CertificateTestProgramCode);
            return;
        }

        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        if (mentor == null)
        {
            _loggerService.LogWarning("Mentor MNT-001 not found. Skipping certificate test class seeding.");
            return;
        }

        var seedTime = DateTime.UtcNow;
        await _unitOfWork.Classes.AddAsync(new Class
        {
            Id = Guid.NewGuid(),
            Code = CertificateTestClassCode,
            Name = "Certificate Test Cohort",
            ProgramId = program.Id,
            MentorId = mentor.Id,
            StartDate = seedTime.AddDays(7),
            EndDate = seedTime.AddDays(28),
            MaxCapacity = 5,
            Status = ClassStatus.Open,
            MinHoursBeforeAssignmentJoin = 24,
            ScheduleSummary = "Self-paced reading cohort",
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed certificate test class — {ClassCode}.",
            CertificateTestClassCode);
    }

    /// <summary>
    /// For each program that has Active/Completed enrollments but no class yet,
    /// create an Open <c>CLS-SEED-*</c> cohort so students can be class-enrolled.
    /// </summary>
    private async Task EnsureMissingProgramClassesForEnrollmentsAsync()
    {
        _loggerService.LogInformation("Ensuring Open classes exist for Active/Completed program enrollments");

        var programEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted
                  && (pe.Status == EnrollmentStatus.Active || pe.Status == EnrollmentStatus.Completed));

        if (programEnrollments.Count == 0)
        {
            return;
        }

        var programIdsNeedingCoverage = programEnrollments
            .Select(pe => pe.ProgramId)
            .Distinct()
            .ToList();

        var existingClasses = await _unitOfWork.Classes.GetAllAsync(
            c => programIdsNeedingCoverage.Contains(c.ProgramId) && !c.IsDeleted);
        var programsWithClass = existingClasses.Select(c => c.ProgramId).ToHashSet();

        var programs = await _unitOfWork.Programs.GetAllAsync(
            p => programIdsNeedingCoverage.Contains(p.Id) && !p.IsDeleted);
        var programsById = programs.ToDictionary(p => p.Id);

        var defaultMentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var seedTime = DateTime.UtcNow;
        var classesToAdd = new List<Class>();
        var skillsToAdd = new List<ClassSkill>();

        foreach (var programId in programIdsNeedingCoverage)
        {
            if (programsWithClass.Contains(programId))
            {
                continue;
            }

            if (!programsById.TryGetValue(programId, out var program))
            {
                continue;
            }

            var mentorId = await ResolveMentorIdForProgramAsync(programId) ?? defaultMentor?.Id;
            var classCode = $"CLS-SEED-{program.Code}";
            var existingByCode = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code == classCode && !c.IsDeleted);
            if (existingByCode != null)
            {
                continue;
            }

            var classEntity = new Class
            {
                Id = Guid.NewGuid(),
                Code = classCode,
                Name = $"{program.Name} Seed Cohort",
                ProgramId = program.Id,
                MentorId = mentorId,
                StartDate = seedTime.AddDays(7),
                EndDate = seedTime.AddDays(98),
                MaxCapacity = 30,
                Status = ClassStatus.Open,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = "Seed cohort for FE enrollment coverage",
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            classesToAdd.Add(classEntity);

            var skillCodes = SeedClassSkillPlan
                .FirstOrDefault(p => p.ProgramCode == program.Code)
                .SkillCodes;
            if (skillCodes != null)
            {
                foreach (var skillCode in skillCodes)
                {
                    var skill = await _unitOfWork.Skills.FirstOrDefaultAsync(s => s.Code == skillCode && !s.IsDeleted);
                    if (skill == null)
                    {
                        continue;
                    }

                    skillsToAdd.Add(new ClassSkill
                    {
                        Id = Guid.NewGuid(),
                        ClassId = classEntity.Id,
                        SkillId = skill.Id,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false,
                    });
                }
            }
        }

        if (classesToAdd.Count == 0)
        {
            _loggerService.LogInformation("All Active/Completed programs already have at least one class.");
            return;
        }

        await _unitOfWork.Classes.AddRangeAsync(classesToAdd);
        if (skillsToAdd.Count > 0)
        {
            await _unitOfWork.ClassSkills.AddRangeAsync(skillsToAdd);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Created {ClassCount} seed class(es) with {SkillCount} class skill(s) for programs missing cohorts.",
            classesToAdd.Count,
            skillsToAdd.Count);
    }

    private async Task<Guid?> ResolveMentorIdForProgramAsync(Guid programId)
    {
        var assigned = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.ProgramId == programId
                 && c.MentorId != null
                 && !c.IsDeleted);
        return assigned?.MentorId;
    }
}
