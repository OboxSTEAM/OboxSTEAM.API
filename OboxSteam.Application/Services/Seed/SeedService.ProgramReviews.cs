using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedProgramReviewsAsync()
    {
        _loggerService.LogInformation("Starting seed program reviews");
        var existingProgramReviews = await _unitOfWork.ProgramReviews.GetAllAsync();
        if (existingProgramReviews.Any())
        {
            _loggerService.LogInformation("Program reviews already exist, skipping seeding");
            return;
        }

        var completed = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted && pe.Status == EnrollmentStatus.Completed,
            pe => pe.Student,
            pe => pe.Program);

        var comments = new[]
        {
            "The cohort schedule was clear and the mentor feedback helped me finish the project.",
            "Hands-on sessions made the theory click. I would take another program in this series.",
            "A solid track. A few live sessions ran long, but the final showcase was worth it.",
            "I liked the weekly rhythm. Materials and assignments lined up with class sessions.",
        };

        var programReviews = new List<ProgramReview>();
        var index = 0;
        foreach (var enrollment in completed)
        {
            var student = enrollment.Student
                ?? await _unitOfWork.Users.GetByIdAsync(enrollment.StudentId);
            if (student == null)
            {
                continue;
            }

            var createdAt = (enrollment.CompletedAt ?? _seedNow).AddDays(2);
            programReviews.Add(new ProgramReview
            {
                Id = Guid.NewGuid(),
                ProgramId = enrollment.ProgramId,
                StudentId = student.Id,
                StarRating = 4 + (index % 2),
                Comment = comments[index % comments.Length],
                CreatedAt = createdAt,
                CreatedBy = student.Id,
                IsDeleted = false,
            });
            index++;
        }

        if (programReviews.Count == 0)
        {
            _loggerService.LogWarning("No program reviews seeded.");
            return;
        }

        await _unitOfWork.ProgramReviews.AddRangeAsync(programReviews);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed program reviews — {Count} review(s) created.",
            programReviews.Count);
    }
}
