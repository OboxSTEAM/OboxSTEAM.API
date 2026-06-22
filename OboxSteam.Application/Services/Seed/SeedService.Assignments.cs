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
    private async Task SeedAssignmentsAsync()
    {
        _loggerService.LogInformation("Starting seed assignments");
        var existingAssignments = await _unitOfWork.Assignments.GetAllAsync();
        if (!existingAssignments.Any())
        {
            var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
            var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
            var courseRobotics1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-01");
            var seedTime = DateTime.UtcNow;

            if (moduleRobotics1 != null)
            {
                var assignmentQuiz = new Assignment
                {
                    Id = Guid.NewGuid(),
                    Code = "ASG-ROBOTICS-QUIZ-01",
                    ModuleId = moduleRobotics1.Id,
                    CourseId = courseRobotics1?.Id,
                    Title = "Robotics Fundamentals Quiz",
                    Description = "Multiple-choice quiz covering basic robotics concepts.",
                    AssignmentType = AssignmentType.Quiz,
                    MaxPoints = 100,
                    DueDate = seedTime.AddDays(14),
                    AllowShuffle = true,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };

                await _unitOfWork.Assignments.AddAsync(assignmentQuiz);
                await _unitOfWork.SaveChangesAsync();

                var question1 = new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = assignmentQuiz.Id,
                    QuestionText = "What is the primary purpose of a sensor on a robot?",
                    QuestionType = "SingleChoice",
                    Points = 50,
                    OrderIndex = 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };

                var question2 = new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = assignmentQuiz.Id,
                    QuestionText = "Which components are essential for robot movement? (Select all that apply)",
                    QuestionType = "MultipleChoice",
                    Points = 50,
                    OrderIndex = 2,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };

                await _unitOfWork.QuizQuestions.AddRangeAsync(new List<QuizQuestion> { question1, question2 });
                await _unitOfWork.SaveChangesAsync();

                var quizOptions = new List<QuizOption>
                {
                    new QuizOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question1.Id,
                        OptionText = "To detect and respond to the environment",
                        IsCorrect = true,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new QuizOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question1.Id,
                        OptionText = "To decorate the robot",
                        IsCorrect = false,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new QuizOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question2.Id,
                        OptionText = "Motor",
                        IsCorrect = true,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new QuizOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question2.Id,
                        OptionText = "Wheel",
                        IsCorrect = true,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new QuizOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question2.Id,
                        OptionText = "Screen protector",
                        IsCorrect = false,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                await _unitOfWork.QuizOptions.AddRangeAsync(quizOptions);
                await _unitOfWork.SaveChangesAsync();
            }

            if (moduleWebDev1 != null)
            {
                var assignmentUpload = new Assignment
                {
                    Id = Guid.NewGuid(),
                    Code = "ASG-WEBDEV-UPLOAD-01",
                    ModuleId = moduleWebDev1.Id,
                    CourseId = null,
                    Title = "Build Your First Landing Page",
                    Description = "Submit a ZIP file containing your HTML and CSS landing page.",
                    AssignmentType = AssignmentType.FileUpload,
                    MaxPoints = 100,
                    DueDate = seedTime.AddDays(21),
                    AllowShuffle = false,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };

                await _unitOfWork.Assignments.AddAsync(assignmentUpload);
                await _unitOfWork.SaveChangesAsync();
            }

            _loggerService.LogInformation("Finished seed assignments");
        }
        else
        {
            _loggerService.LogInformation("Assignments already exist, skipping seeding");
        }

    }
}

