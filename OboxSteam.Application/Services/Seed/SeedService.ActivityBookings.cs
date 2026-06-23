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
    private async Task SeedActivityBookingsAsync()
    {
        _loggerService.LogInformation("Starting seed activity bookings");
        var existingBookings = await _unitOfWork.ActivityBookings.GetAllAsync();
        if (!existingBookings.Any())
        {
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var offlineActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-04-03");

            if (student1 != null && offlineActivity != null)
            {
                var bookings = new List<ActivityBooking>
                {
                    new ActivityBooking
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student1.Id,
                        ActivityId = offlineActivity.Id,
                        Status = BookingStatus.Booked,
                        BookedAt = DateTime.UtcNow.AddDays(-2),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                var liveActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-01-02");
                var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
                if (student2 != null && liveActivity != null)
                {
                    bookings.Add(new ActivityBooking
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student2.Id,
                        ActivityId = liveActivity.Id,
                        Status = BookingStatus.CheckedIn,
                        BookedAt = DateTime.UtcNow.AddDays(-5),
                        CheckedInAt = DateTime.UtcNow.AddDays(-4),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                await _unitOfWork.ActivityBookings.AddRangeAsync(bookings);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed activity bookings");
            }
            else
            {
                _loggerService.LogWarning("Student or offline activity not found. Skipping activity booking seeding.");
            }
        }
        else
        {
            _loggerService.LogInformation("Activity bookings already exist, skipping seeding");
        }

    }
}

