using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class BankQuestionServiceTests
{
    private readonly Guid _managerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _questionBankId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _otherBankId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _questionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _option1Id = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _option2Id = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private BankQuestionService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        return new BankQuestionService(
            _claimsService.Object,
            _db,
            NullLogger<BankQuestionService>.Instance);
    }

    private void SeedQuestionBank()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Course",
            ModuleId = _moduleId,
            IsDeleted = false,
        });
        _db.QuestionBanks.Seed(new QuestionBank
        {
            Id = _questionBankId,
            CourseId = _courseId,
            Name = "Bank",
            IsDeleted = false,
        });
    }

    private BankQuestion SeedQuestion(
        Guid? questionBankId = null,
        bool isDeleted = false,
        bool includeOptions = true)
    {
        var question = new BankQuestion
        {
            Id = _questionId,
            QuestionBankId = questionBankId ?? _questionBankId,
            QuestionText = "Sample question",
            QuestionType = "SingleChoice",
            Points = 1,
            DifficultyLevel = 1,
            OrderIndex = 1,
            IsDeleted = isDeleted,
        };

        if (includeOptions)
        {
            question.Options =
            [
                new BankQuestionOption
                {
                    Id = _option1Id,
                    BankQuestionId = _questionId,
                    OptionText = "A",
                    IsCorrect = true,
                    IsDeleted = false,
                },
                new BankQuestionOption
                {
                    Id = _option2Id,
                    BankQuestionId = _questionId,
                    OptionText = "B",
                    IsCorrect = false,
                    IsDeleted = false,
                },
            ];
        }

        _db.BankQuestions.Seed(question);
        return question;
    }

    [Fact]
    public async Task Delete_SoftDeletesQuestionAndOptions()
    {
        SeedQuestionBank();
        SeedQuestion();
        var sut = CreateSut();

        var deleted = await sut.DeleteBankQuestion(_questionBankId, _questionId);

        Assert.True(deleted);
        Assert.True(_db.BankQuestions.Items[0].IsDeleted);
        Assert.All(_db.BankQuestionOptions.Items, o => Assert.True(o.IsDeleted));
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Delete_SoftDeletesQuestion_WhenNoOptions()
    {
        SeedQuestionBank();
        SeedQuestion(includeOptions: false);
        var sut = CreateSut();

        var deleted = await sut.DeleteBankQuestion(_questionBankId, _questionId);

        Assert.True(deleted);
        Assert.True(_db.BankQuestions.Items[0].IsDeleted);
        Assert.Empty(_db.BankQuestionOptions.Items);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenQuestionMissing()
    {
        SeedQuestionBank();
        var sut = CreateSut();

        Assert.False(await sut.DeleteBankQuestion(_questionBankId, _questionId));
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenQuestionDeleted()
    {
        SeedQuestionBank();
        SeedQuestion(isDeleted: true);
        var sut = CreateSut();

        Assert.False(await sut.DeleteBankQuestion(_questionBankId, _questionId));
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenQuestionBelongsToOtherBank()
    {
        SeedQuestionBank();
        SeedQuestion(questionBankId: _otherBankId);
        var sut = CreateSut();

        Assert.False(await sut.DeleteBankQuestion(_questionBankId, _questionId));
    }
}
