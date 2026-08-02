using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ResearchSubmissionServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _researchModuleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _activityId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _milestoneId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _milestone2Id = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _assignmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _assignment2Id = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _programEnrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _classEnrollmentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private readonly Guid _submissionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<ICertificateService> _certificateService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ResearchSubmissionService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
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
        _blobService
            .Setup(b => b.BucketName)
            .Returns("oboxsteam-bucket-main");
        _blobService
            .Setup(b => b.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => $"https://presigned.example.com/{key}");
        _certificateService
            .Setup(c => c.EnsureProgramCertificateAsync(It.IsAny<Guid>()))
            .ReturnsAsync((CertificateDetailDto?)null);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ResearchSubmissionService(
            _claimsService.Object,
            _db,
            _blobService.Object,
            _certificateService.Object,
            _notificationPublisher.Object,
            NullLogger<ResearchSubmissionService>.Instance);
    }

    private static Mock<IFormFile> CreateFormFile(string fileName = "work.pdf", long length = 1024)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3]));
        return file;
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            Role = role,
            IsDeleted = false,
        });
    }

    private void SeedResearchCurriculum()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Research Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _researchModuleId,
            Code = "MOD-RSH",
            Name = "Research Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-RSH",
            Name = "Research Course",
            ModuleId = _researchModuleId,
            IsDeleted = false,
        });
        _db.Activities.Seed(new Activity
        {
            Id = _activityId,
            Code = "ACT-RSH",
            Name = "Research Reading",
            CourseId = _courseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false,
        });
    }

    private Assignment SeedAssignment(
        Guid? id = null,
        string code = "ASG-001",
        int maxAttempts = 3)
    {
        var assignment = new Assignment
        {
            Id = id ?? _assignmentId,
            Code = code,
            Title = "Milestone Deliverable",
            ModuleId = _researchModuleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 70m,
            MaxAttempts = maxAttempts,
            IsRequiredForModulePass = true,
            IsDeleted = false,
        };
        _db.Assignments.Seed(assignment);
        return assignment;
    }

    private void SeedMilestone(
        Guid? id = null,
        int order = 1,
        Guid? assignmentId = null,
        string title = "Proposal")
    {
        _db.ResearchMilestones.Seed(new ResearchMilestone
        {
            Id = id ?? _milestoneId,
            Code = order == 1 ? "MLS-001" : "MLS-002",
            Title = title,
            ModuleId = _researchModuleId,
            MilestoneOrder = order,
            AssignmentId = assignmentId ?? _assignmentId,
            IsDeleted = false,
        });
    }

    private void SeedStudentEnrollmentChain()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _researchModuleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = _classEnrollmentId,
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedMentorOwnsStudent()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedStudentEnrollmentChain();
    }

    private Submission SeedSubmission(
        SubmissionStatus status = SubmissionStatus.Pending,
        int attemptNumber = 0,
        Guid? milestoneId = null,
        Guid? studentId = null)
    {
        var submission = new Submission
        {
            Id = _submissionId,
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = studentId ?? _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = milestoneId ?? _milestoneId,
            AttemptNumber = attemptNumber,
            Status = status,
            IsDeleted = false,
        };
        _db.Submissions.Seed(submission);
        return submission;
    }

    private void SeedCompletedRequiredActivity()
    {
        _db.ResearchMilestoneActivities.Seed(new ResearchMilestoneActivity
        {
            Id = Guid.NewGuid(),
            ResearchMilestoneId = _milestoneId,
            ActivityId = _activityId,
            IsRequiredForSubmission = true,
            DisplayOrder = 1,
            Activity = _db.Activities.Items[0],
            IsDeleted = false,
        });
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = _activityId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            IsCompleted = true,
            IsDeleted = false,
        });
    }

    // ── StartSubmission ───────────────────────────────────────────────────────

    [Fact]
    public async Task StartSubmission_OpensPending_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedCompletedRequiredActivity();
        var sut = CreateSut(_managerId);

        var result = await sut.StartSubmission(new StartResearchSubmissionRequestDto
        {
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = _milestoneId,
        });

        Assert.Equal(SubmissionStatus.Pending, result.Status);
        Assert.Equal(0, result.AttemptNumber);
        Assert.Equal(_milestoneId, result.ResearchMilestoneId);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartSubmission_Throws_Forbidden_WhenStudent()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.StartSubmission(new StartResearchSubmissionRequestDto
            {
                ModuleEnrollmentId = _moduleEnrollmentId,
                ResearchMilestoneId = _milestoneId,
            }));
    }

    [Fact]
    public async Task StartSubmission_Throws_Conflict_WhenAlreadyExists()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.StartSubmission(new StartResearchSubmissionRequestDto
            {
                ModuleEnrollmentId = _moduleEnrollmentId,
                ResearchMilestoneId = _milestoneId,
            }));
    }

    [Fact]
    public async Task StartSubmission_Throws_WhenEnrollmentInactive()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        _db.ModuleEnrollments.Items[0].Status = EnrollmentStatus.Completed;
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.StartSubmission(new StartResearchSubmissionRequestDto
            {
                ModuleEnrollmentId = _moduleEnrollmentId,
                ResearchMilestoneId = _milestoneId,
            }));
    }

    [Fact]
    public async Task StartSubmissionForClass_ReturnsEmpty_WhenNoStudents()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Empty",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        var result = await sut.StartSubmissionForClass(new StartResearchSubmissionForClassRequestDto
        {
            ClassId = _classId,
            ResearchMilestoneId = _milestoneId,
        });

        Assert.Equal(0, result.TotalClassStudents);
        Assert.Equal(0, result.OpenedCount);
    }

    [Fact]
    public async Task StartSubmissionForClass_SkipsStudentWithoutModuleEnrollment()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedUser(_studentId, RoleType.Student, "STD-001");
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = _classEnrollmentId,
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        var result = await sut.StartSubmissionForClass(new StartResearchSubmissionForClassRequestDto
        {
            ClassId = _classId,
            ResearchMilestoneId = _milestoneId,
        });

        Assert.Equal(1, result.TotalClassStudents);
        Assert.Equal(0, result.OpenedCount);
        Assert.Single(result.Skipped);
    }

    [Fact]
    public async Task GetSubmission_ShowsPassed_WhenGraded()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        _db.Submissions.Seed(new Submission
        {
            Id = _submissionId,
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = _milestoneId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 85m,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.GetSubmission(_submissionId);

        Assert.NotNull(result);
        Assert.True(result!.Passed);
        Assert.Equal(85m, result.AssignedGrade);
    }

    [Fact]
    public async Task GradeSubmission_Continues_WhenCertificateFails()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        _certificateService
            .Setup(c => c.EnsureProgramCertificateAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("cert down"));
        var sut = CreateSut(_managerId);

        var result = await sut.GradeSubmission(_submissionId, new GradeResearchSubmissionRequestDto
        {
            AssignedGrade = 90,
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
    }

    [Fact]
    public async Task StartSubmissionForClass_OpensForEligibleStudents()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        var sut = CreateSut(_managerId);

        var result = await sut.StartSubmissionForClass(new StartResearchSubmissionForClassRequestDto
        {
            ClassId = _classId,
            ResearchMilestoneId = _milestoneId,
        });

        Assert.Equal(1, result.TotalClassStudents);
        Assert.Equal(1, result.OpenedCount);
        Assert.Empty(result.Skipped);
    }

    // ── UploadSubmissionFile ──────────────────────────────────────────────────

    [Fact]
    public async Task UploadSubmissionFile_UploadsMainFile_ReturnsFileUrl()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut();
        var file = CreateFormFile();

        var result = await sut.UploadSubmissionFile(_submissionId, file.Object);

        Assert.Equal("https://cdn.example.com/submissions/file.pdf", result.FileUrl);
        Assert.Null(result.EvidenceUrls);
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
    public async Task UploadSubmissionFile_UploadsEvidence_ReturnsEvidenceUrls()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut();

        var result = await sut.UploadSubmissionFile(
            _submissionId,
            CreateFormFile("photo.jpg").Object,
            isEvidence: true);

        Assert.Null(result.FileUrl);
        Assert.Single(result.EvidenceUrls!);
        Assert.Equal("https://cdn.example.com/submissions/file.pdf", result.EvidenceUrls![0]);
    }

    [Fact]
    public async Task UploadSubmissionFile_Throws_Forbidden_WhenNotOwner()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(studentId: _otherStudentId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UploadSubmissionFile(_submissionId, CreateFormFile().Object));
    }

    [Fact]
    public async Task UploadSubmissionFile_Throws_WhenFileEmpty()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadSubmissionFile(_submissionId, CreateFormFile(length: 0).Object));
    }

    // ── SubmitResearchWork ────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitResearchWork_TurnsIn_WithContentText()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut();

        var result = await sut.SubmitResearchWork(_submissionId, new CreateResearchSubmissionRequestDto
        {
            ContentText = "  My research proposal  ",
        });

        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal(1, result.AttemptNumber);
        Assert.Equal("My research proposal", result.ContentText);
        Assert.NotNull(result.SubmittedAt);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitResearchWork_SubmitsWithFileUrl_AndEvidence()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut();

        var result = await sut.SubmitResearchWork(_submissionId, new CreateResearchSubmissionRequestDto
        {
            FileUrl = "https://cdn.example.com/submissions/file.pdf",
            EvidenceUrls = ["https://cdn.example.com/submissions/evidence.jpg"],
        });

        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal("https://presigned.example.com/submissions/file.pdf", result.FileUrl);
        Assert.Single(_db.MediaAssets.Items);
        Assert.Equal("https://cdn.example.com/submissions/evidence.jpg", _db.MediaAssets.Items[0].FileUrl);
        Assert.Single(_db.SubmissionEvidences.Items);
    }

    [Fact]
    public async Task SubmitResearchWork_Resubmits_AfterReturnedForRevision()
    {
        SeedResearchCurriculum();
        SeedAssignment(maxAttempts: 3);
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.ReturnedForRevision, attemptNumber: 1);
        var sut = CreateSut();

        var result = await sut.SubmitResearchWork(_submissionId, new CreateResearchSubmissionRequestDto
        {
            ContentText = "Revised work",
        });

        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal(2, result.AttemptNumber);
        Assert.Equal("Revised work", result.ContentText);
    }

    [Fact]
    public async Task SubmitResearchWork_Throws_Forbidden_WhenNotStudent()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitResearchWork(_submissionId, new CreateResearchSubmissionRequestDto
            {
                ContentText = "x",
            }));
    }

    [Fact]
    public async Task SubmitResearchWork_Throws_WhenContentMissing()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitResearchWork(_submissionId, new CreateResearchSubmissionRequestDto()));
    }

    [Fact]
    public async Task SubmitResearchWork_Throws_Conflict_WhenAlreadyTurnedIn()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitResearchWork(_submissionId, new CreateResearchSubmissionRequestDto
            {
                ContentText = "x",
            }));
    }

    [Fact]
    public async Task SubmitResearchWork_Throws_Conflict_WhenMaxAttemptsExceeded()
    {
        SeedResearchCurriculum();
        SeedAssignment(maxAttempts: 1);
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.ReturnedForRevision, attemptNumber: 1);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitResearchWork(_submissionId, new CreateResearchSubmissionRequestDto
            {
                ContentText = "x",
            }));
    }

    // ── GradeSubmission ───────────────────────────────────────────────────────

    [Fact]
    public async Task GradeSubmission_MarksGraded_AsManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut(_managerId);

        var result = await sut.GradeSubmission(_submissionId, new GradeResearchSubmissionRequestDto
        {
            AssignedGrade = 85,
            MentorFeedback = "  Great work  ",
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(85m, result.AssignedGrade);
        Assert.Equal("Great work", result.MentorFeedback);
        Assert.True(result.Passed);
        Assert.Equal(_managerId, result.VerifiedBy);
        Assert.NotNull(result.GradedAt);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _certificateService.Verify(c => c.EnsureProgramCertificateAsync(_programEnrollmentId), Times.Once);
    }

    [Fact]
    public async Task GradeSubmission_ReturnsForRevision()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut(_managerId);

        var result = await sut.GradeSubmission(_submissionId, new GradeResearchSubmissionRequestDto
        {
            AssignedGrade = 50,
            MentorFeedback = "Needs more detail",
            ReturnForRevision = true,
        });

        Assert.Equal(SubmissionStatus.ReturnedForRevision, result.Status);
        Assert.Null(result.Passed);
    }

    [Fact]
    public async Task GradeSubmission_AllowsMentor_WhenOwnsStudent()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedMentorOwnsStudent();
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut(_mentorId);

        var result = await sut.GradeSubmission(_submissionId, new GradeResearchSubmissionRequestDto
        {
            AssignedGrade = 75,
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(_mentorId, result.VerifiedBy);
    }

    [Fact]
    public async Task GradeSubmission_Throws_Forbidden_WhenStudent()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GradeSubmission(_submissionId, new GradeResearchSubmissionRequestDto
            {
                AssignedGrade = 80,
            }));
    }

    [Fact]
    public async Task GradeSubmission_Throws_Conflict_WhenNotTurnedIn()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.GradeSubmission(_submissionId, new GradeResearchSubmissionRequestDto
            {
                AssignedGrade = 80,
            }));
    }

    [Fact]
    public async Task GradeSubmission_Throws_WhenGradeOutOfRange()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut(_managerId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GradeSubmission(_submissionId, new GradeResearchSubmissionRequestDto
            {
                AssignedGrade = 150,
            }));
        Assert.Contains("AssignedGrade must be between", ex.Message);
    }

    // ── GetSubmission ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubmission_ReturnsSubmission_ForStudent()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut();

        var result = await sut.GetSubmission(_submissionId);

        Assert.NotNull(result);
        Assert.Equal(_submissionId, result!.Id);
        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal(70m, result.PassScore);
    }

    [Fact]
    public async Task GetSubmission_ReturnsNull_WhenMissing()
    {
        var sut = CreateSut();

        Assert.Null(await sut.GetSubmission(_submissionId));
    }

    [Fact]
    public async Task GetSubmission_Throws_Forbidden_WhenUnauthorized()
    {
        SeedUser(_otherStudentId, RoleType.Student, "STD-002");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedStudentEnrollmentChain();
        SeedSubmission(status: SubmissionStatus.TurnedIn, attemptNumber: 1);
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetSubmission(_submissionId));
    }
}
