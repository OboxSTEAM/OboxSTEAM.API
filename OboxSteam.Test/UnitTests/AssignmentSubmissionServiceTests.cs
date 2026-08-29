using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.AssignmentSubmissionDTO;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AssignmentSubmissionServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _managerId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _mentorId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _otherStudentId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _moduleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _assignmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _enrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _programEnrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _submissionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _classId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _classEnrollmentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<ICertificateService> _certificateService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private AssignmentSubmissionService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(DateTime.UtcNow);
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/submissions/file.pdf");
        _certificateService
            .Setup(c => c.EnsureProgramCertificateInternalAsync(It.IsAny<Guid>()))
            .ReturnsAsync((CertificateDetailDto?)null);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<OboxSteam.Application.Notifications.NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lifecycle = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);

        return new AssignmentSubmissionService(
            _claimsService.Object,
            _db,
            _blobService.Object,
            _certificateService.Object,
            NullLogger<AssignmentSubmissionService>.Instance,
            lifecycle);
    }

    private void SeedStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
    }

    private void SeedManager()
    {
        _db.Users.Seed(new User
        {
            Id = _managerId,
            Code = "MGR-001",
            Email = "manager@test.com",
            Role = RoleType.Manager,
            IsDeleted = false
        });
    }

    private void SeedModule(ModuleType moduleType = ModuleType.Theory)
    {
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = moduleType,
            IsDeleted = false
        });
    }

    private void SeedActiveEnrollment(Guid? programEnrollmentId = null)
    {
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            Status = EnrollmentStatus.Active,
            ProgramEnrollmentId = programEnrollmentId,
            IsDeleted = false
        });
    }

    private void SeedProgramEnrollment()
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false
        });
    }

    private Assignment SeedFileUploadAssignment(
        int maxAttempts = 3,
        int maxPoints = 10,
        decimal passScore = 5m,
        DateTime? availableFrom = null,
        DateTime? availableUntil = null,
        AssignmentType type = AssignmentType.FileUpload,
        bool isDeleted = false)
    {
        var assignment = new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-FILE-001",
            ModuleId = _moduleId,
            Title = "File Upload Task",
            AssignmentType = type,
            MaxPoints = maxPoints,
            PassScore = passScore,
            MaxAttempts = maxAttempts,
            AvailableFrom = availableFrom,
            AvailableUntil = availableUntil,
            IsRequiredForModulePass = true,
            AllowShuffle = false,
            ShuffleOptions = false,
            IsDeleted = isDeleted
        };
        _db.Assignments.Seed(assignment);
        return assignment;
    }

    private void SeedStudentModuleAndAssignment(
        int maxAttempts = 3,
        Guid? programEnrollmentId = null,
        ModuleType moduleType = ModuleType.Theory)
    {
        SeedStudent();
        SeedModule(moduleType);
        SeedActiveEnrollment(programEnrollmentId);
        SeedFileUploadAssignment(maxAttempts: maxAttempts);
    }

    private Submission SeedTurnedInSubmission(
        Guid? researchMilestoneId = null,
        Guid? moduleEnrollmentId = null,
        SubmissionStatus status = SubmissionStatus.TurnedIn,
        int attemptNumber = 1,
        bool isDeleted = false,
        Guid? studentId = null,
        decimal? assignedGrade = null)
    {
        var submission = new Submission
        {
            Id = _submissionId,
            Code = "SUB-EXIST",
            AssignmentId = _assignmentId,
            StudentId = studentId ?? _studentId,
            ModuleEnrollmentId = moduleEnrollmentId ?? _enrollmentId,
            ResearchMilestoneId = researchMilestoneId,
            AttemptNumber = attemptNumber,
            Status = status,
            AssignedGrade = assignedGrade,
            ContentText = "Work",
            FileUrl = "https://cdn.example.com/old.pdf",
            SubmittedAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            IsDeleted = isDeleted
        };
        _db.Submissions.Seed(submission);
        return submission;
    }

    private void SeedMentorOwnsStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            Role = RoleType.Mentor,
            IsDeleted = false
        });

        SeedProgramEnrollment();

        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.Open,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            MaxCapacity = 30,
            IsDeleted = false
        });

        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = _classEnrollmentId,
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false
        });
    }

    private static Mock<IFormFile> CreateFormFile(
        string fileName = "work.pdf",
        long length = 1024)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3]));
        return file;
    }

    // ── SubmitAssignment ──────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAssignment_CreatesSubmission_WithContentText()
    {
        SeedStudentModuleAndAssignment();
        var sut = CreateSut();

        var result = await sut.SubmitAssignment(new SubmitAssignmentRequestDto
        {
            AssignmentId = _assignmentId,
            ContentText = "  My answer  "
        });

        Assert.Equal(_assignmentId, result.AssignmentId);
        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_enrollmentId, result.ModuleEnrollmentId);
        Assert.Equal(1, result.AttemptNumber);
        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal("My answer", result.ContentText);
        Assert.Equal(AssignmentType.FileUpload, result.AssignmentType);
        Assert.Null(result.Passed);
        Assert.Single(_db.Submissions.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task SubmitAssignment_AllowsWhenPersonalAvailableUntilExtendsWindow()
    {
        SeedStudent();
        SeedModule(ModuleType.Experiential);
        SeedActiveEnrollment();
        SeedFileUploadAssignment(availableUntil: DateTime.UtcNow.AddHours(-1));
        _db.AssessmentRecoveryRequests.Seed(new AssessmentRecoveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AssignmentId = _assignmentId,
            Status = AssessmentRecoveryRequestStatus.Approved,
            ExtraAttemptsGranted = 0,
            PersonalAvailableUntil = DateTime.UtcNow.AddDays(2),
            DecidedAt = DateTime.UtcNow.AddMinutes(-5),
            IsDeleted = false
        });
        var sut = CreateSut();

        var result = await sut.SubmitAssignment(new SubmitAssignmentRequestDto
        {
            AssignmentId = _assignmentId,
            ContentText = "Late but granted"
        });

        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsConflict_WhenWindowClosedWithoutPersonalGrant()
    {
        SeedStudent();
        SeedModule(ModuleType.Experiential);
        SeedActiveEnrollment();
        SeedFileUploadAssignment(availableUntil: DateTime.UtcNow.AddHours(-1));
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "Too late"
            }));
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsBadRequest_WhenContentAndFileMissing()
    {
        SeedStudentModuleAndAssignment();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "  ",
                FileUrl = null
            }));

        Assert.Equal("At least one of ContentText or FileUrl is required.", ex.Message);
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsUnauthorized_WhenUserIdEmpty()
    {
        var sut = CreateSut(currentUserId: Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "x"
            }));
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsNotFound_WhenStudentMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "x"
            }));
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsForbidden_WhenCallerNotStudent()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "x"
            }));

        Assert.Equal("Only students can submit assignment work.", ex.Message);
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsNotFound_WhenAssignmentMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "x"
            }));
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsBadRequest_WhenQuizType()
    {
        SeedStudent();
        SeedModule();
        SeedActiveEnrollment();
        SeedFileUploadAssignment(type: AssignmentType.Quiz);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "x"
            }));

        Assert.Equal("Quiz assignments are submitted through the quiz endpoints.", ex.Message);
    }

    [Fact]
    public async Task SubmitAssignment_Resubmits_WhenNotGraded()
    {
        SeedStudentModuleAndAssignment(maxAttempts: 3);
        SeedTurnedInSubmission(status: SubmissionStatus.ReturnedForRevision, attemptNumber: 1);
        var sut = CreateSut();

        var result = await sut.SubmitAssignment(new SubmitAssignmentRequestDto
        {
            AssignmentId = _assignmentId,
            ContentText = "Revised work"
        });

        Assert.Equal(2, result.AttemptNumber);
        Assert.Equal("Revised work", result.ContentText);
        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Single(_db.Submissions.Items);
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsBadRequest_WhenResearchMilestoneLinked()
    {
        SeedStudentModuleAndAssignment();
        _db.ResearchMilestones.Seed(new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = "RM-001",
            ModuleId = _moduleId,
            Title = "Milestone 1",
            AssignmentId = _assignmentId,
            MilestoneOrder = 1,
            IsDeleted = false
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "x"
            }));

        Assert.Contains("research milestone", ex.Message);
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsForbidden_WhenNoActiveEnrollment()
    {
        SeedStudent();
        SeedModule();
        SeedFileUploadAssignment();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "x"
            }));

        Assert.Contains("active module enrollment", ex.Message);
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsConflict_WhenAlreadyGraded()
    {
        SeedStudentModuleAndAssignment();
        SeedTurnedInSubmission(status: SubmissionStatus.Graded, assignedGrade: 10m);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "retry"
            }));

        Assert.Contains("already been graded", ex.Message);
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsConflict_WhenMaxAttemptsReached()
    {
        SeedStudentModuleAndAssignment(maxAttempts: 1, moduleType: ModuleType.Experiential);
        SeedTurnedInSubmission(status: SubmissionStatus.ReturnedForRevision, attemptNumber: 1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "retry"
            }));

        Assert.Contains("Maximum number of attempts", ex.Message);
    }

    // ── GradeAssignment ───────────────────────────────────────────────────────

    [Fact]
    public async Task GradeAssignment_GradesAsManager_AndRecalculatesProgress()
    {
        SeedStudent();
        SeedManager();
        SeedModule();
        SeedProgramEnrollment();
        SeedActiveEnrollment(programEnrollmentId: _programEnrollmentId);
        SeedFileUploadAssignment(maxPoints: 10, passScore: 5m);
        SeedTurnedInSubmission();
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
        {
            AssignedGrade = 8,
            MentorFeedback = "  Good work  "
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(8m, result.AssignedGrade);
        Assert.Equal("Good work", result.MentorFeedback);
        Assert.True(result.Passed);
        Assert.Equal(_managerId, result.VerifiedBy);
        Assert.NotNull(result.GradedAt);
        Assert.True(_db.SaveChangesCallCount >= 1);
        _certificateService.Verify(
            c => c.EnsureProgramCertificateInternalAsync(_programEnrollmentId),
            Times.Once);
    }

    [Fact]
    public async Task GradeAssignment_SkipsProgress_WhenModuleEnrollmentIdNull()
    {
        SeedStudent();
        SeedManager();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission(moduleEnrollmentId: null);
        _db.Submissions.Items[0].ModuleEnrollmentId = null;
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
        {
            AssignedGrade = 5
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        _certificateService.Verify(
            c => c.EnsureProgramCertificateInternalAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task GradeAssignment_AllowsMentor_WhenOwnsStudent()
    {
        SeedStudent();
        SeedModule();
        SeedActiveEnrollment(programEnrollmentId: _programEnrollmentId);
        SeedFileUploadAssignment();
        SeedTurnedInSubmission();
        SeedMentorOwnsStudent();
        var sut = CreateSut(currentUserId: _mentorId);

        var result = await sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
        {
            AssignedGrade = 7
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(_mentorId, result.VerifiedBy);
    }

    [Fact]
    public async Task GradeAssignment_ThrowsForbidden_WhenStudentGrades()
    {
        SeedStudent();
        SeedModule();
        SeedActiveEnrollment();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission();
        var sut = CreateSut(currentUserId: _studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
            {
                AssignedGrade = 5
            }));
    }

    [Fact]
    public async Task GradeAssignment_ThrowsNotFound_WhenSubmissionMissing()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GradeAssignment(Guid.NewGuid(), new GradeAssignmentSubmissionRequestDto
            {
                AssignedGrade = 5
            }));
    }

    [Fact]
    public async Task GradeAssignment_ThrowsBadRequest_WhenResearchSubmission()
    {
        SeedManager();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission(researchMilestoneId: Guid.NewGuid());
        var sut = CreateSut(currentUserId: _managerId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
            {
                AssignedGrade = 5
            }));

        Assert.Contains("research submission", ex.Message);
    }

    [Fact]
    public async Task GradeAssignment_ThrowsConflict_WhenNotTurnedIn()
    {
        SeedManager();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission(status: SubmissionStatus.Pending);
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
            {
                AssignedGrade = 5
            }));
    }

    [Fact]
    public async Task GradeAssignment_ThrowsBadRequest_WhenGradeOutOfRange()
    {
        SeedStudent();
        SeedManager();
        SeedModule();
        SeedActiveEnrollment();
        SeedFileUploadAssignment(maxPoints: 10);
        SeedTurnedInSubmission();
        var sut = CreateSut(currentUserId: _managerId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
            {
                AssignedGrade = 11
            }));

        Assert.Contains("AssignedGrade must be between", ex.Message);
    }

    // ── GetAssignmentSubmission ───────────────────────────────────────────────

    [Fact]
    public async Task GetAssignmentSubmission_ReturnsDto_ForOwnerStudent()
    {
        SeedStudent();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission();
        var sut = CreateSut(currentUserId: _studentId);

        var result = await sut.GetAssignmentSubmission(_submissionId);

        Assert.NotNull(result);
        Assert.Equal(_submissionId, result!.Id);
        Assert.Equal(_assignmentId, result.AssignmentId);
        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
    }

    [Fact]
    public async Task GetAssignmentSubmission_ReturnsNull_WhenMissing()
    {
        var sut = CreateSut();

        var result = await sut.GetAssignmentSubmission(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAssignmentSubmission_ThrowsBadRequest_WhenResearchSubmission()
    {
        SeedStudent();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission(researchMilestoneId: Guid.NewGuid());
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetAssignmentSubmission(_submissionId));
    }

    [Fact]
    public async Task GetAssignmentSubmission_ThrowsNotFound_WhenAssignmentMissing()
    {
        SeedStudent();
        SeedTurnedInSubmission();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetAssignmentSubmission(_submissionId));
    }

    [Fact]
    public async Task GetAssignmentSubmission_ThrowsForbidden_WhenOtherStudent()
    {
        SeedStudent();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission();
        _db.Users.Seed(new User
        {
            Id = _otherStudentId,
            Code = "STD-002",
            Email = "other@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
        var sut = CreateSut(currentUserId: _otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetAssignmentSubmission(_submissionId));
    }

    // ── UploadAssignmentFile ──────────────────────────────────────────────────

    [Fact]
    public async Task UploadAssignmentFile_UploadsAndReturnsPreviewUrl()
    {
        SeedStudent();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission();
        var sut = CreateSut();
        var file = CreateFormFile();

        var url = await sut.UploadAssignmentFile(_submissionId, file.Object);

        Assert.Equal("https://cdn.example.com/submissions/file.pdf", url);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.Is<string>(n => n.Contains(_submissionId.ToString()) && n.EndsWith(".pdf")),
                It.IsAny<Stream>(),
                It.Is<string>(f => f.Contains(_submissionId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _blobService.Verify(b => b.GetPreviewUrlAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UploadAssignmentFile_ThrowsForbidden_WhenNotOwner()
    {
        SeedStudent();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission(studentId: _otherStudentId);
        var sut = CreateSut(currentUserId: _studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UploadAssignmentFile(_submissionId, CreateFormFile().Object));
    }

    [Fact]
    public async Task UploadAssignmentFile_ThrowsBadRequest_WhenFileEmpty()
    {
        SeedStudent();
        SeedModule();
        SeedFileUploadAssignment();
        SeedTurnedInSubmission();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadAssignmentFile(_submissionId, CreateFormFile(length: 0).Object));
    }

    // ── Grade correction / reopen (Step 7) ────────────────────────────────────

    private void SeedClosedAcademicFailState(ProgramPurchaseEndReason endReason)
    {
        var programEnrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId);
        programEnrollment.Status = EnrollmentStatus.Failed;
        programEnrollment.EndReason = endReason;
        programEnrollment.EndedModuleId = _moduleId;
        programEnrollment.EndedAt = DateTime.UtcNow.AddHours(-1);

        _db.ModuleEnrollments.Items.Single(me => me.Id == _enrollmentId).Status = EnrollmentStatus.Failed;

        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = _classEnrollmentId,
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Withdrawn,
            IsDeleted = false
        });
    }

    private void SeedIncompleteActivity()
    {
        // One unfinished activity keeps module progress below 100% so a reopened
        // module enrollment stays Active instead of flipping straight to Completed.
        var courseId = Guid.NewGuid();
        _db.Courses.Seed(new Course
        {
            Id = courseId,
            ModuleId = _moduleId,
            Code = "CRS-001",
            Name = "Course 1",
            CourseOrder = 1,
            IsDeleted = false
        });
        _db.Activities.Seed(new Activity
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Code = "ACT-001",
            Name = "Reading 1",
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false
        });
    }

    [Fact]
    public async Task GradeAssignment_RegradeToPass_ReopensAcademicFailEnrollment()
    {
        SeedStudent();
        SeedManager();
        SeedModule();
        SeedProgramEnrollment();
        SeedActiveEnrollment(programEnrollmentId: _programEnrollmentId);
        SeedFileUploadAssignment(maxPoints: 10, passScore: 5m);
        SeedIncompleteActivity();
        SeedTurnedInSubmission(status: SubmissionStatus.Graded, assignedGrade: 2m);
        SeedClosedAcademicFailState(ProgramPurchaseEndReason.AcademicFail);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
        {
            AssignedGrade = 8
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(8m, result.AssignedGrade);

        var programEnrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId);
        Assert.Equal(EnrollmentStatus.Active, programEnrollment.Status);
        Assert.Null(programEnrollment.EndReason);
        Assert.Null(programEnrollment.EndedModuleId);
        Assert.Null(programEnrollment.EndedAt);
        Assert.Equal(
            EnrollmentStatus.Active,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _enrollmentId).Status);
        Assert.Equal(
            ClassEnrollmentStatus.Active,
            _db.ClassEnrollments.Items.Single(ce => ce.Id == _classEnrollmentId).Status);
    }

    [Fact]
    public async Task GradeAssignment_RegradeToPass_KeepsFailed_WhenEndReasonIsAttendance()
    {
        SeedStudent();
        SeedManager();
        SeedModule();
        SeedProgramEnrollment();
        SeedActiveEnrollment(programEnrollmentId: _programEnrollmentId);
        SeedFileUploadAssignment(maxPoints: 10, passScore: 5m);
        SeedIncompleteActivity();
        SeedTurnedInSubmission(status: SubmissionStatus.Graded, assignedGrade: 2m);
        SeedClosedAcademicFailState(ProgramPurchaseEndReason.Attendance);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
        {
            AssignedGrade = 8
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId).Status);
        Assert.Equal(
            ClassEnrollmentStatus.Withdrawn,
            _db.ClassEnrollments.Items.Single(ce => ce.Id == _classEnrollmentId).Status);
    }

    [Fact]
    public async Task GradeAssignment_RegradeToFail_KeepsEnrollmentFailed()
    {
        SeedStudent();
        SeedManager();
        SeedModule();
        SeedProgramEnrollment();
        SeedActiveEnrollment(programEnrollmentId: _programEnrollmentId);
        SeedFileUploadAssignment(maxPoints: 10, passScore: 5m);
        SeedIncompleteActivity();
        SeedTurnedInSubmission(status: SubmissionStatus.Graded, assignedGrade: 2m);
        SeedClosedAcademicFailState(ProgramPurchaseEndReason.AcademicFail);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
        {
            AssignedGrade = 3
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(3m, result.AssignedGrade);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId).Status);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task GradeAssignment_RegradeAsMentor_OnGradedSubmission_ThrowsConflict()
    {
        SeedStudent();
        SeedModule();
        SeedActiveEnrollment(programEnrollmentId: _programEnrollmentId);
        SeedFileUploadAssignment(maxPoints: 10, passScore: 5m);
        SeedTurnedInSubmission(status: SubmissionStatus.Graded, assignedGrade: 2m);
        SeedMentorOwnsStudent();
        var sut = CreateSut(currentUserId: _mentorId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.GradeAssignment(_submissionId, new GradeAssignmentSubmissionRequestDto
            {
                AssignedGrade = 8
            }));
    }

    [Fact]
    public async Task SubmitAssignment_ThrowsForbidden_WhenProgramEnrollmentClosed()
    {
        SeedStudentModuleAndAssignment(programEnrollmentId: _programEnrollmentId);
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Failed,
            IsDeleted = false
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitAssignment(new SubmitAssignmentRequestDto
            {
                AssignmentId = _assignmentId,
                ContentText = "late work"
            }));
    }
}
