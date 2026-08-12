using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AssessmentAttemptPolicyTests
{
    [Fact]
    public async Task GetEffectiveMaxAttempts_Theory_IsUnlimited()
    {
        var db = new InMemoryUnitOfWork();
        var moduleId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.Modules.Seed(new Module
        {
            Id = moduleId,
            Code = "T1",
            Name = "Theory",
            ProgramId = Guid.NewGuid(),
            ModuleType = ModuleType.Theory,
            IsDeleted = false
        });
        var assignment = new Assignment
        {
            Id = assignmentId,
            Code = "A1",
            ModuleId = moduleId,
            Title = "Quiz",
            MaxAttempts = 1,
            IsDeleted = false
        };

        var max = await AssessmentAttemptPolicy.GetEffectiveMaxAttemptsAsync(
            db, assignment, studentId);

        Assert.Equal(int.MaxValue, max);
    }

    [Fact]
    public async Task ValidateMaxAttemptsNotExceededAsync_Experiential_Throws_WhenExceeded()
    {
        var db = new InMemoryUnitOfWork();
        var moduleId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.Modules.Seed(new Module
        {
            Id = moduleId,
            Code = "E1",
            Name = "Lab",
            ProgramId = Guid.NewGuid(),
            ModuleType = ModuleType.Experiential,
            IsDeleted = false
        });
        var assignment = new Assignment
        {
            Id = assignmentId,
            Code = "A1",
            ModuleId = moduleId,
            Title = "Lab task",
            MaxAttempts = 1,
            IsDeleted = false
        };

        await Assert.ThrowsAsync<ConflictException>(() =>
            ResearchSubmissionValidator.ValidateMaxAttemptsNotExceededAsync(
                db, assignment, studentId, nextAttemptNumber: 2));
    }
}
