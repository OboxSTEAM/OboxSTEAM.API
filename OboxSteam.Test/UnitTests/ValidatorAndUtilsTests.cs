using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.QuizDTO;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ValidatorAndUtilsTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private static IConfiguration CreateJwtConfiguration(string? secret = "this-is-a-test-secret-key-32chars!") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SecretKey"] = secret,
                ["JWT:Issuer"] = "test",
                ["JWT:Audience"] = "test",
            })
            .Build();

    // ── ProgramEnrollmentValidator ────────────────────────────────────────────

    [Fact]
    public void ProgramEnrollmentValidator_ValidatesIdsPaginationAndEntities()
    {
        Assert.Throws<BadRequestException>(() => ProgramEnrollmentValidator.ValidateProgramIdRequired(Guid.Empty));
        Assert.Throws<BadRequestException>(() => ProgramEnrollmentValidator.ValidateStudentIdRequired(Guid.Empty));
        Assert.Throws<BadRequestException>(() => ProgramEnrollmentValidator.ValidatePagination(0, 10));

        Assert.Throws<NotFoundException>(() =>
            ProgramEnrollmentValidator.ValidateProgramExists(null, Guid.NewGuid()));
        Assert.Throws<ConflictException>(() =>
            ProgramEnrollmentValidator.ValidateNotAlreadyEnrolled(new ProgramEnrollment()));
        Assert.Throws<NotFoundException>(() =>
            ProgramEnrollmentValidator.ValidateStudentExists(null, Guid.NewGuid()));
        Assert.Throws<ForbiddenException>(() =>
            ProgramEnrollmentValidator.ValidateCanListProgramEnrollments(RoleType.Mentor));

        var program = ProgramEnrollmentValidator.ValidateProgramExists(
            new Program { Id = Guid.NewGuid(), IsDeleted = false },
            Guid.NewGuid());
        Assert.False(program.IsDeleted);
    }

    // ── ModuleEnrollmentValidator ─────────────────────────────────────────────

    [Fact]
    public void ModuleEnrollmentValidator_ValidatesStaticRules()
    {
        Assert.Throws<BadRequestException>(() =>
            ModuleEnrollmentValidator.ValidateProgramEnrollmentIdRequired(Guid.Empty));
        Assert.Throws<BadRequestException>(() => ModuleEnrollmentValidator.ValidateModuleIdRequired(Guid.Empty));
        Assert.Throws<NotFoundException>(() =>
            ModuleEnrollmentValidator.ValidateProgramEnrollmentExists(null, Guid.NewGuid()));
        Assert.Throws<ForbiddenException>(() =>
            ModuleEnrollmentValidator.ValidateProgramEnrollmentBelongsToStudent(
                new ProgramEnrollment { StudentId = Guid.NewGuid() },
                Guid.NewGuid()));
        Assert.Throws<BadRequestException>(() =>
            ModuleEnrollmentValidator.ValidateProgramEnrollmentActiveForEnroll(
                new ProgramEnrollment { Status = EnrollmentStatus.PendingPayment }));
        Assert.Throws<BadRequestException>(() =>
            ModuleEnrollmentValidator.ValidateModuleBelongsToProgram(
                new Module { ProgramId = Guid.NewGuid() },
                Guid.NewGuid()));
        Assert.Throws<ConflictException>(() =>
            ModuleEnrollmentValidator.ValidateNoActiveEnrollment(new ModuleEnrollment()));
        Assert.Throws<BadRequestException>(() =>
            ModuleEnrollmentValidator.ValidateRetakeEligibility(null, ModuleType.Experiential));
        Assert.Throws<BadRequestException>(() =>
            ModuleEnrollmentValidator.ValidateRetakeEligibility(
                new ModuleEnrollment(),
                ModuleType.Theory));
        Assert.NotNull(
            ModuleEnrollmentValidator.ValidateRetakeEligibility(
                new ModuleEnrollment(),
                ModuleType.Experiential));
        Assert.Throws<BadRequestException>(() =>
            ModuleEnrollmentValidator.ValidateProgramEnrollmentLink(null));
    }

    [Fact]
    public async Task ModuleEnrollmentValidator_ValidatePrerequisiteCompletedAsync_Throws_WhenMissing()
    {
        var studentId = Guid.NewGuid();
        var prereqId = Guid.NewGuid();
        var module = new Module { Id = Guid.NewGuid(), PrerequisiteModuleId = prereqId };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ModuleEnrollmentValidator.ValidatePrerequisiteCompletedAsync(_db, studentId, module));
    }

    [Fact]
    public async Task ModuleEnrollmentValidator_ValidatePrerequisiteCompletedAsync_Passes_WhenCompleted()
    {
        var studentId = Guid.NewGuid();
        var prereqId = Guid.NewGuid();
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ModuleId = prereqId,
            Status = EnrollmentStatus.Completed,
            IsDeleted = false,
        });

        await ModuleEnrollmentValidator.ValidatePrerequisiteCompletedAsync(
            _db,
            studentId,
            new Module { Id = Guid.NewGuid(), PrerequisiteModuleId = prereqId });
    }

    // ── ActivityValidator ─────────────────────────────────────────────────────

    [Fact]
    public void ActivityValidator_ReturnsAllowedTypesPerModule()
    {
        Assert.Contains(ActivityType.Offline, ActivityValidator.GetAllowedActivityTypes(ModuleType.Experiential));
        Assert.Throws<BadRequestException>(() =>
            ActivityValidator.GetAllowedActivityTypes((ModuleType)999));
        Assert.Throws<BadRequestException>(() =>
            ActivityValidator.ValidateActivityTypeForModule(ModuleType.Theory, ActivityType.Offline));
    }

    [Fact]
    public void ActivityValidator_ValidateTypeRules_CoversActivityKinds()
    {
        ActivityValidator.ValidateTypeRules(
            ActivityType.SelfPaced,
            durationMinutes: null,
            requireQrCheckin: false);

        Assert.Throws<BadRequestException>(() =>
            ActivityValidator.ValidateTypeRules(
                ActivityType.SelfPaced, 60, requireQrCheckin: false));

        Assert.Throws<BadRequestException>(() =>
            ActivityValidator.ValidateTypeRules(
                ActivityType.SelfPaced, null, requireQrCheckin: true));

        Assert.Throws<BadRequestException>(() =>
            ActivityValidator.ValidateTypeRules(
                ActivityType.LiveOnline, null, requireQrCheckin: false));

        Assert.Throws<BadRequestException>(() =>
            ActivityValidator.ValidateTypeRules(
                ActivityType.Offline, 0, requireQrCheckin: false));

        ActivityValidator.ValidateTypeRules(
            ActivityType.Offline,
            120,
            requireQrCheckin: true);

        Assert.Throws<BadRequestException>(() =>
            ActivityValidator.ValidateTypeRules(
                ActivityType.LiveOnline,
                60,
                requireQrCheckin: true));
    }

    // ── PortfolioSubdomainValidator ─────────────────────────────────────────

    [Theory]
    [InlineData("ab", "between 3 and 63")]
    [InlineData("-bad", "lowercase letters")]
    [InlineData("bad--name", "consecutive hyphens")]
    [InlineData("api", "reserved")]
    public void PortfolioSubdomainValidator_TryValidateFormat_RejectsInvalid(string value, string reasonPart)
    {
        var ok = PortfolioSubdomainValidator.TryValidateFormat(value, out var reason);

        Assert.False(ok);
        Assert.Contains(reasonPart, reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PortfolioSubdomainValidator_Normalize_TrimsAndLowercases()
    {
        Assert.Null(PortfolioSubdomainValidator.Normalize("   "));
        Assert.Equal("my-portfolio", PortfolioSubdomainValidator.Normalize(" My-Portfolio "));
        Assert.True(PortfolioSubdomainValidator.TryValidateFormat("my-portfolio", out _));
    }

    // ── EnrollmentAccessValidator ─────────────────────────────────────────────

    [Fact]
    public async Task EnrollmentAccessValidator_GetCurrentStudentForEnrollAsync_ValidatesRole()
    {
        var studentId = Guid.NewGuid();
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(studentId);
        _db.Users.Seed(new User
        {
            Id = studentId,
            Role = RoleType.Student,
            IsDeleted = false,
        });

        var user = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _db, _claimsService.Object, "forbidden");

        Assert.Equal(studentId, user.Id);

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(Guid.Empty);
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(_db, _claimsService.Object, "forbidden"));
    }

    [Fact]
    public async Task EnrollmentAccessValidator_EnsureCanViewEnrollmentAsync_AllowsOwnerParentAndManager()
    {
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(studentId);
        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _db, _claimsService.Object, studentId, "denied");

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(parentId);
        _db.Users.Seed(new User { Id = parentId, Role = RoleType.Parent, IsDeleted = false });
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            StudentId = studentId,
            IsVerified = true,
            IsDeleted = false,
        });
        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _db, _claimsService.Object, studentId, "denied");

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(managerId);
        _db.Users.Seed(new User { Id = managerId, Role = RoleType.Manager, IsDeleted = false });
        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _db, _claimsService.Object, studentId, "denied");
    }

    [Fact]
    public async Task EnrollmentAccessValidator_EnsureCanViewEnrollmentAsync_ForbidsUnverifiedParent()
    {
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(parentId);
        _db.Users.Seed(new User { Id = parentId, Role = RoleType.Parent, IsDeleted = false });
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            StudentId = studentId,
            IsVerified = false,
            IsDeleted = false,
        });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
                _db, _claimsService.Object, studentId, "denied"));
    }

    [Fact]
    public async Task EnrollmentAccessValidator_EnsureVerifiedParentOfAsync_ReturnsStudentAndLink()
    {
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(parentId);
        _db.Users.Seed(new User
        {
            Id = parentId,
            Role = RoleType.Parent,
            Email = "parent@test.com",
            IsDeleted = false,
        });
        _db.Users.Seed(new User
        {
            Id = studentId,
            Role = RoleType.Student,
            Email = "student@test.com",
            IsDeleted = false,
        });
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            StudentId = studentId,
            IsVerified = true,
            IsDeleted = false,
        });

        var (student, link) = await EnrollmentAccessValidator.EnsureVerifiedParentOfAsync(
            _db,
            _claimsService.Object,
            studentId);

        Assert.Equal(studentId, student.Id);
        Assert.True(link.IsVerified);
    }

    [Fact]
    public async Task EnrollmentAccessValidator_EnsureCanViewEnrollmentAsync_ForbidsUnlinkedParentAndMentor()
    {
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(parentId);
        _db.Users.Seed(new User { Id = parentId, Role = RoleType.Parent, IsDeleted = false });
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
                _db, _claimsService.Object, studentId, "denied"));

        _claimsService.Setup(c => c.GetCurrentUserId).Returns(mentorId);
        _db.Users.Seed(new User { Id = mentorId, Role = RoleType.Mentor, IsDeleted = false });
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
                _db, _claimsService.Object, studentId, "denied"));
    }

    // ── ResearchSubmissionValidator ───────────────────────────────────────────

    [Fact]
    public void ResearchSubmissionValidator_ValidatesContentAvailabilityAndFiles()
    {
        Assert.Throws<BadRequestException>(() =>
            ResearchSubmissionValidator.ValidateSubmitContent(new CreateResearchSubmissionRequestDto()));

        ResearchSubmissionValidator.ValidateSubmitContent(new CreateResearchSubmissionRequestDto
        {
            ContentText = "hello",
        });

        Assert.Throws<ForbiddenException>(() =>
            ResearchSubmissionValidator.ValidateAssignmentAvailability(
                new Assignment { AvailableFrom = FixedNow.AddHours(1) },
                FixedNow));
        Assert.Throws<ConflictException>(() =>
            ResearchSubmissionValidator.ValidateAssignmentAvailability(
                new Assignment { AvailableUntil = FixedNow.AddHours(-1) },
                FixedNow));

        var pdf = CreateFormFile("paper.pdf", 1024);
        ResearchSubmissionValidator.ValidateUploadFile(pdf.Object);

        var badExt = CreateFormFile("virus.exe", 100);
        Assert.Throws<BadRequestException>(() => ResearchSubmissionValidator.ValidateUploadFile(badExt.Object));

        var hugeImage = CreateFormFile("photo.jpg", 11L * 1024 * 1024);
        Assert.Throws<BadRequestException>(() => ResearchSubmissionValidator.ValidateUploadFile(hugeImage.Object));
    }

    [Fact]
    public void ResearchSubmissionValidator_ValidatesSubmissionEntity()
    {
        Assert.Throws<NotFoundException>(() =>
            ResearchSubmissionValidator.ValidateSubmissionExists(null, Guid.NewGuid()));
        Assert.Throws<BadRequestException>(() =>
            ResearchSubmissionValidator.ValidateResearchSubmission(new Submission()));
    }

    private static Mock<IFormFile> CreateFormFile(string name, long length)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(name);
        file.Setup(f => f.Length).Returns(length);
        return file;
    }

    // ── ClassSessionValidator ─────────────────────────────────────────────────

    [Fact]
    public void ClassSessionValidator_ValidatesCreateAndReferences()
    {
        Assert.Throws<BadRequestException>(() => ClassSessionValidator.ValidatePagination(0, 10));
        Assert.Throws<NotFoundException>(() =>
            ClassSessionValidator.ValidateClassSessionExists(null, Guid.NewGuid()));
        Assert.Throws<BadRequestException>(() =>
            ClassSessionValidator.ValidateExactlyOneCurriculumItem(null, null));
        Assert.Throws<BadRequestException>(() =>
            ClassSessionValidator.ValidateExactlyOneCurriculumItem(Guid.NewGuid(), Guid.NewGuid()));
    }

    // ── Utils ─────────────────────────────────────────────────────────────────

    [Fact]
    public void JwtUtils_GenerateToken_Throws_WhenSecretTooShort()
    {
        Assert.Throws<ArgumentException>(() =>
            JwtUtils.GenerateJwtToken(
                Guid.NewGuid(),
                "a@test.com",
                "Student",
                CreateJwtConfiguration("short"),
                TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void JwtUtils_GenerateToken_ReturnsSignedToken()
    {
        var token = JwtUtils.GenerateJwtToken(
            Guid.NewGuid(),
            "a@test.com",
            "Student",
            CreateJwtConfiguration(),
            TimeSpan.FromMinutes(5));

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void TokenTools_GenerateRefreshToken_ReturnsBase64()
    {
        var token = TokenTools.GenerateRefreshToken();
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Theory]
    [InlineData("easy", 1)]
    [InlineData("MEDIUM", 3)]
    [InlineData("hard", 5)]
    [InlineData("", 0)]
    [InlineData("unknown", 0)]
    public void DifficultyLevelMapper_TryMapFromCsv(string raw, int expected)
    {
        var ok = DifficultyLevelMapper.TryMapFromCsv(raw, out var level);
        if (expected == 0)
        {
            Assert.False(ok);
        }
        else
        {
            Assert.True(ok);
            Assert.Equal(expected, level);
        }
    }

    [Fact]
    public void CurriculumStatusHelper_FindNewlyUnlockedModuleIds_ReturnsDependents()
    {
        var completedModuleId = Guid.NewGuid();
        var childModuleId = Guid.NewGuid();
        var snapshot = new ProgramCurriculumTreeSnapshot
        {
            Modules =
            [
                new Module { Id = childModuleId, PrerequisiteModuleId = completedModuleId },
            ],
        };
        var enrollments = new Dictionary<Guid, ModuleEnrollment>
        {
            [completedModuleId] = new()
            {
                ModuleId = completedModuleId,
                ProgressPercent = 100m,
            },
        };

        var unlocked = CurriculumStatusHelper.FindNewlyUnlockedModuleIds(
            snapshot,
            completedModuleId,
            enrollments,
            snapshot.Modules.ToDictionary(m => m.Id));

        Assert.Contains(childModuleId, unlocked);
        Assert.Empty(CurriculumStatusHelper.FindNewlyUnlockedModuleIds(
            snapshot,
            Guid.NewGuid(),
            enrollments,
            snapshot.Modules.ToDictionary(m => m.Id)));
    }

    [Fact]
    public void CurriculumStatusHelper_ResearchMilestoneAssignment_RequiresPreviousPass()
    {
        var milestoneId = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var snapshot = new ProgramCurriculumTreeSnapshot
        {
            LinksByMilestoneId = new Dictionary<Guid, List<ResearchMilestoneActivity>>
            {
                [milestoneId] =
                [
                    new ResearchMilestoneActivity
                    {
                        ActivityId = activityId,
                        IsRequiredForSubmission = true,
                    },
                ],
            },
            AssignmentsById = new Dictionary<Guid, Assignment>
            {
                [assignmentId] = new Assignment { Id = assignmentId },
            },
        };

        Assert.False(CurriculumStatusHelper.IsResearchMilestoneAssignmentAccessible(
            new ResearchMilestone { Id = milestoneId },
            new ResearchMilestone { Id = previousId },
            snapshot,
            new Dictionary<Guid, Submission>(),
            _ => true));

        Assert.False(CurriculumStatusHelper.IsResearchMilestoneAssignmentAccessible(
            new ResearchMilestone { Id = milestoneId },
            null,
            snapshot,
            new Dictionary<Guid, Submission>(),
            _ => false));
    }

    // ── AssignmentValidator ───────────────────────────────────────────────────

    [Fact]
    public void AssignmentValidator_ValidatesFieldsAndQuizConfig()
    {
        Assert.Throws<BadRequestException>(() => AssignmentValidator.ValidateRequiredFields("", "title"));
        Assert.Throws<BadRequestException>(() =>
            AssignmentValidator.ValidateCommonFields(0, 0, 0, -1));
        Assert.Throws<NotFoundException>(() => AssignmentValidator.ValidateModuleExists(null));
        Assert.Throws<ConflictException>(() => AssignmentValidator.ValidateCanDelete(1));
        Assert.Throws<BadRequestException>(() =>
            AssignmentValidator.ValidateDifficultyPercents(10, 10, 10));
        Assert.Throws<BadRequestException>(() =>
            AssignmentValidator.ValidateCourseBelongsToModule(
                new Course { ModuleId = Guid.NewGuid(), IsDeleted = false },
                Guid.NewGuid(),
                Guid.NewGuid()));
    }

    [Fact]
    public async Task AssignmentValidator_ValidateQuizConfigAsync_CoversBranches()
    {
        var moduleId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var bankId = Guid.NewGuid();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            AssignmentValidator.ValidateQuizConfigAsync(
                _db, AssignmentType.FileUpload, Guid.NewGuid(), null, moduleId, 0, 0, 0, null));

        _db.Courses.Seed(new Course { Id = courseId, ModuleId = moduleId, IsDeleted = false });
        _db.QuestionBanks.Seed(new QuestionBank { Id = bankId, CourseId = courseId, IsDeleted = false });
        _db.BankQuestions.Seed(new BankQuestion { Id = Guid.NewGuid(), QuestionBankId = bankId, IsDeleted = false });

        await AssignmentValidator.ValidateQuizConfigAsync(
            _db, AssignmentType.Quiz, bankId, courseId, moduleId, 50, 30, 20, 1);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            AssignmentValidator.ValidateQuizConfigAsync(
                _db, AssignmentType.Quiz, bankId, Guid.NewGuid(), moduleId, 50, 30, 20, 1));
    }

    // ── ClassValidator ────────────────────────────────────────────────────────

    [Fact]
    public void ClassValidator_ValidatesCreateUpdateAndTransitions()
    {
        Assert.Throws<BadRequestException>(() => ClassValidator.ValidatePagination(0, 10));
        Assert.Throws<NotFoundException>(() => ClassValidator.ValidateClassExists(null, Guid.NewGuid()));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateMentorExists(new User { Role = RoleType.Student, IsDeleted = false }, Guid.NewGuid()));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateDateRange(FixedNow, FixedNow));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateCapacityNotBelowEnrollment(1, 5));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateStatusTransition(ClassStatus.Draft, ClassStatus.Completed));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateNotUpdatingStatusViaPatch(ClassStatus.Open));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateDeletableStatus(new Class { Status = ClassStatus.InProgress }));

        var openClass = new Class
        {
            Status = ClassStatus.Open,
            StartDate = FixedNow.AddDays(-1),
            EndDate = FixedNow.AddDays(30),
            MaxCapacity = 10,
            MinHoursBeforeAssignmentJoin = 0,
        };
        Assert.True(ClassValidator.IsReadyForAutoStart(openClass, 10, FixedNow));
        Assert.False(ClassValidator.IsReadyForAutoStart(openClass, 5, FixedNow));
    }

    // ── ResearchSubmissionValidator (more branches) ───────────────────────────

    [Fact]
    public void ResearchSubmissionValidator_ValidatesOwnershipOpenStateAndVideoSize()
    {
        var studentId = Guid.NewGuid();
        var submission = new Submission { StudentId = studentId, Status = SubmissionStatus.Graded };

        Assert.Throws<ForbiddenException>(() =>
            ResearchSubmissionValidator.ValidateSubmissionOwnership(submission, Guid.NewGuid()));
        Assert.Throws<ConflictException>(() =>
            ResearchSubmissionValidator.ValidateSubmissionOpenForSubmit(submission));

        var video = CreateFormFile("clip.mp4", 4L * 1024 * 1024 * 1024);
        Assert.Throws<BadRequestException>(() => ResearchSubmissionValidator.ValidateUploadFile(video.Object));

        ResearchSubmissionValidator.ValidateSubmitContent(new CreateResearchSubmissionRequestDto
        {
            EvidenceMediaAssetIds = [Guid.NewGuid()],
        });

        ResearchSubmissionValidator.ValidateEvidenceUploadFile(CreateFormFile("clip.mp4", 1024).Object);
        Assert.Throws<BadRequestException>(() =>
            ResearchSubmissionValidator.ValidateEvidenceUploadFile(CreateFormFile("paper.pdf", 1024).Object));
    }

    [Fact]
    public void ResearchSubmissionValidator_EvaluateStudentSubmitEligibility_CoversStatusBranches()
    {
        var assignment = new Assignment { MaxAttempts = 2 };
        var now = FixedNow;

        var locked = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            false, ["Activity incomplete"], assignment, null, now);
        Assert.False(locked.CanSubmit);
        Assert.Contains("Milestone is locked.", locked.SubmitBlockReasons);
        Assert.Contains("Activity incomplete", locked.SubmitBlockReasons);
        Assert.DoesNotContain(locked.SubmitBlockReasons, r => r.Contains("Mentor has not opened", StringComparison.Ordinal));

        var unlockedNoSubmission = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true, [], assignment, null, now);
        Assert.True(unlockedNoSubmission.CanSubmit);

        var pending = new Submission { Status = SubmissionStatus.Pending, AttemptNumber = 1 };
        var canSubmit = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true, [], assignment, pending, now);
        Assert.True(canSubmit.CanSubmit);

        var turnedIn = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true, [], assignment, new Submission { Status = SubmissionStatus.TurnedIn }, now);
        Assert.Contains("pending mentor review", turnedIn.SubmitBlockReasons[0]);

        var graded = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true, [], assignment, new Submission { Status = SubmissionStatus.Graded }, now);
        Assert.Contains("already been graded", graded.SubmitBlockReasons[0]);

        var maxAttempts = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true,
            [],
            assignment,
            new Submission { Status = SubmissionStatus.ReturnedForRevision, AttemptNumber = 2 },
            now);
        Assert.Contains("Maximum number of attempts", maxAttempts.SubmitBlockReasons[0]);

        var notYetAvailable = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true,
            [],
            new Assignment { MaxAttempts = 2, AvailableFrom = now.AddHours(1) },
            pending,
            now);
        Assert.Contains(notYetAvailable.SubmitBlockReasons, r => r.Contains("not yet available", StringComparison.OrdinalIgnoreCase));

        var expiredWithoutPersonal = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true,
            [],
            new Assignment { MaxAttempts = 2, AvailableUntil = now.AddHours(-1) },
            null,
            now);
        Assert.False(expiredWithoutPersonal.CanSubmit);
        Assert.Contains("Assignment is no longer available.", expiredWithoutPersonal.SubmitBlockReasons);

        var expiredWithPersonal = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
            true,
            [],
            new Assignment { MaxAttempts = 2, AvailableUntil = now.AddHours(-1) },
            null,
            now,
            personalAvailableUntil: now.AddDays(1));
        Assert.True(expiredWithPersonal.CanSubmit);
    }

    // ── ProgramCurriculumTreeMapper ───────────────────────────────────────────

    [Fact]
    public void ProgramCurriculumTreeMapper_MapsResearchAndTheoryModules()
    {
        var programId = Guid.NewGuid();
        var theoryModuleId = Guid.NewGuid();
        var researchModuleId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var linkActivityId = Guid.NewGuid();

        var snapshot = new ProgramCurriculumTreeSnapshot
        {
            Program = new Program { Id = programId, Name = "STEM" },
            Modules =
            [
                new Module { Id = theoryModuleId, Name = "Theory", ModuleType = ModuleType.Theory, ModuleOrder = 1 },
                new Module { Id = researchModuleId, Name = "Capstone", ModuleType = ModuleType.Research, ModuleOrder = 2 },
            ],
            CoursesByModuleId = new Dictionary<Guid, List<Course>>
            {
                [theoryModuleId] = [new Course { Id = courseId, Name = "Intro" }],
            },
            ActivitiesByCourseId = new Dictionary<Guid, List<Activity>>
            {
                [courseId] = [new Activity { Id = activityId, Name = "Watch", ActivityOrder = 1, ActivityType = ActivityType.SelfPaced }],
            },
            MaterialsByActivityId = new Dictionary<Guid, Material>
            {
                [activityId] = new Material { Id = Guid.NewGuid(), ActivityId = activityId, Title = "Slides" },
            },
            MilestonesByModuleId = new Dictionary<Guid, List<ResearchMilestone>>
            {
                [researchModuleId] = [new ResearchMilestone { Id = milestoneId, Title = "M1", MilestoneOrder = 1 }],
            },
            LinksByMilestoneId = new Dictionary<Guid, List<ResearchMilestoneActivity>>
            {
                [milestoneId] =
                [
                    new ResearchMilestoneActivity { ActivityId = linkActivityId, ResearchMilestoneId = milestoneId },
                ],
            },
            ActivitiesById = new Dictionary<Guid, Activity>
            {
                [linkActivityId] = new Activity { Id = linkActivityId, Name = "Lab", ActivityOrder = 1, ActivityType = ActivityType.Offline },
            },
        };

        var dto = ProgramCurriculumTreeMapper.ToProgramCurriculumDto(snapshot);

        Assert.Equal(2, dto.Modules.Count);
        Assert.Single(dto.Modules[0].Courses!);
        Assert.Single(dto.Modules[0].Courses![0].Activities);
        Assert.Single(dto.Modules[1].Milestones!);
        Assert.Single(dto.Modules[1].Milestones![0].Activities);
    }

    // ── MentorScopeValidator / ClassEnrollmentValidator ───────────────────────

    [Fact]
    public async Task MentorScopeValidator_EnforcesProgramAndClassOwnership()
    {
        var mentorId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();

        _db.Classes.Seed(new Class
        {
            Id = classId,
            MentorId = mentorId,
            ProgramId = programId,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module { Id = moduleId, ProgramId = programId, IsDeleted = false });

        await MentorScopeValidator.EnsureMentorOwnsProgramAsync(_db, mentorId, programId);
        var ownedClass = await MentorScopeValidator.EnsureMentorOwnsClassAsync(_db, mentorId, classId);
        Assert.Equal(classId, ownedClass.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            MentorScopeValidator.EnsureMentorOwnsProgramAsync(_db, Guid.NewGuid(), programId));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            MentorScopeValidator.EnsureMentorOwnsClassAsync(_db, Guid.NewGuid(), classId));
    }

    [Fact]
    public void ClassEnrollmentValidator_ValidatesEnrollmentRules()
    {
        Assert.Throws<BadRequestException>(() =>
            ClassEnrollmentValidator.ValidateProgramEnrollmentIdRequired(Guid.Empty));
        Assert.Throws<BadRequestException>(() =>
            ClassEnrollmentValidator.ValidateClassIdRequired(Guid.Empty));
        Assert.Throws<NotFoundException>(() =>
            ClassEnrollmentValidator.ValidateProgramEnrollmentExists(null, Guid.NewGuid()));
        Assert.Throws<ForbiddenException>(() =>
            ClassEnrollmentValidator.ValidateProgramEnrollmentBelongsToStudent(
                new ProgramEnrollment { StudentId = Guid.NewGuid() },
                Guid.NewGuid()));
        Assert.Throws<BadRequestException>(() =>
            ClassEnrollmentValidator.ValidateProgramEnrollmentActiveForEnroll(
                new ProgramEnrollment { Status = EnrollmentStatus.PendingPayment }));
        Assert.Throws<BadRequestException>(() =>
            ClassEnrollmentValidator.ValidateClassBelongsToProgram(
                new Class { ProgramId = Guid.NewGuid() },
                Guid.NewGuid()));
        Assert.Throws<BadRequestException>(() =>
            ClassEnrollmentValidator.ValidateClassOpenForEnrollment(
                new Class { Status = ClassStatus.Draft }));
        Assert.Throws<BadRequestException>(() =>
            ClassEnrollmentValidator.ValidateClassOpenForEnrollment(
                new Class { Status = ClassStatus.ReadyForMentor }));
        Assert.Throws<BadRequestException>(() =>
            ClassEnrollmentValidator.ValidateClassOpenForEnrollment(
                new Class { Status = ClassStatus.InProgress }));
        ClassEnrollmentValidator.ValidateClassOpenForEnrollment(
            new Class { Status = ClassStatus.Open });
    }

    // ── QuizAttemptValidator ──────────────────────────────────────────────────

    [Fact]
    public void QuizAttemptValidator_ValidateAssignmentForQuizStart_CoversAllBranches()
    {
        Assert.Throws<NotFoundException>(() =>
            QuizAttemptValidator.ValidateAssignmentForQuizStart(null));
        Assert.Throws<NotFoundException>(() =>
            QuizAttemptValidator.ValidateAssignmentForQuizStart(new Assignment { IsDeleted = true }));
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateAssignmentForQuizStart(
                new Assignment { AssignmentType = AssignmentType.FileUpload }));
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateAssignmentForQuizStart(
                new Assignment { AssignmentType = AssignmentType.Quiz, QuestionBankId = null }));

        var valid = QuizAttemptValidator.ValidateAssignmentForQuizStart(
            new Assignment { AssignmentType = AssignmentType.Quiz, QuestionBankId = Guid.NewGuid() });
        Assert.NotNull(valid);
    }

    [Fact]
    public void QuizAttemptValidator_ValidateAssignmentAvailability_CoversAllBranches()
    {
        var now = DateTime.UtcNow;

        // Available from in the future
        Assert.Throws<ForbiddenException>(() =>
            QuizAttemptValidator.ValidateAssignmentAvailability(
                new Assignment { AvailableFrom = now.AddDays(1) }, now));

        // Available until in the past
        Assert.Throws<ConflictException>(() =>
            QuizAttemptValidator.ValidateAssignmentAvailability(
                new Assignment { AvailableUntil = now.AddDays(-1) }, now));

        // Due date in the past
        Assert.Throws<ConflictException>(() =>
            QuizAttemptValidator.ValidateAssignmentAvailability(
                new Assignment { DueDate = now.AddDays(-1) }, now));

        // No constraints - passes
        QuizAttemptValidator.ValidateAssignmentAvailability(new Assignment(), now);
    }

    [Fact]
    public void QuizAttemptValidator_ValidateBankQuestionsForDraw_CoversAllBranches()
    {
        var assignment = new Assignment { QuestionCount = 2 };
        var empty = Array.Empty<BankQuestion>();
        var one = new[] { new BankQuestion { Id = Guid.NewGuid() } };
        var two = new BankQuestion[]
        {
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() },
        };

        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateBankQuestionsForDraw(assignment, empty));
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateBankQuestionsForDraw(assignment, one));

        // Valid - passes
        QuizAttemptValidator.ValidateBankQuestionsForDraw(assignment, two);

        // QuestionCount = null → uses bankQuestions.Count
        QuizAttemptValidator.ValidateBankQuestionsForDraw(
            new Assignment { QuestionCount = null }, one);

        // QuestionCount = 0
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateBankQuestionsForDraw(
                new Assignment { QuestionCount = 0 }, one));
    }

    [Fact]
    public void QuizAttemptValidator_ValidateSaveDraftAndSubmit()
    {
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateSaveDraftRequest(null));
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateSubmitRequest(null));

        // Valid - Answers defaults to []
        QuizAttemptValidator.ValidateSaveDraftRequest(new SaveDraftAnswersRequestDto());
        QuizAttemptValidator.ValidateSubmitRequest(new SubmitQuizAnswersRequestDto());
    }

    [Fact]
    public void QuizAttemptValidator_ValidateSubmissionStates()
    {
        QuizAttemptValidator.ValidateAssignmentIdRequired(Guid.NewGuid());
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateAssignmentIdRequired(Guid.Empty));

        var graded = new Submission { Status = SubmissionStatus.Graded };
        var pending = new Submission { Status = SubmissionStatus.Pending };

        QuizAttemptValidator.ValidateSubmissionPending(pending);
        Assert.Throws<ConflictException>(() =>
            QuizAttemptValidator.ValidateSubmissionPending(graded));

        QuizAttemptValidator.ValidateSubmissionGraded(graded);
        Assert.Throws<ConflictException>(() =>
            QuizAttemptValidator.ValidateSubmissionGraded(pending));

        QuizAttemptValidator.ValidateSubmissionExists(pending, Guid.NewGuid());
        Assert.Throws<NotFoundException>(() =>
            QuizAttemptValidator.ValidateSubmissionExists(null, Guid.NewGuid()));

        QuizAttemptValidator.ValidateSubmissionHasQuizSnapshot(
            new[] { new QuizQuestion { Id = Guid.NewGuid() } });
        Assert.Throws<BadRequestException>(() =>
            QuizAttemptValidator.ValidateSubmissionHasQuizSnapshot(
                Array.Empty<QuizQuestion>()));

        var sid = Guid.NewGuid();
        QuizAttemptValidator.ValidateSubmissionOwnership(
            new Submission { StudentId = sid }, sid);
        Assert.Throws<ForbiddenException>(() =>
            QuizAttemptValidator.ValidateSubmissionOwnership(
                new Submission { StudentId = Guid.NewGuid() }, Guid.NewGuid()));
    }

    // ── ClassValidator additional branch tests ────────────────────────────────

    [Fact]
    public void ClassValidator_StatusTransition_CoversAllBranches()
    {
        ClassValidator.ValidateStatusTransition(ClassStatus.Draft, ClassStatus.ReadyForMentor);
        ClassValidator.ValidateStatusTransition(ClassStatus.ReadyForMentor, ClassStatus.Open);
        ClassValidator.ValidateStatusTransition(ClassStatus.Open, ClassStatus.InProgress);
        ClassValidator.ValidateStatusTransition(ClassStatus.InProgress, ClassStatus.Completed);

        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateStatusTransition(ClassStatus.Draft, ClassStatus.Open));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateStatusTransition(ClassStatus.Draft, ClassStatus.Completed));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateStatusTransition(ClassStatus.Completed, ClassStatus.Draft));
    }

    [Fact]
    public void ClassValidator_IsReadyForAutoStart_CoversAllBranches()
    {
        var now = DateTime.UtcNow;
        var cls = new Class
        {
            Status = ClassStatus.Open,
            MaxCapacity = 10,
            StartDate = now.AddHours(-1),
        };

        Assert.True(ClassValidator.IsReadyForAutoStart(cls, 10, now));
        Assert.False(ClassValidator.IsReadyForAutoStart(cls, 9, now));
        Assert.False(ClassValidator.IsReadyForAutoStart(cls, 10, now.AddHours(-2)));

        cls.Status = ClassStatus.Draft;
        Assert.False(ClassValidator.IsReadyForAutoStart(cls, 10, now));
    }

    [Fact]
    public void ClassValidator_ValidateCreateRequest_CoversAllBranches()
    {
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateCreateRequest(new CreateClassRequestDto
            {
                Code = "", Name = "N", ProgramId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
                MaxCapacity = 1
            }));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateCreateRequest(new CreateClassRequestDto
            {
                Code = "C", Name = "", ProgramId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
                MaxCapacity = 1
            }));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateCreateRequest(new CreateClassRequestDto
            {
                Code = "C", Name = "N", ProgramId = Guid.Empty,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
                MaxCapacity = 1
            }));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateCreateRequest(new CreateClassRequestDto
            {
                Code = "C", Name = "N", ProgramId = Guid.NewGuid(),
                MentorId = Guid.Empty,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
                MaxCapacity = 1
            }));

        // Lead time: StartDate must be at least 14 days out to leave an enrollment window.
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateCreateRequest(new CreateClassRequestDto
            {
                Code = "C", Name = "N", ProgramId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow.AddDays(7), EndDate = DateTime.UtcNow.AddDays(30),
                MaxCapacity = 1
            }));

        // Valid
        ClassValidator.ValidateCreateRequest(new CreateClassRequestDto
        {
            Code = "C", Name = "N", ProgramId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddDays(21), EndDate = DateTime.UtcNow.AddDays(60),
            MaxCapacity = 1
        });
    }

    [Fact]
    public void ClassValidator_ValidateDeletableStatus_CoversAllBranches()
    {
        ClassValidator.ValidateDeletableStatus(new Class { Status = ClassStatus.Draft });
        ClassValidator.ValidateDeletableStatus(new Class { Status = ClassStatus.ReadyForMentor });
        ClassValidator.ValidateDeletableStatus(new Class { Status = ClassStatus.Open });
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateDeletableStatus(new Class { Status = ClassStatus.InProgress }));
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateDeletableStatus(new Class { Status = ClassStatus.Completed }));
    }

    [Fact]
    public void ClassValidator_ValidateOpenClassHasNoActiveStudents()
    {
        ClassValidator.ValidateOpenClassHasNoActiveStudents(
            new Class { Status = ClassStatus.Open }, 0);
        Assert.Throws<ConflictException>(() =>
            ClassValidator.ValidateOpenClassHasNoActiveStudents(
                new Class { Status = ClassStatus.Open }, 1));
        // Draft status - no check
        ClassValidator.ValidateOpenClassHasNoActiveStudents(
            new Class { Status = ClassStatus.Draft }, 5);
    }

    [Fact]
    public void ClassValidator_ValidateCapacityNotBelowEnrollment()
    {
        ClassValidator.ValidateCapacityNotBelowEnrollment(10, 5);
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateCapacityNotBelowEnrollment(5, 10));
    }

    [Fact]
    public void ClassValidator_ValidateMentorExists_CoversRoleBranches()
    {
        ClassValidator.ValidateMentorExists(
            new User { Role = RoleType.Mentor }, Guid.NewGuid());
        ClassValidator.ValidateMentorExists(
            new User { Role = RoleType.Manager }, Guid.NewGuid());
        ClassValidator.ValidateMentorExists(
            new User { Role = RoleType.Admin }, Guid.NewGuid());
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateMentorExists(
                new User { Role = RoleType.Student }, Guid.NewGuid()));
        Assert.Throws<NotFoundException>(() =>
            ClassValidator.ValidateMentorExists(null, Guid.NewGuid()));
        Assert.Throws<NotFoundException>(() =>
            ClassValidator.ValidateMentorExists(
                new User { IsDeleted = true }, Guid.NewGuid()));
    }

    [Fact]
    public void ClassValidator_ValidateNotUpdatingStatusViaPatch()
    {
        ClassValidator.ValidateNotUpdatingStatusViaPatch(null);
        Assert.Throws<BadRequestException>(() =>
            ClassValidator.ValidateNotUpdatingStatusViaPatch(ClassStatus.Open));
    }

    [Fact]
    public void ClassValidator_ValidateTransitionToStatus_ReadyForMentorAndOpen()
    {
        var cls = new Class
        {
            Status = ClassStatus.Draft,
            StartDate = DateTime.UtcNow.AddDays(7),
            EndDate = DateTime.UtcNow.AddDays(30),
            MaxCapacity = 10,
            MinHoursBeforeAssignmentJoin = 0,
        };
        ClassValidator.ValidateTransitionToStatus(cls, Guid.NewGuid(), ClassStatus.ReadyForMentor);

        cls.Status = ClassStatus.ReadyForMentor;
        ClassValidator.ValidateTransitionToStatus(cls, Guid.NewGuid(), ClassStatus.Open);
    }
}
