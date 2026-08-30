using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.AssessmentRecoveryDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AssessmentRecoveryRequestServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _moduleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _assignmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _enrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _classId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _requestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private AssessmentRecoveryRequestService CreateSut(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(DateTime.UtcNow);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lifecycle = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);

        return new AssessmentRecoveryRequestService(
            _db,
            _claimsService.Object,
            _notificationPublisher.Object,
            NullLogger<AssessmentRecoveryRequestService>.Instance,
            lifecycle);
    }

    private void SeedStudentExperiential(ModuleType moduleType = ModuleType.Experiential)
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Hands-on",
            ProgramId = _programId,
            ModuleType = moduleType,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-001",
            ModuleId = _moduleId,
            Title = "Lab report",
            AssignmentType = AssignmentType.FileUpload,
            MaxAttempts = 1,
            TimeLimitMinutes = 60,
            MaxPoints = 10,
            PassScore = 5,
            IsDeleted = false,
        });
        ClassAssignmentWindowSeed.ClassWithActiveEnrollment(
            _db,
            _classId,
            _programId,
            _studentId,
            mentorId: _mentorId);
        ClassAssignmentWindowSeed.Open(_db, _classId, _moduleId, _assignmentId);
    }

    [Fact]
    public async Task Create_ThrowsBadRequest_WhenTheoryModule()
    {
        SeedStudentExperiential(ModuleType.Theory);
        var sut = CreateSut(_studentId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateAsync(new CreateAssessmentRecoveryRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                AssignmentId = _assignmentId,
            }));
        Assert.Contains("unlimited attempts", ex.Message);
    }

    [Fact]
    public async Task Create_ThrowsConflict_WhenClassWindowClosed()
    {
        SeedStudentExperiential();
        var window = _db.ClassSessions.Items.Single(s => s.AssignmentId == _assignmentId);
        window.EndTime = DateTime.UtcNow.AddHours(-1);
        var sut = CreateSut(_studentId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateAsync(new CreateAssessmentRecoveryRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                AssignmentId = _assignmentId,
            }));
        Assert.Equal(AssignmentWindowPolicy.ClosedMessage, ex.Message);
    }

    [Fact]
    public async Task Approve_ThrowsBadRequest_WhenExtraAttemptsIsZero()
    {
        SeedStudentExperiential();
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Role = RoleType.Mentor,
            IsDeleted = false,
        });
        _db.AssessmentRecoveryRequests.Seed(new AssessmentRecoveryRequest
        {
            Id = _requestId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AssignmentId = _assignmentId,
            ClassId = _classId,
            Status = AssessmentRecoveryRequestStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut(_mentorId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ApproveAsync(_requestId, new DecideAssessmentRecoveryRequestDto
            {
                ExtraAttemptsGranted = 0,
            }));
        Assert.Contains("at least one extra attempt", ex.Message);
    }
}
