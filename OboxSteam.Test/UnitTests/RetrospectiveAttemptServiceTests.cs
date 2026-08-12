using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.RetrospectiveDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class RetrospectiveAttemptServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _moduleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _assignmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _enrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private RetrospectiveAttemptService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);

        return new RetrospectiveAttemptService(
            _claimsService.Object,
            _db,
            NullLogger<RetrospectiveAttemptService>.Instance);
    }

    private void SeedStudentAndEnrollment(ModuleType moduleType = ModuleType.Theory)
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });

        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = moduleType,
            IsDeleted = false
        });

        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            Status = EnrollmentStatus.Active,
            ProgramEnrollmentId = null,
            IsDeleted = false
        });
    }

    private Assignment SeedRetrospectiveAssignment(
        int maxAttempts = 3,
        int maxPoints = 10,
        decimal passScore = 5m)
    {
        var assignment = new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-RETRO-001",
            ModuleId = _moduleId,
            Title = "Week 1 Reflection",
            Description = "Write your reflection",
            AssignmentType = AssignmentType.Retrospective,
            MaxPoints = maxPoints,
            PassScore = passScore,
            MaxAttempts = maxAttempts,
            IsRequiredForModulePass = false,
            IsDeleted = false
        };

        _db.Assignments.Seed(assignment);
        return assignment;
    }

    private Submission SeedSubmission(
        SubmissionStatus status = SubmissionStatus.Pending,
        Guid? studentId = null,
        string? contentText = null,
        Guid? researchMilestoneId = null,
        int attemptNumber = 1,
        decimal? assignedGrade = null,
        string? mentorFeedback = null,
        DateTime? gradedAt = null)
    {
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            Code = $"SUB-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            AssignmentId = _assignmentId,
            StudentId = studentId ?? _studentId,
            ModuleEnrollmentId = _enrollmentId,
            ResearchMilestoneId = researchMilestoneId,
            AttemptNumber = attemptNumber,
            Status = status,
            ContentText = contentText,
            AssignedGrade = assignedGrade,
            MentorFeedback = mentorFeedback,
            GradedAt = gradedAt,
            SubmittedAt = status is SubmissionStatus.TurnedIn or SubmissionStatus.Graded
                ? DateTime.UtcNow.AddMinutes(-10)
                : null,
            IsDeleted = false
        };

        _db.Submissions.Seed(submission);
        return submission;
    }

    // ── StartRetrospective ──────────────────────────────────────────────────────

    [Fact]
    public async Task StartRetrospective_CreatesNewPendingDraft()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var sut = CreateSut();

        var result = await sut.StartRetrospective(_assignmentId);

        Assert.Equal(_assignmentId, result.AssignmentId);
        Assert.Equal("Week 1 Reflection", result.Title);
        Assert.Equal(SubmissionStatus.Pending, result.Status);
        Assert.Equal(1, result.AttemptNumber);
        Assert.Null(result.ContentText);
        Assert.Single(_db.Submissions.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartRetrospective_ResumesExistingPendingSubmission()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var existing = SeedSubmission(contentText: "Draft in progress");
        var sut = CreateSut();

        var result = await sut.StartRetrospective(_assignmentId);

        Assert.Equal(existing.Id, result.SubmissionId);
        Assert.Equal("Draft in progress", result.ContentText);
        Assert.Equal(SubmissionStatus.Pending, result.Status);
        Assert.Single(_db.Submissions.Items);
        Assert.Equal(0, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartRetrospective_ThrowsBadRequest_WhenAssignmentIdEmpty()
    {
        SeedStudentAndEnrollment();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.StartRetrospective(Guid.Empty));
        Assert.Equal("AssignmentId is required.", ex.Message);
    }

    [Fact]
    public async Task StartRetrospective_ThrowsNotFound_WhenAssignmentMissing()
    {
        SeedStudentAndEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.StartRetrospective(_assignmentId));
    }

    [Fact]
    public async Task StartRetrospective_ThrowsBadRequest_WhenAssignmentIsNotRetrospective()
    {
        SeedStudentAndEnrollment();
        var assignment = SeedRetrospectiveAssignment();
        assignment.AssignmentType = AssignmentType.Quiz;
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.StartRetrospective(_assignmentId));
        Assert.Equal("This assignment is not a retrospective.", ex.Message);
    }

    [Fact]
    public async Task StartRetrospective_ThrowsConflict_WhenPastDueDate()
    {
        SeedStudentAndEnrollment();
        var assignment = SeedRetrospectiveAssignment();
        assignment.DueDate = DateTime.UtcNow.AddMinutes(-1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.StartRetrospective(_assignmentId));
        Assert.Equal("Assignment is past due date.", ex.Message);
    }

    [Fact]
    public async Task StartRetrospective_ThrowsForbidden_WhenNoActiveModuleEnrollment()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
        SeedRetrospectiveAssignment();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.StartRetrospective(_assignmentId));
        Assert.Contains("active module enrollment", ex.Message);
    }

    [Fact]
    public async Task StartRetrospective_ThrowsForbidden_WhenCallerIsNotStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "MGR-001",
            Email = "manager@test.com",
            Role = RoleType.Manager,
            IsDeleted = false
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.StartRetrospective(_assignmentId));
        Assert.Equal(RetrospectiveAttemptValidator.RetrospectiveForbiddenMessage, ex.Message);
    }

    [Fact]
    public async Task StartRetrospective_ThrowsConflict_WhenExistingSubmissionTurnedIn()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        SeedSubmission(status: SubmissionStatus.TurnedIn, contentText: "Done");
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.StartRetrospective(_assignmentId));
        Assert.Equal("Submission is pending mentor review.", ex.Message);
    }

    [Fact]
    public async Task StartRetrospective_ThrowsBadRequest_WhenExistingSubmissionIsResearch()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        SeedSubmission(researchMilestoneId: Guid.NewGuid());
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.StartRetrospective(_assignmentId));
        Assert.Contains("research submission", ex.Message);
    }

    // ── GetRetrospective ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRetrospective_ReturnsDto_ForOwnerStudent()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment(passScore: 5m, maxPoints: 10);
        var submission = SeedSubmission(
            status: SubmissionStatus.Graded,
            contentText: "Final text",
            assignedGrade: 8m,
            mentorFeedback: "Good work",
            gradedAt: DateTime.UtcNow.AddMinutes(-1));
        var sut = CreateSut();

        var result = await sut.GetRetrospective(submission.Id);

        Assert.NotNull(result);
        Assert.Equal(submission.Id, result!.SubmissionId);
        Assert.Equal("Final text", result.ContentText);
        Assert.Equal(8m, result.AssignedGrade);
        Assert.True(result.Passed);
        Assert.Equal("Good work", result.MentorFeedback);
        Assert.Equal(SubmissionStatus.Graded, result.Status);
    }

    [Fact]
    public async Task GetRetrospective_PassedIsFalse_WhenGradedBelowPassScore()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment(passScore: 7m, maxPoints: 10);
        var submission = SeedSubmission(
            status: SubmissionStatus.Graded,
            contentText: "Weak",
            assignedGrade: 4m,
            gradedAt: DateTime.UtcNow);
        var sut = CreateSut();

        var result = await sut.GetRetrospective(submission.Id);

        Assert.NotNull(result);
        Assert.False(result!.Passed);
    }

    [Fact]
    public async Task GetRetrospective_ReturnsNull_WhenSubmissionMissing()
    {
        SeedStudentAndEnrollment();
        var sut = CreateSut();

        var result = await sut.GetRetrospective(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRetrospective_ThrowsForbidden_WhenUnrelatedUser()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission();

        var otherId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        _db.Users.Seed(new User
        {
            Id = otherId,
            Code = "STD-002",
            Email = "other@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });

        var sut = CreateSut(currentUserId: otherId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetRetrospective(submission.Id));
    }

    [Fact]
    public async Task GetRetrospective_ThrowsBadRequest_WhenSubmissionIsResearch()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(researchMilestoneId: Guid.NewGuid());
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.GetRetrospective(submission.Id));
    }

    // ── SaveDraft ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveDraft_UpdatesContentText_AndReturnsLastSavedAt()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission();
        var sut = CreateSut();

        var response = await sut.SaveDraft(submission.Id, new SaveRetrospectiveDraftRequestDto
        {
            ContentText = "  My draft text  "
        });

        Assert.True(response.LastSavedAt <= DateTime.UtcNow);
        Assert.Equal("My draft text", submission.ContentText);
        Assert.Equal(_studentId, submission.UpdatedBy);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task SaveDraft_ThrowsNotFound_WhenSubmissionMissing()
    {
        SeedStudentAndEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.SaveDraft(Guid.NewGuid(), new SaveRetrospectiveDraftRequestDto { ContentText = "x" }));
    }

    [Fact]
    public async Task SaveDraft_ThrowsForbidden_WhenNotOwner()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(studentId: Guid.NewGuid());
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SaveDraft(submission.Id, new SaveRetrospectiveDraftRequestDto { ContentText = "x" }));
    }

    [Fact]
    public async Task SaveDraft_ThrowsConflict_WhenSubmissionTurnedIn()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(status: SubmissionStatus.TurnedIn, contentText: "Done");
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SaveDraft(submission.Id, new SaveRetrospectiveDraftRequestDto { ContentText = "edit" }));
        Assert.Equal("This submission is not open for editing.", ex.Message);
    }

    [Fact]
    public async Task SaveDraft_ThrowsBadRequest_WhenSubmissionIsResearch()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(researchMilestoneId: Guid.NewGuid());
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SaveDraft(submission.Id, new SaveRetrospectiveDraftRequestDto { ContentText = "x" }));
    }

    // ── SubmitRetrospective ───────────────────────────────────────────────────

    [Fact]
    public async Task SubmitRetrospective_TurnsInWithRequestContent()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(contentText: "Old draft");
        var sut = CreateSut();

        var result = await sut.SubmitRetrospective(submission.Id, new SubmitRetrospectiveRequestDto
        {
            ContentText = "  Final reflection  "
        });

        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal("Final reflection", result.ContentText);
        Assert.NotNull(result.SubmittedAt);
        Assert.Equal(SubmissionStatus.TurnedIn, submission.Status);
        Assert.Null(submission.FileUrl);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task SubmitRetrospective_UsesSavedDraft_WhenRequestContentOmitted()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(contentText: "Saved draft content");
        var sut = CreateSut();

        var result = await sut.SubmitRetrospective(submission.Id, new SubmitRetrospectiveRequestDto
        {
            ContentText = null
        });

        Assert.Equal("Saved draft content", result.ContentText);
        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
    }

    [Fact]
    public async Task SubmitRetrospective_ThrowsConflict_WhenMaxAttemptsExceededOnRevision()
    {
        SeedStudentAndEnrollment(ModuleType.Experiential);
        SeedRetrospectiveAssignment(maxAttempts: 1);
        var submission = SeedSubmission(
            status: SubmissionStatus.ReturnedForRevision,
            contentText: "Cannot submit again",
            attemptNumber: 1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitRetrospective(submission.Id, new SubmitRetrospectiveRequestDto
            {
                ContentText = "Too many attempts"
            }));

        Assert.Contains("Maximum number of attempts", ex.Message);
    }

    [Fact]
    public async Task SubmitRetrospective_ThrowsBadRequest_WhenContentEmptyAndNoDraft()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(contentText: null);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitRetrospective(submission.Id, new SubmitRetrospectiveRequestDto
            {
                ContentText = "   "
            }));

        Assert.Equal("ContentText is required to submit a retrospective.", ex.Message);
    }

    [Fact]
    public async Task SubmitRetrospective_ThrowsConflict_WhenAlreadyTurnedIn()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(status: SubmissionStatus.TurnedIn, contentText: "Done");
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitRetrospective(submission.Id, new SubmitRetrospectiveRequestDto
            {
                ContentText = "Again"
            }));

        Assert.Equal("This submission is not open for submission.", ex.Message);
    }

    [Fact]
    public async Task SubmitRetrospective_ThrowsForbidden_WhenNotOwner()
    {
        SeedStudentAndEnrollment();
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(studentId: Guid.NewGuid(), contentText: "Mine");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitRetrospective(submission.Id, new SubmitRetrospectiveRequestDto
            {
                ContentText = "Hack"
            }));
    }

    [Fact]
    public async Task SubmitRetrospective_ThrowsForbidden_WhenNoActiveEnrollment()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
        SeedRetrospectiveAssignment();
        var submission = SeedSubmission(contentText: "No enroll");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitRetrospective(submission.Id, new SubmitRetrospectiveRequestDto
            {
                ContentText = "No enroll"
            }));
    }
}
