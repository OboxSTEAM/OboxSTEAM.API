using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private static readonly (
        string Code,
        string Name,
        string ProgramCode,
        ClassStatus Status,
        int StartDaysOffset,
        int EndDaysOffset,
        string ScheduleSummary,
        string[] SkillCodes)[] MentorBoardClassDefinitions =
    [
        (
            "CLS-BOARD-ROBOTICS-01",
            "Robotics Mentor Board Cohort",
            "PRG-ROBOTICS",
            ClassStatus.ReadyForMentor,
            40,
            130,
            "TBD — awaiting mentor assignment",
            ["SKL-TECH-ROBOTICS-IOT", "SKL-ENG-PROTOTYPE"]),
        (
            "CLS-BOARD-ROBOTICS-02",
            "Robotics Mentor Board Open Cohort",
            "PRG-ROBOTICS",
            ClassStatus.ReadyForMentor,
            45,
            135,
            "Weekend intensive — mentor TBD",
            ["SKL-TECH-ROBOTICS-IOT", "SKL-TECH-PROG-PYTHON"]),
        (
            "CLS-BOARD-WEBDEV-01",
            "Web Dev Mentor Board Cohort",
            "PRG-WEBDEV",
            ClassStatus.ReadyForMentor,
            50,
            140,
            "Evening cohort — mentor TBD",
            ["SKL-TECH-PROG-JS", "SKL-ART-UXUI"]),
        (
            "CLS-BOARD-IOT-01",
            "IoT Mentor Board Open Cohort",
            "PRG-IOT",
            ClassStatus.ReadyForMentor,
            55,
            145,
            "Sensor lab cohort — mentor TBD",
            ["SKL-TECH-ROBOTICS-IOT", "SKL-ENG-SYSTEMS"]),
        (
            "CLS-BOARD-GAMEDEV-01",
            "Game Dev Mentor Board Cohort",
            "PRG-GAMEDEV",
            ClassStatus.ReadyForMentor,
            60,
            150,
            "Unity studio — mentor TBD",
            ["SKL-TECH-SOFTWARE", "SKL-SOFT-CREATIVE"]),
        (
            "CLS-BOARD-AIBASIC-01",
            "AI Basics Mentor Board Open Cohort",
            "PRG-AIBASIC",
            ClassStatus.ReadyForMentor,
            65,
            155,
            "ML intro lab — mentor TBD",
            ["SKL-TECH-PROG-PYTHON", "SKL-MATH-STATS"]),
    ];

    private static readonly (
        string ClassCode,
        string MentorCode,
        ClassMentorRequestStatus Status,
        string? Message,
        string? DecisionNote)[] MentorBoardRequestPlan =
    [
        (
            "CLS-BOARD-ROBOTICS-02",
            "MNT-001",
            ClassMentorRequestStatus.Pending,
            "I can cover the weekend intensive; already mentoring Spring cohort.",
            null),
        (
            "CLS-BOARD-WEBDEV-01",
            "MNT-007",
            ClassMentorRequestStatus.Withdrawn,
            "Initially interested; withdrawing due to schedule conflict.",
            null),
        (
            "CLS-BOARD-WEBDEV-01",
            "MNT-004",
            ClassMentorRequestStatus.Rejected,
            "Interested in expanding into web foundations.",
            "Skill profile is game-focused; prefer a web specialist for this cohort."),
        (
            "CLS-BOARD-GAMEDEV-01",
            "MNT-004",
            ClassMentorRequestStatus.Pending,
            "Unity and creative direction are my primary track — ready to own this draft cohort.",
            null),
    ];

    /// <summary>
    /// Seeds Manager-created unassigned classes (MentorId null) with required skills,
    /// plus sample ClassMentorRequests for the mentor assign board FE.
    /// </summary>
    private async Task SeedMentorBoardClassesAsync()
    {
        _loggerService.LogInformation("Starting seed mentor board (unassigned) classes");

        var seedTime = _seedNow;
        var classesToAdd = new List<Class>();
        var skillsToAdd = new List<ClassSkill>();
        var createdClassCodes = new List<string>();

        foreach (var definition in MentorBoardClassDefinitions)
        {
            var existing = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code == definition.Code && !c.IsDeleted);
            if (existing != null)
            {
                continue;
            }

            var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
                p => p.Code == definition.ProgramCode && !p.IsDeleted);
            if (program == null)
            {
                _loggerService.LogWarning(
                    "Program {ProgramCode} not found. Skipping board class {ClassCode}.",
                    definition.ProgramCode,
                    definition.Code);
                continue;
            }

            var classEntity = new Class
            {
                Id = Guid.NewGuid(),
                Code = definition.Code,
                Name = definition.Name,
                ProgramId = program.Id,
                MentorId = null,
                StartDate = seedTime.AddDays(definition.StartDaysOffset),
                EndDate = seedTime.AddDays(definition.EndDaysOffset),
                MaxCapacity = 20,
                Status = definition.Status,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = definition.ScheduleSummary,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            classesToAdd.Add(classEntity);
            createdClassCodes.Add(definition.Code);

            foreach (var skillCode in definition.SkillCodes)
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

        if (classesToAdd.Count > 0)
        {
            await _unitOfWork.Classes.AddRangeAsync(classesToAdd);
            if (skillsToAdd.Count > 0)
            {
                await _unitOfWork.ClassSkills.AddRangeAsync(skillsToAdd);
            }

            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Created {ClassCount} mentor-board class(es) with {SkillCount} skill tag(s).",
                classesToAdd.Count,
                skillsToAdd.Count);
        }
        else
        {
            _loggerService.LogInformation("Mentor board classes already seeded, skipping class create");
        }

        await SeedMentorBoardRequestsAsync(seedTime);
    }

    private async Task SeedMentorBoardRequestsAsync(DateTime seedTime)
    {
        var manager = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == "MNG-001" || u.Role == RoleType.Manager);

        var mentorsByCode = (await _unitOfWork.Users.GetAllAsync(u => u.Role == RoleType.Mentor && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);
        var boardCodes = MentorBoardRequestPlan
            .Select(r => r.ClassCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var classesByCode = (await _unitOfWork.Classes.GetAllAsync(
                c => boardCodes.Contains(c.Code) && !c.IsDeleted))
            .ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);

        var requestsToAdd = new List<ClassMentorRequest>();

        foreach (var row in MentorBoardRequestPlan)
        {
            if (!classesByCode.TryGetValue(row.ClassCode, out var classEntity)
                || !mentorsByCode.TryGetValue(row.MentorCode, out var mentor))
            {
                continue;
            }

            var existing = await _unitOfWork.ClassMentorRequests.FirstOrDefaultAsync(
                r => r.ClassId == classEntity.Id
                     && r.MentorId == mentor.Id
                     && r.Status == row.Status
                     && !r.IsDeleted);
            if (existing != null)
            {
                continue;
            }

            if (row.Status == ClassMentorRequestStatus.Pending)
            {
                var pending = await _unitOfWork.ClassMentorRequests.FirstOrDefaultAsync(
                    r => r.ClassId == classEntity.Id
                         && r.MentorId == mentor.Id
                         && r.Status == ClassMentorRequestStatus.Pending
                         && !r.IsDeleted);
                if (pending != null)
                {
                    continue;
                }
            }

            var request = new ClassMentorRequest
            {
                Id = Guid.NewGuid(),
                ClassId = classEntity.Id,
                MentorId = mentor.Id,
                Status = row.Status,
                Message = row.Message,
                CreatedAt = seedTime.AddDays(-1),
                CreatedBy = mentor.Id,
                IsDeleted = false,
            };

            if (row.Status is ClassMentorRequestStatus.Rejected or ClassMentorRequestStatus.Approved)
            {
                request.DecidedAt = seedTime.AddHours(-6);
                request.DecidedBy = manager?.Id;
                request.DecisionNote = row.DecisionNote;
            }

            if (row.Status == ClassMentorRequestStatus.Withdrawn)
            {
                request.DecidedAt = seedTime.AddHours(-3);
            }

            requestsToAdd.Add(request);
        }

        if (requestsToAdd.Count == 0)
        {
            _loggerService.LogInformation("Mentor board requests already seeded, skipping");
            return;
        }

        await _unitOfWork.ClassMentorRequests.AddRangeAsync(requestsToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed mentor board requests — {Count} request(s).",
            requestsToAdd.Count);
    }
}
