using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    // (courseCode, quizCode, title, questionCount) for Module 1 (Theory) bank-backed quizzes.
    // Each course gets one quiz; the last course's quiz is the larger 15-question one.
    private static readonly (string CourseCode, string BankName, string QuizCode, string Title, int QuestionCount)[]
        RoboticsModule1QuizDefinitions =
        [
            ("CRS-ROBOTICS-01", "Robot Fundamentals Question Bank", "ASG-ROBOTICS-Q01", "Robot Fundamentals Quiz", 5),
            ("CRS-ROBOTICS-02", "Mechanics & Actuators Question Bank", "ASG-ROBOTICS-Q02", "Mechanics & Actuators Quiz", 5),
            ("CRS-ROBOTICS-03", "Safety & Lab Practice Question Bank", "ASG-ROBOTICS-Q03", "Safety & Lab Practice Final Quiz", 15),
        ];

    private async Task SeedAssignmentsAsync()
    {
        _loggerService.LogInformation("Starting seed assignments");
        var seedTime = _seedNow;

        await SeedRoboticsModule1QuizzesAsync(seedTime);
        await SeedRoboticsModule2RetrospectiveAsync(seedTime);
        await SeedWebDevUploadAssignmentAsync(seedTime);

        _loggerService.LogInformation("Finished seed assignments");
    }

    private async Task SeedRoboticsModule1QuizzesAsync(DateTime seedTime)
    {
        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01" && !m.IsDeleted);
        if (module == null)
        {
            _loggerService.LogWarning("Module MOD-ROBOTICS-01 not found; skipping Module 1 quizzes.");
            return;
        }

        foreach (var definition in RoboticsModule1QuizDefinitions)
        {
            if (await AssignmentCodeExistsAsync(definition.QuizCode))
            {
                _loggerService.LogInformation("Assignment {Code} already exists; skipping.", definition.QuizCode);
                continue;
            }

            var course = await _unitOfWork.Courses.FirstOrDefaultAsync(
                c => c.Code == definition.CourseCode && !c.IsDeleted);
            if (course == null)
            {
                _loggerService.LogWarning(
                    "Course {CourseCode} not found; skipping quiz {Code}.", definition.CourseCode, definition.QuizCode);
                continue;
            }

            var bank = await _unitOfWork.QuestionBanks.FirstOrDefaultAsync(
                qb => qb.CourseId == course.Id && qb.Name == definition.BankName && !qb.IsDeleted);
            if (bank == null)
            {
                _loggerService.LogWarning(
                    "Question bank '{BankName}' not found for course {CourseCode}; skipping quiz {Code}.",
                    definition.BankName,
                    definition.CourseCode,
                    definition.QuizCode);
                continue;
            }

            var quiz = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = definition.QuizCode,
                ModuleId = module.Id,
                CourseId = course.Id,
                Title = definition.Title,
                Description = $"Auto-graded quiz drawing {definition.QuestionCount} questions from the {definition.BankName}.",
                AssignmentType = AssignmentType.Quiz,
                MaxPoints = 100,
                PassScore = 50,
                IsRequiredForModulePass = true,
                DueDate = seedTime.AddDays(14),
                AllowShuffle = true,
                ShuffleOptions = true,
                QuestionBankId = bank.Id,
                QuestionCount = definition.QuestionCount,
                EasyPercent = 40,
                MediumPercent = 40,
                HardPercent = 20,
                TimeLimitMinutes = definition.QuestionCount >= 15 ? 45 : 20,
                MaxAttempts = 3,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };

            await _unitOfWork.Assignments.AddAsync(quiz);
            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation(
                "Seeded Module 1 quiz {Code} ({QuestionCount} questions) for course {CourseCode}.",
                definition.QuizCode,
                definition.QuestionCount,
                definition.CourseCode);
        }
    }

    private async Task SeedRoboticsModule2RetrospectiveAsync(DateTime seedTime)
    {
        const string retrospectiveCode = "ASG-ROBOTICS-RETRO-02";

        if (await AssignmentCodeExistsAsync(retrospectiveCode))
        {
            _loggerService.LogInformation("Assignment {Code} already exists; skipping.", retrospectiveCode);
            return;
        }

        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-02" && !m.IsDeleted);
        if (module == null)
        {
            _loggerService.LogWarning("Module MOD-ROBOTICS-02 not found; skipping Module 2 retrospective.");
            return;
        }

        var course = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-06" && !c.IsDeleted);
        if (course == null)
        {
            _loggerService.LogWarning("Course CRS-ROBOTICS-06 not found; skipping Module 2 retrospective.");
            return;
        }

        var retrospective = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = retrospectiveCode,
            ModuleId = module.Id,
            CourseId = course.Id,
            Title = "Sensors & Movement Retrospective",
            Description = "Reflect on the field-trip sessions: what you learned about sensors, movement, and calibration.",
            AssignmentType = AssignmentType.Retrospective,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(21),
            AllowShuffle = false,
            MaxAttempts = 1,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Assignments.AddAsync(retrospective);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation("Seeded Module 2 retrospective {Code} on course CRS-ROBOTICS-06.", retrospectiveCode);
    }

    private async Task SeedWebDevUploadAssignmentAsync(DateTime seedTime)
    {
        const string uploadCode = "ASG-WEBDEV-UPLOAD-01";

        if (await AssignmentCodeExistsAsync(uploadCode))
        {
            _loggerService.LogInformation("Assignment {Code} already exists; skipping.", uploadCode);
            return;
        }

        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01" && !m.IsDeleted);
        if (module == null)
        {
            _loggerService.LogWarning("Module MOD-WEBDEV-01 not found; skipping WebDev upload assignment.");
            return;
        }

        var assignmentUpload = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = uploadCode,
            ModuleId = module.Id,
            CourseId = null,
            Title = "Build Your First Landing Page",
            Description = "Submit a ZIP file containing your HTML and CSS landing page.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            DueDate = seedTime.AddDays(21),
            AllowShuffle = false,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Assignments.AddAsync(assignmentUpload);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation("Seeded WebDev upload assignment {Code}.", uploadCode);
    }

    private async Task<bool> AssignmentCodeExistsAsync(string code)
    {
        var existing = await _unitOfWork.Assignments.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        return existing != null;
    }
}
