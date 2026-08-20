using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedActivitiesAsync()
    {
        _loggerService.LogInformation("Starting seed activities");
        var existingActivities = await _unitOfWork.Activities.AnyIncludingDeletedAsync();

        if (!existingActivities)
        {
            var allCourses = await _unitOfWork.Courses.GetAllAsync();
            var courseByCode = allCourses.ToDictionary(c => c.Code, c => c);
            var seedTime = _seedNow;

            var activities = CreateSeedActivities(courseByCode, seedTime);

            if (activities.Count > 0)
            {
                await _unitOfWork.Activities.AddRangeAsync(activities);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed activities — {Count} activity(ies) created.", activities.Count);
            }
            else
            {
                _loggerService.LogWarning("No activities seeded because required courses were not found.");
            }
        }
        else
        {
            _loggerService.LogInformation("Activities already exist, skipping activity seeding");
        }

    }
}

