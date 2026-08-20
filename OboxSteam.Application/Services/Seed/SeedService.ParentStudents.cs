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
    private async Task SeedParentStudentLinksAsync()
    {
        _loggerService.LogInformation("Starting seed parent-student links");
        var existingParentStudents = await _unitOfWork.ParentStudents.GetAllAsync();
        if (!existingParentStudents.Any())
        {
            var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "PRT-001");
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");

            if (parent != null && student1 != null && student2 != null)
            {
                var parentStudents = new List<ParentStudent>
                {
                    new ParentStudent
                    {
                        Id = Guid.NewGuid(),
                        ParentId = parent.Id,
                        StudentId = student1.Id,
                        IsVerified = true,
                        CreatedAt = _seedNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new ParentStudent
                    {
                        Id = Guid.NewGuid(),
                        ParentId = parent.Id,
                        StudentId = student2.Id,
                        IsVerified = true,
                        CreatedAt = _seedNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                if (student3 != null)
                {
                    parentStudents.Add(new ParentStudent
                    {
                        Id = Guid.NewGuid(),
                        ParentId = parent.Id,
                        StudentId = student3.Id,
                        IsVerified = false,
                        CreatedAt = _seedNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                await _unitOfWork.ParentStudents.AddRangeAsync(parentStudents);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed parent-student links");
            }
            else
            {
                _loggerService.LogWarning("Parent or students not found. Skipping parent-student seeding.");
            }
        }
        else
        {
            _loggerService.LogInformation("Parent-student links already exist, skipping seeding");
        }

    }
}

