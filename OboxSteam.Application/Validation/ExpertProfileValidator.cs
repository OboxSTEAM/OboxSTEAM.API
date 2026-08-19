using OboxSteam.Application.Utils;

namespace OboxSteam.Application.Validation;

public static class ExpertProfileValidator
{
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
}
