using OboxSteam.Application.DTOs.ExpertDTO;
using OboxSteam.Application.Utils;

namespace OboxSteam.Application.Validation;

public static class ExpertProfileValidator
{
    public const int MaxSpecializationCount = 20;
    public const int MaxSpecializationTagLength = 80;
    public const int MaxBioOrAchievementsLength = 4000;
    public const int MaxTitleOrOrganizationLength = 255;

    public static void ValidateDegreeRequest(string? title, string? institution, int year)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw ErrorHelper.BadRequest("Degree title is required.");
        }

        if (string.IsNullOrWhiteSpace(institution))
        {
            throw ErrorHelper.BadRequest("Degree institution is required.");
        }

        ValidateYear(year);
    }

    public static void ValidatePublicationRequest(string? title, int year)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw ErrorHelper.BadRequest("Publication title is required.");
        }

        ValidateYear(year);
    }

    public static void ValidateYear(int year)
    {
        var maxYear = DateTime.UtcNow.Year + 1;
        if (year < 1950 || year > maxYear)
        {
            throw ErrorHelper.BadRequest($"Year must be between 1950 and {maxYear}.");
        }
    }

    public static void ValidateSelfUpdateRequest(UpdateMyExpertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        {
            throw ErrorHelper.BadRequest("Full name must be at least 2 characters long.");
        }

        if (request.FullName.Trim().Length > MaxTitleOrOrganizationLength)
        {
            throw ErrorHelper.BadRequest($"Full name must be at most {MaxTitleOrOrganizationLength} characters.");
        }

        ValidateOptionalMaxLength(request.Title, "Title", MaxTitleOrOrganizationLength);
        ValidateOptionalMaxLength(request.Organization, "Organization", MaxTitleOrOrganizationLength);
        ValidateOptionalMaxLength(request.Bio, "Bio", MaxBioOrAchievementsLength);
        ValidateOptionalMaxLength(request.Achievements, "Achievements", MaxBioOrAchievementsLength);

        if (!string.IsNullOrWhiteSpace(request.LinkedInUrl))
        {
            ValidateHttpUrl(request.LinkedInUrl, "LinkedInUrl");
        }

        if (request.AvatarUrl is { Length: > 0 } && !string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            ValidateHttpUrl(request.AvatarUrl, "AvatarUrl");
        }

        if (request.Specialization != null)
        {
            ValidateSpecialization(request.Specialization);
        }
    }

    public static string[] NormalizeSpecialization(string[] specialization)
    {
        return specialization
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void ValidateSpecialization(string[] specialization)
    {
        var tags = NormalizeSpecialization(specialization);
        if (tags.Length > MaxSpecializationCount)
        {
            throw ErrorHelper.BadRequest(
                $"Specialization may contain at most {MaxSpecializationCount} tags.");
        }

        var tooLong = tags.FirstOrDefault(t => t.Length > MaxSpecializationTagLength);
        if (tooLong != null)
        {
            throw ErrorHelper.BadRequest(
                $"Each specialization tag must be at most {MaxSpecializationTagLength} characters.");
        }
    }

    private static void ValidateOptionalMaxLength(string? value, string fieldName, int maxLength)
    {
        if (value != null && value.Trim().Length > maxLength)
        {
            throw ErrorHelper.BadRequest($"{fieldName} must be at most {maxLength} characters.");
        }
    }

    private static void ValidateHttpUrl(string value, string fieldName)
    {
        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw ErrorHelper.BadRequest($"{fieldName} must be a valid http or https URL.");
        }
    }
}
