using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedAcademicYearClassesAsync()
    {
        _loggerService.LogInformation("Starting seed academic-year classes");

        var existingCodes = (await _unitOfWork.Classes.GetAllAsync(c => !c.IsDeleted))
            .Select(c => c.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var programs = (await _unitOfWork.Programs.GetAllAsync(p => !p.IsDeleted))
            .ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);
        var mentors = (await _unitOfWork.Users.GetAllAsync(u => u.Role == RoleType.Mentor && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);

        var classesToAdd = new List<Class>();
        var skillsToAdd = new List<ClassSkill>();

        foreach (var definition in GetAcademicYearClassDefinitions())
        {
            if (existingCodes.Contains(definition.Code))
            {
                continue;
            }

            if (!programs.TryGetValue(definition.ProgramCode, out var program))
            {
                _loggerService.LogWarning(
                    "Skipping class {ClassCode}: program {ProgramCode} not found.",
                    definition.Code,
                    definition.ProgramCode);
                continue;
            }

            Guid? mentorId = null;
            if (!string.IsNullOrWhiteSpace(definition.MentorCode))
            {
                if (!mentors.TryGetValue(definition.MentorCode, out var mentor))
                {
                    _loggerService.LogWarning(
                        "Skipping class {ClassCode}: mentor {MentorCode} not found.",
                        definition.Code,
                        definition.MentorCode);
                    continue;
                }

                mentorId = mentor.Id;
            }

            var startDate = AtDays(definition.StartDaysOffset).Date;
            var endDate = AtDays(definition.EndDaysOffset).Date;
            var classEntity = new Class
            {
                Id = Guid.NewGuid(),
                Code = definition.Code,
                Name = definition.Name,
                ProgramId = program.Id,
                MentorId = mentorId,
                StartDate = startDate,
                EndDate = endDate,
                MaxCapacity = definition.MaxCapacity,
                Status = definition.Status,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = definition.ScheduleSummary,
                CreatedAt = startDate.AddDays(-14),
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            classesToAdd.Add(classEntity);

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
                    CreatedAt = classEntity.CreatedAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        if (classesToAdd.Count == 0)
        {
            _loggerService.LogInformation("Academic-year classes already seeded, skipping");
            return;
        }

        await _unitOfWork.Classes.AddRangeAsync(classesToAdd);
        if (skillsToAdd.Count > 0)
        {
            await _unitOfWork.ClassSkills.AddRangeAsync(skillsToAdd);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed academic-year classes — {ClassCount} class(es), {SkillCount} skill link(s).",
            classesToAdd.Count,
            skillsToAdd.Count);
    }

    /// <summary>
    /// Existing local databases may still have unassigned cohorts as Open (the old
    /// board status). Open now requires a mentor — move those rows to ReadyForMentor.
    /// </summary>
    private async Task AlignUnassignedClassesToReadyForMentorAsync()
    {
        var codes = GetUnassignedAcademicYearClassCodes()
            .Concat(MentorBoardClassDefinitions.Select(definition => definition.Code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var classes = await _unitOfWork.Classes.GetAllAsync(
            c => !c.IsDeleted
                 && c.MentorId == null
                 && c.Status == ClassStatus.Open
                 && codes.Contains(c.Code));

        if (classes.Count == 0)
        {
            return;
        }

        foreach (var classEntity in classes)
        {
            classEntity.Status = ClassStatus.ReadyForMentor;
            await _unitOfWork.Classes.Update(classEntity);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Aligned {Count} unassigned Open class(es) to ReadyForMentor.",
            classes.Count);
    }
}
