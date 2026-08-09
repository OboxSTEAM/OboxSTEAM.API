using OboxSteam.Application.DTOs.MentorDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>Business rules for mentor skill tags and evidence.</summary>
public static class MentorSkillValidator
{
    public const int MaxYearsOfExperience = 60;
    public const int MaxDescriptionLength = 4000;
    public const int MaxEvidencesPerSkill = 20;
    public const int MaxEvidenceTitleLength = 255;
    public const int MaxEvidenceIssuerLength = 255;
    public const int MaxEvidenceUrlLength = 2000;
    public const int MaxEvidenceCredentialIdLength = 100;

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

        if (mentor.Role is not (RoleType.Mentor or RoleType.Manager or RoleType.Admin))
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

    public static void ValidateYearsOfExperience(int years)
    {
        if (years < 0 || years > MaxYearsOfExperience)
        {
            throw ErrorHelper.BadRequest(
                $"YearsOfExperience must be between 0 and {MaxYearsOfExperience}.");
        }
    }

    public static void ValidateDescription(string? description)
    {
        if (description != null && description.Length > MaxDescriptionLength)
        {
            throw ErrorHelper.BadRequest(
                $"Description must be at most {MaxDescriptionLength} characters.");
        }
    }

    public static void ValidateEvidenceList(
        IReadOnlyList<MentorSkillEvidenceRequestDto>? evidences,
        DateTime utcNow)
    {
        if (evidences == null)
        {
            return;
        }

        if (evidences.Count > MaxEvidencesPerSkill)
        {
            throw ErrorHelper.BadRequest(
                $"A mentor skill may have at most {MaxEvidencesPerSkill} evidence entries.");
        }

        foreach (var evidence in evidences)
        {
            ValidateEvidence(evidence, utcNow);
        }
    }

    public static void ValidateEvidence(
        MentorSkillEvidenceRequestDto evidence,
        DateTime utcNow)
    {
        if (evidence == null)
        {
            throw ErrorHelper.BadRequest("Evidence entry cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(evidence.Title))
        {
            throw ErrorHelper.BadRequest("Evidence title is required.");
        }

        if (evidence.Title.Trim().Length > MaxEvidenceTitleLength)
        {
            throw ErrorHelper.BadRequest(
                $"Evidence title must be at most {MaxEvidenceTitleLength} characters.");
        }

        if (evidence.Issuer != null && evidence.Issuer.Trim().Length > MaxEvidenceIssuerLength)
        {
            throw ErrorHelper.BadRequest(
                $"Evidence issuer must be at most {MaxEvidenceIssuerLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(evidence.Url))
        {
            throw ErrorHelper.BadRequest("Evidence URL is required.");
        }

        var url = evidence.Url.Trim();
        if (url.Length > MaxEvidenceUrlLength)
        {
            throw ErrorHelper.BadRequest(
                $"Evidence URL must be at most {MaxEvidenceUrlLength} characters.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw ErrorHelper.BadRequest("Evidence URL must be an absolute HTTPS URL.");
        }

        if (evidence.CredentialId != null &&
            evidence.CredentialId.Trim().Length > MaxEvidenceCredentialIdLength)
        {
            throw ErrorHelper.BadRequest(
                $"Evidence credential id must be at most {MaxEvidenceCredentialIdLength} characters.");
        }

        if (evidence.IssuedAt.HasValue && evidence.IssuedAt.Value > utcNow)
        {
            throw ErrorHelper.BadRequest("Evidence issue date cannot be in the future.");
        }
    }
}
