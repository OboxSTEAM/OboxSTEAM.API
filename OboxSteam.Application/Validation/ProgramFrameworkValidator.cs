using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramFrameworkDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Opt-in blueprint field rules and submit-review pre-check.
/// Null constraint fields are not enforced. Multiple pre-check failures are joined
/// into one <see cref="ErrorHelper.BadRequest"/> message.
/// </summary>
public static class ProgramFrameworkValidator
{
    public const int MaxNameLength = 255;

    public static void ValidateName(string? name, bool required)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (required)
            {
                throw ErrorHelper.BadRequest("Framework name is required.");
            }

            return;
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw ErrorHelper.BadRequest($"Framework name must be at most {MaxNameLength} characters.");
        }
    }

    public static void ValidatePositiveConstraint(string fieldName, int? value)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw ErrorHelper.BadRequest($"{fieldName} must be greater than 0 when set.");
        }
    }

    public static void ValidateCriterion(FrameworkRubricCriterionRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Criterion cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw ErrorHelper.BadRequest("Criterion name is required.");
        }

        if (request.Name.Trim().Length > MaxNameLength)
        {
            throw ErrorHelper.BadRequest($"Criterion name must be at most {MaxNameLength} characters.");
        }

        if (request.MaxScore <= 0)
        {
            throw ErrorHelper.BadRequest("Criterion max score must be greater than 0.");
        }
    }

    public static void ValidateCriteriaList(IReadOnlyList<FrameworkRubricCriterionRequest>? criteria)
    {
        if (criteria == null)
        {
            return;
        }

        foreach (var criterion in criteria)
        {
            ValidateCriterion(criterion);
        }
    }

    /// <summary>
    /// Pre-check a program against its optional framework. No-op when
    /// <see cref="Program.FrameworkId"/> is null or the blueprint was removed.
    /// Only non-null rules are evaluated. Called from submit-review.
    /// </summary>
    public static async Task ValidateForSubmitAsync(IUnitOfWork unitOfWork, Guid programId)
    {
        var program = await unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        if (!program.FrameworkId.HasValue)
        {
            return;
        }

        var framework = await unitOfWork.ProgramFrameworks.GetByIdAsync(program.FrameworkId.Value);
        if (framework == null || framework.IsDeleted)
        {
            return;
        }

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(unitOfWork, programId);
        var errors = CollectRuleFailures(framework, snapshot);

        if (errors.Count > 0)
        {
            throw ErrorHelper.BadRequest(string.Join(" ", errors));
        }
    }

    public static List<string> CollectRuleFailures(
        ProgramFramework framework,
        ProgramCurriculumTreeSnapshot snapshot)
    {
        var errors = new List<string>();

        var moduleCount = snapshot.Modules.Count;
        if (framework.MinModules.HasValue && moduleCount < framework.MinModules.Value)
        {
            errors.Add(
                $"Program has {moduleCount} module(s); framework requires at least {framework.MinModules.Value}.");
        }

        var offlineCount = snapshot.ActivitiesById.Values
            .Count(a => a.ActivityType == ActivityType.Offline);
        if (framework.MinOfflineSessions.HasValue && offlineCount < framework.MinOfflineSessions.Value)
        {
            errors.Add(
                $"Program has {offlineCount} Offline session(s); framework requires at least {framework.MinOfflineSessions.Value}.");
        }

        var liveCount = snapshot.ActivitiesById.Values
            .Count(a => a.ActivityType == ActivityType.LiveOnline);
        if (framework.MinLiveSessions.HasValue && liveCount < framework.MinLiveSessions.Value)
        {
            errors.Add(
                $"Program has {liveCount} LiveOnline session(s); framework requires at least {framework.MinLiveSessions.Value}.");
        }

        if (framework.RequireFinalAssessment == true)
        {
            var capstoneCount = snapshot.MilestonesByModuleId.Values
                .SelectMany(m => m)
                .Count(m => m.IsCapstone && !m.IsDeleted);
            if (capstoneCount < 1)
            {
                errors.Add(
                    "Program has no capstone research milestone; framework requires a final assessment.");
            }
        }

        return errors;
    }
}
