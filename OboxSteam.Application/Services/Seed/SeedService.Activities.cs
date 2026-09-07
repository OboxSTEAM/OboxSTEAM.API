using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedActivitiesAsync()
    {
        _loggerService.LogInformation("Starting seed activities");
        var existingCodes = (await _unitOfWork.Activities.GetAllAsync(a => !a.IsDeleted))
            .Select(a => a.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allCourses = await _unitOfWork.Courses.GetAllAsync(c => !c.IsDeleted);
        var courseByCode = allCourses.ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);
        var activities = CreateSeedActivities(courseByCode, _seedNow)
            .Where(a => !existingCodes.Contains(a.Code))
            .ToList();

        if (activities.Count > 0)
        {
            await _unitOfWork.Activities.AddRangeAsync(activities);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation("Finished seed activities — {Count} activity(ies) created.", activities.Count);
        }
        else if (existingCodes.Contains("ACT-ROBOTICS-01-02"))
        {
            _loggerService.LogInformation("Catalog activities already present, nothing to insert.");
        }
        else
        {
            _loggerService.LogWarning("No activities seeded because required courses were not found.");
        }

    }
}

