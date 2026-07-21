using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>Business rules for mentor skill tags.</summary>
public static class MentorSkillValidator
{
    public static void ValidateSkillExists(Skill? skill, Guid skillId)
    {
        if (skill == null || skill.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Skill with id '{skillId}' not found.");
        }
    }

    public static void ValidateMentorUser(User? mentor, Guid mentorId)
    {
        if (mentor == null || mentor.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Mentor with id '{mentorId}' not found.");
        }

        if (mentor.Role is not (RoleType.Mentor or RoleType.Manager or RoleType.SuperAdmin))
        {
            throw ErrorHelper.BadRequest($"User '{mentorId}' is not eligible for mentor skills.");
        }
    }

    public static void ValidateNoDuplicate(MentorSkill? existing)
    {
        if (existing != null)
        {
            throw ErrorHelper.Conflict("This skill is already on the mentor profile.");
        }
    }

    public static void ValidateMentorSkillExists(MentorSkill? entity, Guid id)
    {
        if (entity == null || entity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Mentor skill with id '{id}' not found.");
        }
    }

    public static void ValidateOwnership(MentorSkill entity, Guid mentorId)
    {
        if (entity.MentorId != mentorId)
        {
            throw ErrorHelper.Forbidden("You can only manage your own mentor skills.");
        }
    }

    public static void ValidateClassLimitValue(int? maxConcurrentClasses)
    {
        if (maxConcurrentClasses.HasValue && maxConcurrentClasses.Value < 1)
        {
            throw ErrorHelper.BadRequest("MaxConcurrentClasses must be at least 1.");
        }
    }
}
