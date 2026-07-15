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
    private async Task SeedCoursesAsync()
    {
        _loggerService.LogInformation("Starting seed courses");
        var existingCourses = await _unitOfWork.Courses.GetAllAsync();
        if (!existingCourses.Any())
        {
            var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
            var moduleRobotics2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-02");
            var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
            var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
            var moduleWebDev2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-02");
            var moduleSteam1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-STEAM-01");
            var moduleSteam2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-STEAM-02");
            var moduleIot1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-IOT-01");
            var moduleIot2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-IOT-02");
            var moduleCertTest = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-CERT-TEST-01");

            var courses = new List<Course>();
            var seedTime = DateTime.UtcNow;

            AddRoboticsCourses(
                courses,
                moduleRobotics1,
                moduleRobotics2,
                moduleRobotics3,
                seedTime);

            if (moduleWebDev1 != null)
            {
                courses.Add(new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-WEBDEV-01",
                    ModuleId = moduleWebDev1.Id,
                    Name = "HTML & CSS - Evening Class",
                    Description = "Evening cohort for HTML structure, semantic markup, and responsive CSS layouts.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
            else
            {
                _loggerService.LogWarning("Module MOD-WEBDEV-01 not found. Skipping web foundations course seeding.");
            }

            if (moduleWebDev2 != null)
            {
                courses.Add(new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-WEBDEV-02",
                    ModuleId = moduleWebDev2.Id,
                    Name = "JavaScript Basics - Weekend Bootcamp",
                    Description = "Weekend intensive on variables, DOM manipulation, and simple interactive pages.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
            else
            {
                _loggerService.LogWarning("Module MOD-WEBDEV-02 not found. Skipping JavaScript course seeding.");
            }

            if (moduleSteam1 != null)
            {
                courses.Add(new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-STEAM-01",
                    ModuleId = moduleSteam1.Id,
                    Name = "STEAM Lab Kickoff - Cohort 1",
                    Description = "Introductory STEAM lab exploring interdisciplinary project-based learning.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
            else
            {
                _loggerService.LogWarning("Module MOD-STEAM-01 not found. Skipping STEAM kickoff course seeding.");
            }

            if (moduleSteam2 != null)
            {
                courses.Add(new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-STEAM-02",
                    ModuleId = moduleSteam2.Id,
                    Name = "Creative Prototyping - Workshop A",
                    Description = "Hands-on workshop for rapid prototyping with recycled materials and simple circuits.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
            else
            {
                _loggerService.LogWarning("Module MOD-STEAM-02 not found. Skipping creative prototyping course seeding.");
            }

            if (moduleIot1 != null)
            {
                courses.Add(new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-IOT-01",
                    ModuleId = moduleIot1.Id,
                    Name = "Sensors 101 - Morning Class",
                    Description = "Introduction to sensors, Arduino basics, and reading environmental data.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
            else
            {
                _loggerService.LogWarning("Module MOD-IOT-01 not found. Skipping IoT sensors course seeding.");
            }

            if (moduleIot2 != null)
            {
                courses.Add(new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-IOT-02",
                    ModuleId = moduleIot2.Id,
                    Name = "Cloud Lab - Cohort Beta",
                    Description = "Connect devices to the cloud using MQTT and visualize live sensor data.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
            else
            {
                _loggerService.LogWarning("Module MOD-IOT-02 not found. Skipping IoT cloud course seeding.");
            }

            if (moduleCertTest != null)
            {
                courses.Add(new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-CERT-TEST-01",
                    ModuleId = moduleCertTest.Id,
                    Name = "Certificate Test Course",
                    Description = "Single-course fixture for certificate generation testing.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
            else
            {
                _loggerService.LogWarning("Module MOD-CERT-TEST-01 not found. Skipping certificate test course seeding.");
            }

            if (courses.Count > 0)
            {
                await _unitOfWork.Courses.AddRangeAsync(courses);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed courses — {Count} course(s) created.", courses.Count);
            }
            else
            {
                _loggerService.LogWarning("No courses seeded because required modules were not found.");
            }
        }
        else
        {
            _loggerService.LogInformation("Courses already exist, skipping course seeding");
        }

    }
}

