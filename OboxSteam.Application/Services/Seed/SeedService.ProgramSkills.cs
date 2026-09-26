using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Links catalog skills to published programs so completed enrollments grant portfolio skills.
    /// Idempotent: skips pairs that already exist.
    /// </summary>
    private async Task SeedProgramSkillsAsync()
    {
        _loggerService.LogInformation("Starting seed program ↔ skill links");

        var programs = await _unitOfWork.Programs.GetAllAsync(p => !p.IsDeleted);
        var skills = await _unitOfWork.Skills.GetAllAsync(s => !s.IsDeleted);
        if (programs.Count == 0 || skills.Count == 0)
        {
            _loggerService.LogWarning("Programs or skills missing — skipping program skill seed.");
            return;
        }

        var programsByCode = programs.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var skillsByCode = skills.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        var definitions = BuildProgramSkillDefinitions();
        var existing = await _unitOfWork.ProgramSkills.GetAllAsync(ps => !ps.IsDeleted);
        var existingKeys = existing
            .Select(ps => (ps.ProgramId, ps.SkillId))
            .ToHashSet();

        var toAdd = new List<ProgramSkill>();
        var missingSkillCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingProgramCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (programCode, skillCodes) in definitions)
        {
            if (!programsByCode.TryGetValue(programCode, out var program))
            {
                missingProgramCodes.Add(programCode);
                continue;
            }

            foreach (var skillCode in skillCodes)
            {
                if (!skillsByCode.TryGetValue(skillCode, out var skill))
                {
                    missingSkillCodes.Add(skillCode);
                    continue;
                }

                if (existingKeys.Contains((program.Id, skill.Id)))
                {
                    continue;
                }

                toAdd.Add(new ProgramSkill
                {
                    Id = Guid.NewGuid(),
                    ProgramId = program.Id,
                    SkillId = skill.Id,
                    ModuleId = null,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
                existingKeys.Add((program.Id, skill.Id));
            }
        }

        if (missingProgramCodes.Count > 0)
        {
            _loggerService.LogWarning(
                "Program skill seed skipped unknown program code(s): {Codes}",
                string.Join(", ", missingProgramCodes.OrderBy(c => c)));
        }

        if (missingSkillCodes.Count > 0)
        {
            _loggerService.LogWarning(
                "Program skill seed skipped unknown skill code(s): {Codes}",
                string.Join(", ", missingSkillCodes.OrderBy(c => c)));
        }

        if (toAdd.Count == 0)
        {
            _loggerService.LogInformation("Program skill links already complete. Skipping.");
            return;
        }

        await _unitOfWork.ProgramSkills.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed program skills — added {Count} link(s).", toAdd.Count);
    }

    /// <summary>
    /// Program → catalog skill codes. WEBDEV is the STD-001 completed track used by portfolio FE.
    /// </summary>
    private static List<(string ProgramCode, string[] SkillCodes)> BuildProgramSkillDefinitions() =>
    [
        ("PRG-WEBDEV",
        [
            "SKL-TECH-PROG-JS",
            "SKL-TECH-COMP-THINK",
            "SKL-TECH-DIGITAL-LIT",
            "SKL-ART-UXUI",
            "SKL-ART-VISUAL",
            "SKL-ENG-DESIGN",
            "SKL-SOFT-COMM",
            "SKL-SOFT-CREATIVE",
            "SKL-SOFT-COLLAB",
            "SKL-SOFT-SELFLEARN",
        ]),
        ("PRG-ROBOTICS",
        [
            "SKL-TECH-ROBOTICS-IOT",
            "SKL-TECH-PROG-PYTHON",
            "SKL-ENG-PROTOTYPE",
            "SKL-ENG-DESIGN",
            "SKL-ENG-TEST-ITERATE",
            "SKL-SOFT-COLLAB",
        ]),
        ("PRG-IOT",
        [
            "SKL-TECH-ROBOTICS-IOT",
            "SKL-TECH-PROG-PYTHON",
            "SKL-ENG-SYSTEMS",
            "SKL-ENG-PROTOTYPE",
            "SKL-ENG-PROBLEM",
        ]),
        ("PRG-PYBASIC",
        [
            "SKL-TECH-PROG-PYTHON",
            "SKL-TECH-COMP-THINK",
            "SKL-MATH-LOGIC",
            "SKL-SOFT-SELFLEARN",
        ]),
        ("PRG-MATHFUN",
        [
            "SKL-MATH-LOGIC",
            "SKL-MATH-PROBLEM",
            "SKL-MATH-MEASURE",
            "SKL-SOFT-CRITICAL",
        ]),
        ("PRG-DIGART",
        [
            "SKL-ART-VISUAL",
            "SKL-ART-AESTHETIC",
            "SKL-ART-UXUI",
            "SKL-SOFT-CREATIVE",
        ]),
        ("PRG-MUSICTECH",
        [
            "SKL-ART-MUSIC",
            "SKL-TECH-SOFTWARE",
            "SKL-SOFT-CREATIVE",
            "SKL-SOFT-COLLAB",
        ]),
        ("PRG-DATAMATH",
        [
            "SKL-MATH-STATS",
            "SKL-MATH-MODEL",
            "SKL-MATH-LOGIC",
            "SKL-SOFT-CRITICAL",
        ]),
        ("PRG-3DDESIGN",
        [
            "SKL-ENG-DRAWING",
            "SKL-ENG-PROTOTYPE",
            "SKL-TECH-SOFTWARE",
            "SKL-ART-VISUAL",
        ]),
        ("PRG-GAMEDEV",
        [
            "SKL-TECH-PROG-SCRATCH",
            "SKL-TECH-COMP-THINK",
            "SKL-ART-STORY",
            "SKL-SOFT-CREATIVE",
            "SKL-SOFT-COLLAB",
        ]),
    ];
}
