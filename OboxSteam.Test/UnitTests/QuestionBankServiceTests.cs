using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.BankQuestionDTO;
using OboxSteam.Application.DTOs.QuestionBankDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class QuestionBankServiceTests
{
    private readonly Guid _managerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _otherCourseId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _questionBankId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _otherBankId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _questionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _assignmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICsvQuestionParserService> _csvParser = new();

    private QuestionBankService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        return new QuestionBankService(
            _claimsService.Object,
            _csvParser.Object,
            _db,
            NullLogger<QuestionBankService>.Instance);
    }

    private static Mock<IFormFile> CreateCsvFile(string fileName = "questions.csv", long length = 128)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream("csv"u8.ToArray()));
        return file;
    }

    private (Program Program, Module Module, Course Course) SeedCurriculum(
        Guid? courseId = null,
        string courseName = "Intro Course")
    {
        if (_db.Programs.Items.Count == 0)
        {
            _db.Programs.Seed(new Program
            {
                Id = _programId,
                Code = "PRG-001",
                Name = "STEAM Program",
                Category = ProgramCategory.Technology,
                Level = DifficultyLevel.Beginner,
                IsDeleted = false,
            });
        }

        if (_db.Modules.Items.Count == 0)
        {
            _db.Modules.Seed(new Module
            {
                Id = _moduleId,
                Code = "MOD-001",
                Name = "Module A",
                ProgramId = _programId,
                Program = _db.Programs.Items[0],
                ModuleType = ModuleType.Theory,
                ModuleOrder = 1,
                IsDeleted = false,
            });
        }

        var targetCourseId = courseId ?? _courseId;
        var existingCourse = _db.Courses.Items.FirstOrDefault(c => c.Id == targetCourseId);
        if (existingCourse != null)
            return (_db.Programs.Items[0], _db.Modules.Items[0], existingCourse);

        var course = new Course
        {
            Id = targetCourseId,
            Code = targetCourseId == _courseId ? "CRS-001" : "CRS-002",
            Name = courseName,
            ModuleId = _moduleId,
            Module = _db.Modules.Items[0],
            IsDeleted = false,
        };
        _db.Courses.Seed(course);
        return (_db.Programs.Items[0], _db.Modules.Items[0], course);
    }

    private QuestionBank SeedQuestionBank(
        Guid? id = null,
        string name = "Midterm Bank",
        Guid? courseId = null,
        string courseName = "Intro Course",
        List<BankQuestion>? questions = null,
        bool isDeleted = false)
    {
        var (_, _, course) = SeedCurriculum(courseId ?? _courseId, courseName);
        var bank = new QuestionBank
        {
            Id = id ?? _questionBankId,
            CourseId = course.Id,
            Course = course,
            Name = name,
            Description = "Bank desc",
            Questions = questions ?? [],
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            IsDeleted = isDeleted,
        };
        _db.QuestionBanks.Seed(bank);

        if (questions is { Count: > 0 })
            _db.BankQuestions.Seed(questions.ToArray());

        return bank;
    }

    private static CsvBankQuestionRowDto ValidSingleChoiceRow(
        int rowNumber = 2,
        string questionText = "What is 2+2?",
        string questionType = "singlechoice",
        string difficulty = "easy",
        decimal points = 1m)
    {
        return new CsvBankQuestionRowDto
        {
            RowNumber = rowNumber,
            QuestionText = questionText,
            QuestionType = questionType,
            Difficulty = difficulty,
            Points = points,
            Options =
            [
                new CsvBankQuestionOptionRowDto { OptionText = "3", IsCorrect = false },
                new CsvBankQuestionOptionRowDto { OptionText = "4", IsCorrect = true },
            ],
        };
    }

    // ── GetAllQuestionBanks ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPage()
    {
        SeedQuestionBank(name: "Alpha Bank");
        SeedQuestionBank(
            id: _otherBankId,
            name: "Beta Bank",
            courseId: _otherCourseId,
            courseName: "Advanced Course");
        var sut = CreateSut();

        var result = await sut.GetAllQuestionBanks(
            "alpha", "name", false, 1, 10, courseId: _courseId);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alpha Bank", result.Items[0].Name);
        Assert.Equal("Intro Course", result.Items[0].CourseName);
        Assert.Equal("Module A", result.Items[0].ModuleName);
        Assert.Equal("STEAM Program", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetAll_ReturnsEmpty_WhenNoBanks()
    {
        SeedCurriculum();
        var sut = CreateSut();

        var result = await sut.GetAllQuestionBanks(null, null, true, 1, 10);

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetAll_FiltersByModuleProgramAndHierarchySearch()
    {
        SeedQuestionBank(name: "Alpha Bank");
        SeedQuestionBank(
            id: _otherBankId,
            name: "Beta Bank",
            courseId: _otherCourseId,
            courseName: "Advanced Course");
        var sut = CreateSut();

        var byModule = await sut.GetAllQuestionBanks(
            null, null, false, 1, 10, moduleId: _moduleId);
        var byProgram = await sut.GetAllQuestionBanks(
            null, null, false, 1, 10, programId: _programId);
        var byCourseSearch = await sut.GetAllQuestionBanks(
            "advanced", null, false, 1, 10);

        Assert.Equal(2, byModule.TotalCount);
        Assert.Equal(2, byProgram.TotalCount);
        Assert.Single(byCourseSearch.Items);
        Assert.Equal("Beta Bank", byCourseSearch.Items[0].Name);
    }

    [Fact]
    public async Task GetAll_AppliesAlternateSortColumns()
    {
        var (_, _, course) = SeedCurriculum();
        var otherCourse = SeedCurriculum(_otherCourseId, "Zebra Course").Course;
        var earlyBank = SeedQuestionBank(
            id: _questionBankId,
            name: "Few Questions",
            courseId: course.Id,
            questions:
            [
                new BankQuestion
                {
                    Id = _questionId,
                    QuestionBankId = _questionBankId,
                    QuestionText = "Q1",
                    QuestionType = "SingleChoice",
                    Points = 1,
                    DifficultyLevel = 1,
                    OrderIndex = 1,
                    IsDeleted = false,
                },
            ]);
        earlyBank.CreatedAt = DateTime.UtcNow.AddDays(-10);
        earlyBank.UpdatedAt = DateTime.UtcNow.AddDays(-5);

        var lateBank = SeedQuestionBank(
            id: _otherBankId,
            name: "Many Questions",
            courseId: otherCourse.Id,
            questions:
            [
                new BankQuestion
                {
                    Id = Guid.Parse("67676767-6767-6767-6767-676767676767"),
                    QuestionBankId = _otherBankId,
                    QuestionText = "Q2",
                    QuestionType = "SingleChoice",
                    Points = 1,
                    DifficultyLevel = 1,
                    OrderIndex = 1,
                    IsDeleted = false,
                },
                new BankQuestion
                {
                    Id = Guid.Parse("68686868-6868-6868-6868-686868686868"),
                    QuestionBankId = _otherBankId,
                    QuestionText = "Q3",
                    QuestionType = "SingleChoice",
                    Points = 1,
                    DifficultyLevel = 1,
                    OrderIndex = 2,
                    IsDeleted = false,
                },
            ]);
        lateBank.CreatedAt = DateTime.UtcNow.AddDays(-1);
        lateBank.UpdatedAt = DateTime.UtcNow.AddDays(-1);

        var sut = CreateSut();

        var byQuestionCount = await sut.GetAllQuestionBanks(null, "questioncount", true, 1, 10);
        var byCourseName = await sut.GetAllQuestionBanks(null, "coursename", false, 1, 10);
        var byProgramName = await sut.GetAllQuestionBanks(null, "programname", true, 1, 10);
        var byUpdatedAt = await sut.GetAllQuestionBanks(null, "updatedat", false, 1, 10);
        var byCreatedAt = await sut.GetAllQuestionBanks(null, "createdat", true, 1, 10);

        Assert.Equal("Many Questions", byQuestionCount.Items[0].Name);
        Assert.Equal(2, byQuestionCount.Items[0].QuestionCount);
        Assert.Equal("Intro Course", byCourseName.Items[0].CourseName);
        Assert.Equal("STEAM Program", byProgramName.Items[0].ProgramName);
        Assert.Equal("Few Questions", byUpdatedAt.Items[0].Name);
        Assert.Equal("Many Questions", byCreatedAt.Items[0].Name);
    }

    [Fact]
    public async Task GetAll_UsesDefaultCreatedAtSortDescending()
    {
        var bankA = SeedQuestionBank(id: _questionBankId, name: "Older");
        bankA.CreatedAt = DateTime.UtcNow.AddDays(-3);
        var bankB = SeedQuestionBank(id: _otherBankId, name: "Newer");
        bankB.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var sut = CreateSut();

        var result = await sut.GetAllQuestionBanks(null, null, true, 1, 10);

        Assert.Equal("Newer", result.Items[0].Name);
        Assert.Equal("Older", result.Items[1].Name);
    }

    // ── CreateQuestionBank ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsQuestionBank()
    {
        SeedCurriculum();
        var sut = CreateSut();

        var result = await sut.CreateQuestionBank(new CreateQuestionBankRequestDto
        {
            CourseId = _courseId,
            Name = "  Final Bank  ",
            Description = "  Desc  ",
        });

        Assert.Equal("Final Bank", result.Name);
        Assert.Equal("Desc", result.Description);
        Assert.Equal(_courseId, result.CourseId);
        Assert.Single(_db.QuestionBanks.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Create_Throws_WhenNameMissing()
    {
        SeedCurriculum();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateQuestionBank(new CreateQuestionBankRequestDto
            {
                CourseId = _courseId,
                Name = "  ",
            }));
    }

    [Fact]
    public async Task Create_Throws_WhenCourseMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateQuestionBank(new CreateQuestionBankRequestDto
            {
                CourseId = _courseId,
                Name = "Bank",
            }));
    }

    [Fact]
    public async Task Create_Throws_WhenDuplicateNameInCourse()
    {
        SeedQuestionBank(name: "Shared Name");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateQuestionBank(new CreateQuestionBankRequestDto
            {
                CourseId = _courseId,
                Name = "shared name",
            }));
    }

    // ── GetQuestionBankById ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsBank()
    {
        SeedQuestionBank();
        var sut = CreateSut();

        var result = await sut.GetQuestionBankById(_questionBankId);

        Assert.NotNull(result);
        Assert.Equal("Midterm Bank", result!.Name);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenMissingOrDeleted()
    {
        SeedQuestionBank(isDeleted: true);
        var sut = CreateSut();

        Assert.Null(await sut.GetQuestionBankById(_questionBankId));
        Assert.Null(await sut.GetQuestionBankById(Guid.NewGuid()));
    }

    // ── DeleteQuestionBank ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesBankQuestionsAndOptions()
    {
        var optionId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var question = new BankQuestion
        {
            Id = _questionId,
            QuestionBankId = _questionBankId,
            QuestionText = "Q1",
            QuestionType = "SingleChoice",
            Points = 1,
            DifficultyLevel = 1,
            OrderIndex = 1,
            Options =
            [
                new BankQuestionOption
                {
                    Id = optionId,
                    BankQuestionId = _questionId,
                    OptionText = "A",
                    IsCorrect = true,
                    IsDeleted = false,
                },
            ],
            IsDeleted = false,
        };
        SeedQuestionBank(questions: [question]);
        var sut = CreateSut();

        var deleted = await sut.DeleteQuestionBank(_questionBankId);

        Assert.True(deleted);
        Assert.True(_db.QuestionBanks.Items[0].IsDeleted);
        Assert.True(_db.BankQuestions.Items[0].IsDeleted);
        Assert.True(_db.BankQuestions.Items[0].Options.First().IsDeleted);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenMissing()
    {
        var sut = CreateSut();

        Assert.False(await sut.DeleteQuestionBank(_questionBankId));
    }

    [Fact]
    public async Task Delete_SoftDeletesBankWithoutQuestions()
    {
        SeedQuestionBank();
        var sut = CreateSut();

        var deleted = await sut.DeleteQuestionBank(_questionBankId);

        Assert.True(deleted);
        Assert.True(_db.QuestionBanks.Items[0].IsDeleted);
        Assert.Empty(_db.BankQuestions.Items);
    }

    [Fact]
    public async Task Delete_Throws_WhenLinkedToAssignment()
    {
        SeedQuestionBank();
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-001",
            Title = "Quiz",
            ModuleId = _moduleId,
            AssignmentType = AssignmentType.Quiz,
            QuestionBankId = _questionBankId,
            MaxPoints = 10,
            PassScore = 5,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.DeleteQuestionBank(_questionBankId));
    }

    // ── ImportQuestionsFromCsv ──────────────────────────────────────────────────

    [Fact]
    public async Task Import_ImportsValidRows()
    {
        SeedQuestionBank();
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)
            [
                ValidSingleChoiceRow(),
                new CsvBankQuestionRowDto
                {
                    RowNumber = 3,
                    QuestionText = "Pick all even numbers",
                    QuestionType = "multichoice",
                    Difficulty = "medium",
                    Points = 2,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "2", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "3", IsCorrect = false },
                        new CsvBankQuestionOptionRowDto { OptionText = "4", IsCorrect = true },
                    ],
                },
            ]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, _db.BankQuestions.Items.Count);
        Assert.Equal(5, _db.BankQuestionOptions.Items.Count);
        Assert.Equal(1, _db.BankQuestions.Items[0].OrderIndex);
        Assert.Equal(2, _db.BankQuestions.Items[1].OrderIndex);
    }

    [Fact]
    public async Task Import_AppendsOrderIndex_AfterExistingQuestions()
    {
        SeedQuestionBank(questions:
        [
            new BankQuestion
            {
                Id = _questionId,
                QuestionBankId = _questionBankId,
                QuestionText = "Existing",
                QuestionType = "SingleChoice",
                Points = 1,
                DifficultyLevel = 1,
                OrderIndex = 3,
                IsDeleted = false,
            },
        ]);
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)[ValidSingleChoiceRow()]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(4, _db.BankQuestions.Items.Single(q => q.QuestionText == "What is 2+2?").OrderIndex);
    }

    [Fact]
    public async Task Import_ReportsSingleChoiceRuleViolation()
    {
        SeedQuestionBank();
        var row = ValidSingleChoiceRow();
        row.Options[1].IsCorrect = false;
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)[row]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains("exactly 1 correct", result.Errors[0].Error);
    }

    [Fact]
    public async Task Import_ReportsMultipleChoiceNeedsCorrectOption()
    {
        SeedQuestionBank();
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)
            [
                new CsvBankQuestionRowDto
                {
                    RowNumber = 2,
                    QuestionText = "Pick numbers",
                    QuestionType = "multichoice",
                    Difficulty = "hard",
                    Points = 2,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "1", IsCorrect = false },
                        new CsvBankQuestionOptionRowDto { OptionText = "2", IsCorrect = false },
                    ],
                },
            ]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(1, result.FailedCount);
        Assert.Contains("at least 1 correct", result.Errors[0].Error);
    }

    [Fact]
    public async Task Import_ReportsTrueFalseOptionRules()
    {
        SeedQuestionBank();
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)
            [
                new CsvBankQuestionRowDto
                {
                    RowNumber = 2,
                    QuestionText = "Sky is blue?",
                    QuestionType = "truefalse",
                    Difficulty = "easy",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "True", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "False", IsCorrect = false },
                        new CsvBankQuestionOptionRowDto { OptionText = "Maybe", IsCorrect = false },
                    ],
                },
            ]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(1, result.FailedCount);
        Assert.Contains("exactly 2 options", result.Errors[0].Error);
    }

    [Fact]
    public async Task Import_ReportsInvalidTypeDifficultyAndParseErrors()
    {
        SeedQuestionBank();
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)
            [
                new CsvBankQuestionRowDto
                {
                    RowNumber = 2,
                    QuestionText = "Bad type",
                    QuestionType = "essay",
                    Difficulty = "easy",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "B", IsCorrect = false },
                    ],
                },
                new CsvBankQuestionRowDto
                {
                    RowNumber = 3,
                    QuestionText = "Bad difficulty",
                    QuestionType = "singlechoice",
                    Difficulty = "insane",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "B", IsCorrect = false },
                    ],
                },
                new CsvBankQuestionRowDto
                {
                    RowNumber = 4,
                    QuestionText = "Broken row",
                    ParseErrors = ["Missing option columns"],
                },
            ]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(3, result.FailedCount);
        Assert.Contains("singlechoice", result.Errors[0].Error);
        Assert.Contains("easy, medium, or hard", result.Errors[1].Error);
        Assert.Contains("Missing option", result.Errors[2].Error);
    }

    [Fact]
    public async Task Import_ReportsRequiredFieldAndOptionCountErrors()
    {
        SeedQuestionBank();
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)
            [
                new CsvBankQuestionRowDto
                {
                    RowNumber = 2,
                    QuestionText = "  ",
                    QuestionType = "singlechoice",
                    Difficulty = "easy",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "B", IsCorrect = false },
                    ],
                },
                new CsvBankQuestionRowDto
                {
                    RowNumber = 3,
                    QuestionText = "Missing type",
                    QuestionType = " ",
                    Difficulty = "easy",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "B", IsCorrect = false },
                    ],
                },
                new CsvBankQuestionRowDto
                {
                    RowNumber = 4,
                    QuestionText = "Missing difficulty",
                    QuestionType = "singlechoice",
                    Difficulty = " ",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "B", IsCorrect = false },
                    ],
                },
                new CsvBankQuestionRowDto
                {
                    RowNumber = 5,
                    QuestionText = "No points",
                    QuestionType = "singlechoice",
                    Difficulty = "easy",
                    Points = 0,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "B", IsCorrect = false },
                    ],
                },
                new CsvBankQuestionRowDto
                {
                    RowNumber = 6,
                    QuestionText = "One option",
                    QuestionType = "singlechoice",
                    Difficulty = "easy",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                    ],
                },
            ]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(5, result.FailedCount);
        Assert.Contains("QuestionText is required", result.Errors[0].Error);
        Assert.Contains("QuestionType is required", result.Errors[1].Error);
        Assert.Contains("Difficulty is required", result.Errors[2].Error);
        Assert.Contains("Points must be greater than 0", result.Errors[3].Error);
        Assert.Contains("At least 2 options", result.Errors[4].Error);
    }

    [Fact]
    public async Task Import_ReportsTrueFalseMustHaveOneCorrect()
    {
        SeedQuestionBank();
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)
            [
                new CsvBankQuestionRowDto
                {
                    RowNumber = 2,
                    QuestionText = "Always true?",
                    QuestionType = "truefalse",
                    Difficulty = "easy",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "True", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "False", IsCorrect = true },
                    ],
                },
            ]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(1, result.FailedCount);
        Assert.Contains("exactly 1 correct", result.Errors[0].Error);
    }

    [Fact]
    public async Task Import_TruncatesLongQuestionTextInErrors()
    {
        SeedQuestionBank();
        var longText = new string('x', 120);
        _csvParser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CsvBankQuestionRowDto>)
            [
                new CsvBankQuestionRowDto
                {
                    RowNumber = 2,
                    QuestionText = longText,
                    QuestionType = "essay",
                    Difficulty = "easy",
                    Points = 1,
                    Options =
                    [
                        new CsvBankQuestionOptionRowDto { OptionText = "A", IsCorrect = true },
                        new CsvBankQuestionOptionRowDto { OptionText = "B", IsCorrect = false },
                    ],
                },
            ]);
        var sut = CreateSut();

        var result = await sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile().Object);

        Assert.Equal(83, result.Errors[0].QuestionText!.Length);
        Assert.EndsWith("...", result.Errors[0].QuestionText);
    }

    [Fact]
    public async Task Import_Throws_WhenFileInvalidOrBankMissing()
    {
        SeedQuestionBank();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile(length: 0).Object));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ImportQuestionsFromCsv(_questionBankId, CreateCsvFile(fileName: "bad.txt").Object));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ImportQuestionsFromCsv(_questionBankId, null!));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ImportQuestionsFromCsv(
                _questionBankId,
                CreateCsvFile(length: 5L * 1024 * 1024 + 1).Object));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.ImportQuestionsFromCsv(Guid.NewGuid(), CreateCsvFile().Object));
    }
}
