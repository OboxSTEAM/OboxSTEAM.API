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

        if (!program.FrameworkVersionId.HasValue)
        {
            return;
        }

        var version = await unitOfWork.ProgramFrameworkVersions.GetByIdAsync(program.FrameworkVersionId.Value);
        if (version == null || version.IsDeleted || !version.IsPublished)
        {
            throw ErrorHelper.Conflict("The assigned framework version is unavailable or not published.");
        }

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(unitOfWork, programId);
        var errors = CollectRuleFailures(version, snapshot);

        if (errors.Count > 0)
        {
            throw ErrorHelper.BadRequest(string.Join(" ", errors));
        }
    }

    public static List<string> CollectRuleFailures(
        ProgramFrameworkVersion framework,
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

        if (framework.RequireCapstoneResearchMilestone == true)
        {
            var capstoneCount = snapshot.MilestonesByModuleId.Values
                .SelectMany(m => m)
                .Count(m => m.IsCapstone && !m.IsDeleted);
            if (capstoneCount < 1)
            {
                errors.Add(
                    "Program has no ResearchMilestone with IsCapstone; framework requires a capstone research milestone.");
            }
        }

        return errors;
    }

    /// <summary>Published versions are immutable; draft versions remain editable.</summary>
    public static async Task EnsureNotLockedByReviewAsync(IUnitOfWork unitOfWork, Guid frameworkId)
    {
        var draft = await unitOfWork.ProgramFrameworkVersions.FirstOrDefaultAsync(
            v => v.FrameworkId == frameworkId && !v.IsPublished && !v.IsDeleted);
        if (draft != null)
        {
            return;
        }

        throw ErrorHelper.Conflict(
            "Create a draft framework version before editing; published versions are immutable.");
    }
}
