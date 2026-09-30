using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>Live framework rule check of a program's current curriculum.</summary>
public static class ProgramFrameworkCheck
{
    public const string UnavailableCode = "FRAMEWORK_UNAVAILABLE";

    /// <summary>
    /// Passes trivially without a pinned framework version. Throws 409
    /// <see cref="UnavailableCode"/> when the pinned version is missing or unpublished.
    /// </summary>
    public static async Task<FrameworkCheckDto> RunAsync(
        IUnitOfWork unitOfWork,
        Program program,
        ProgramCurriculumTreeSnapshot? tree = null)
    {
        var dto = new FrameworkCheckDto
        {
            ProgramId = program.Id,
            FrameworkVersionId = program.FrameworkVersionId,
            AllPassed = true,
        };

        if (!program.FrameworkVersionId.HasValue)
        {
            return dto;
        }

        var version = await unitOfWork.ProgramFrameworkVersions.GetByIdAsync(program.FrameworkVersionId.Value);
        if (version == null || version.IsDeleted || !version.IsPublished)
        {
            throw ErrorHelper.Conflict(
                "The assigned framework version is unavailable or not published.",
                UnavailableCode);
        }

        tree ??= await ProgramCurriculumTreeLoader.LoadAsync(unitOfWork, program.Id);
        dto.Checks = await FrameworkRuleEvaluator.EvaluateAsync(unitOfWork, version, tree);
        dto.AllPassed = dto.Checks.TrueForAll(c => c.Passed);
        return dto;
    }
}
