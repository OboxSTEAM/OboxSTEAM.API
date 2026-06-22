using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

public static class ResearchMilestoneValidator
{
    public const string MutateMilestoneForbiddenMessage =
        "Only SuperAdmin and Manager can create, update, or delete research milestones.";

    public const string MutateActivityLinkForbiddenMessage =
        "Only SuperAdmin, Manager, and assigned Mentors can manage milestone activity links.";

    public const string ViewProgressForbiddenMessage =
        "You do not have permission to view milestone progress.";

    public static Module ValidateResearchModule(Module? module, Guid moduleId)
    {
        var validModule = AssignmentValidator.ValidateModuleExists(module);

        if (validModule.ModuleType != ModuleType.Research)
        {
            throw ErrorHelper.BadRequest(
                $"Module '{moduleId}' is not a Research module. Milestones can only be created in Research modules.");
        }

        return validModule;
    }

    public static ResearchMilestone ValidateMilestoneExists(ResearchMilestone? milestone, Guid milestoneId)
    {
        if (milestone == null || milestone.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Research milestone with id '{milestoneId}' not found.");
        }

        return milestone;
    }

    public static void ValidateCanDeleteMilestone(int submissionCount)
    {
        if (submissionCount > 0)
        {
            throw ErrorHelper.Conflict(
                "Cannot delete a research milestone that has existing submissions.");
        }
    }

    public static void ValidateCapstoneUniqueness(bool isCapstone, ResearchMilestone? existingCapstone, Guid? currentMilestoneId)
    {
        if (!isCapstone || existingCapstone == null)
        {
            return;
        }

        if (currentMilestoneId.HasValue && existingCapstone.Id == currentMilestoneId.Value)
        {
            return;
        }

        throw ErrorHelper.Conflict("This module already has a capstone milestone.");
    }

    public static async Task ValidateMilestoneOrderUniqueAsync(
        IUnitOfWork unitOfWork,
        Guid moduleId,
        int milestoneOrder,
        Guid? excludeMilestoneId = null)
    {
        var duplicate = await unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.ModuleId == moduleId
                  && rm.MilestoneOrder == milestoneOrder
                  && !rm.IsDeleted
                  && (!excludeMilestoneId.HasValue || rm.Id != excludeMilestoneId.Value));

        if (duplicate != null)
        {
            throw ErrorHelper.Conflict(
                $"MilestoneOrder {milestoneOrder} is already used by another milestone in this module.");
        }
    }

    public static async Task<User> EnsureCanMutateMilestoneAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService)
    {
        var user = await GetCurrentUserAsync(unitOfWork, claimsService);

        if (user.Role is not (RoleType.SuperAdmin or RoleType.Manager))
        {
            throw ErrorHelper.Forbidden(MutateMilestoneForbiddenMessage);
        }

        return user;
    }

    public static async Task<User> EnsureCanMutateActivityLinkAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid moduleId,
        Guid? classId)
    {
        var user = await GetCurrentUserAsync(unitOfWork, claimsService);

        if (user.Role is RoleType.SuperAdmin or RoleType.Manager)
        {
            return user;
        }

        if (user.Role == RoleType.Mentor)
        {
            if (!classId.HasValue || classId.Value == Guid.Empty)
            {
                throw ErrorHelper.BadRequest(MentorScopeValidator.ClassIdRequiredMessage);
            }

            await MentorScopeValidator.EnsureMentorOwnsClassForModuleAsync(
                unitOfWork,
                user.Id,
                classId.Value,
                moduleId);
            return user;
        }

        throw ErrorHelper.Forbidden(MutateActivityLinkForbiddenMessage);
    }

    public static async Task ValidateActivityBelongsToModuleAsync(
        IUnitOfWork unitOfWork,
        Guid activityId,
        Guid moduleId)
    {
        var activity = await unitOfWork.Activities.GetByIdAsync(activityId);
        if (activity == null || activity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        var course = await unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        AssignmentValidator.ValidateCourseBelongsToModule(course, activity.CourseId, moduleId);
    }

    public static async Task<List<ResearchMilestoneActivity>> LoadActivityLinksAsync(
        IUnitOfWork unitOfWork,
        Guid milestoneId)
    {
        var links = await unitOfWork.ResearchMilestoneActivities.GetAllAsync(
            link => link.ResearchMilestoneId == milestoneId && !link.IsDeleted,
            link => link.Activity);

        return links.OrderBy(link => link.DisplayOrder).ToList();
    }

    public static bool HasPassedSubmission(
        ResearchMilestone previousMilestone,
        IReadOnlyDictionary<Guid, Submission> submissionsByMilestoneId,
        IReadOnlyDictionary<Guid, Assignment> assignmentsById)
    {
        if (!submissionsByMilestoneId.TryGetValue(previousMilestone.Id, out var submission))
        {
            return false;
        }

        if (!assignmentsById.TryGetValue(previousMilestone.AssignmentId, out var assignment))
        {
            return false;
        }

        return submission.Status == SubmissionStatus.Graded
               && submission.AssignedGrade.HasValue
               && submission.AssignedGrade.Value >= assignment.PassScore;
    }

    private static async Task<User> GetCurrentUserAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        return user;
    }
}
