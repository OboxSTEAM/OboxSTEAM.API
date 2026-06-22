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
    private async Task SeedCourseEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed course enrollments");
        var existingCourseEnrollments = await _unitOfWork.CourseEnrollments.GetAllAsync();
        if (!existingCourseEnrollments.Any())
        {
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
            var courseRobotics1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-01");
            var courseRobotics2 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-02");
            var courseWebDev1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-WEBDEV-01");
            var courseSteam1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-STEAM-01");
            var enrollTime = DateTime.UtcNow;

            var courseEnrollments = new List<CourseEnrollment>();

            if (student1 != null && courseRobotics1 != null)
            {
                courseEnrollments.Add(new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student1.Id,
                    CourseId = courseRobotics1.Id,
                    Status = EnrollmentStatus.Active,
                    JoinedAt = enrollTime.AddDays(-7),
                    StartedAt = enrollTime.AddDays(-6),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student1 != null && courseRobotics2 != null)
            {
                courseEnrollments.Add(new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student1.Id,
                    CourseId = courseRobotics2.Id,
                    Status = EnrollmentStatus.Active,
                    JoinedAt = enrollTime.AddDays(-3),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student2 != null && courseWebDev1 != null)
            {
                courseEnrollments.Add(new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student2.Id,
                    CourseId = courseWebDev1.Id,
                    Status = EnrollmentStatus.Active,
                    JoinedAt = enrollTime.AddDays(-4),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student3 != null && courseSteam1 != null)
            {
                courseEnrollments.Add(new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student3.Id,
                    CourseId = courseSteam1.Id,
                    Status = EnrollmentStatus.Active,
                    JoinedAt = enrollTime.AddDays(-12),
                    StartedAt = enrollTime.AddDays(-11),
                    CompletedAt = null,
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (courseEnrollments.Count > 0)
            {
                await _unitOfWork.CourseEnrollments.AddRangeAsync(courseEnrollments);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed course enrollments");
            }
        }
        else
        {
            _loggerService.LogInformation("Course enrollments already exist, skipping seeding");
        }

    }
}

