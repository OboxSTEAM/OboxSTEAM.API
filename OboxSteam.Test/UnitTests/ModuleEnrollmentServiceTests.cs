using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ModuleEnrollmentServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _moduleId2 = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _programEnrollmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ModuleEnrollmentService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ModuleEnrollmentService(
            _db,
            _claimsService.Object,
            NullLogger<ModuleEnrollmentService>.Instance,
            _notificationPublisher.Object);
    }

    private void SeedStudent(Guid? id = null)
    {
        _db.Users.Seed(new User
        {
            Id = id ?? _studentId,
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

    private void SeedProgramEnrollment(
        EnrollmentStatus status = EnrollmentStatus.Active,
        Guid? studentId = null,
        bool isDeleted = false)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = studentId ?? _studentId,
            ProgramId = _programId,
            Status = status,
            IsDeleted = isDeleted
        });
    }

    private Module SeedModule(
        Guid? id = null,
        Guid? programId = null,
        int order = 1,
        string name = "Module 1",
        bool isDeleted = false)
    {
        var module = new Module
        {
            Id = id ?? _moduleId,
            Code = "MOD-001",
            Name = name,
            ProgramId = programId ?? _programId,
            ModuleType = ModuleType.Experiential,
            ModuleOrder = order,
            Price = 100m,
            RetakeFee = 50m,
            IsMandatory = true,
            IsDeleted = isDeleted
        };
        _db.Modules.Seed(module);
        return module;
    }

    private ModuleEnrollment SeedModuleEnrollment(
        Guid? id = null,
        Guid? moduleId = null,
        EnrollmentStatus status = EnrollmentStatus.Failed,
        int attemptNumber = 1,
        int failureCount = 2,
        bool isDeleted = false,
        Module? module = null)
    {
        var enrollment = new ModuleEnrollment
        {
            Id = id ?? _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = moduleId ?? _moduleId,
            Module = module!,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = status,
            AttemptNumber = attemptNumber,
            AssignmentFailureCount = failureCount,
            ProgressPercent = 40m,
            EnrolledAt = DateTime.UtcNow.AddDays(-10),
            IsDeleted = isDeleted
        };
        _db.ModuleEnrollments.Seed(enrollment);
        return enrollment;
    }

    private UpdateModuleEnrollmentRequestDto BuildRetakeRequest()
        => new()
        {
            ProgramEnrollmentId = _programEnrollmentId,
            ModuleId = _moduleId
        };

    // ── RetakeModuleAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Retake_CreatesPendingPayment_AfterFailedAttempt()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var module = SeedModule();
        SeedModuleEnrollment(module: module, status: EnrollmentStatus.Failed, attemptNumber: 1, failureCount: 2);
        var sut = CreateSut();

        var result = await sut.RetakeModuleAsync(BuildRetakeRequest());

        Assert.Equal(EnrollmentStatus.PendingPayment, result.Status);
        Assert.Equal(2, result.AttemptNumber);
        Assert.Equal(0, result.AssignmentFailureCount);
        Assert.Equal(_moduleId, result.ModuleId);
        Assert.Equal("Module 1", result.Name);
        Assert.Equal(2, _db.ModuleEnrollments.Items.Count);
        Assert.Equal(1, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(c => c.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Retake_ReusesExistingPendingPayment()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var module = SeedModule();
        SeedModuleEnrollment(
            module: module,
            status: EnrollmentStatus.PendingPayment,
            attemptNumber: 2,
            failureCount: 0);
        var sut = CreateSut();

        var result = await sut.RetakeModuleAsync(BuildRetakeRequest());

        Assert.Equal(_moduleEnrollmentId, result.Id);
        Assert.Equal(EnrollmentStatus.PendingPayment, result.Status);
        Assert.Single(_db.ModuleEnrollments.Items);
        Assert.Equal(0, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Retake_ThrowsConflict_WhenActiveEnrollmentExists()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var module = SeedModule();
        SeedModuleEnrollment(module: module, status: EnrollmentStatus.Active, failureCount: 0);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => sut.RetakeModuleAsync(BuildRetakeRequest()));
    }

    [Fact]
    public async Task Retake_ThrowsBadRequest_WhenNoFailedAttempt()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedModule();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RetakeModuleAsync(BuildRetakeRequest()));
        Assert.Contains("class re-delivery", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Retake_ThrowsBadRequest_ForTheoryModule()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var module = SeedModule();
        module.ModuleType = ModuleType.Theory;
        SeedModuleEnrollment(module: module, status: EnrollmentStatus.Failed, failureCount: 2);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RetakeModuleAsync(BuildRetakeRequest()));
        Assert.Contains("Theory modules", ex.Message);
    }

    [Fact]
    public async Task Retake_ThrowsBadRequest_WhenIdsEmpty()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RetakeModuleAsync(new UpdateModuleEnrollmentRequestDto
            {
                ProgramEnrollmentId = Guid.Empty,
                ModuleId = _moduleId
            }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RetakeModuleAsync(new UpdateModuleEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ModuleId = Guid.Empty
            }));
    }

    [Fact]
    public async Task Retake_ThrowsForbidden_WhenNotStudent()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.RetakeModuleAsync(BuildRetakeRequest()));
    }

    [Fact]
    public async Task Retake_ThrowsNotFound_WhenProgramEnrollmentMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.RetakeModuleAsync(BuildRetakeRequest()));
    }

    [Fact]
    public async Task Retake_ThrowsForbidden_WhenProgramEnrollmentBelongsToOther()
    {
        SeedStudent();
        SeedProgramEnrollment(studentId: _otherStudentId);
        SeedModule();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.RetakeModuleAsync(BuildRetakeRequest()));
    }

    [Fact]
    public async Task Retake_ThrowsBadRequest_WhenProgramEnrollmentNotActive()
    {
        SeedStudent();
        SeedProgramEnrollment(status: EnrollmentStatus.PendingPayment);
        SeedModule();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.RetakeModuleAsync(BuildRetakeRequest()));
    }

    [Fact]
    public async Task Retake_ThrowsNotFound_WhenModuleMissing()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.RetakeModuleAsync(BuildRetakeRequest()));
    }

    [Fact]
    public async Task Retake_ThrowsBadRequest_WhenModuleWrongProgram()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedModule(programId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.RetakeModuleAsync(BuildRetakeRequest()));
    }

    // ── GetModuleEnrollmentByIdAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsDto_WhenModuleNavWired()
    {
        SeedStudent();
        var module = SeedModule();
        SeedModuleEnrollment(module: module, status: EnrollmentStatus.Active, failureCount: 0);
        var sut = CreateSut();

        var result = await sut.GetModuleEnrollmentByIdAsync(_moduleEnrollmentId);

        Assert.Equal(_moduleEnrollmentId, result.Id);
        Assert.Equal("MOD-001", result.Code);
        Assert.Equal("Module 1", result.Name);
        Assert.Equal(_programEnrollmentId, result.ProgramEnrollmentId);
    }

    [Fact]
    public async Task GetById_ThrowsNotFound_WhenMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetModuleEnrollmentByIdAsync(_moduleEnrollmentId));
    }

    [Fact]
    public async Task GetById_ThrowsForbidden_WhenOtherStudent()
    {
        SeedStudent();
        SeedStudent(_otherStudentId);
        var module = SeedModule();
        SeedModuleEnrollment(module: module, status: EnrollmentStatus.Active, failureCount: 0);
        var sut = CreateSut(currentUserId: _otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetModuleEnrollmentByIdAsync(_moduleEnrollmentId));
    }

    [Fact]
    public async Task GetById_AllowsManager()
    {
        SeedStudent();
        SeedManager();
        var module = SeedModule();
        SeedModuleEnrollment(module: module, status: EnrollmentStatus.Active, failureCount: 0);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetModuleEnrollmentByIdAsync(_moduleEnrollmentId);

        Assert.Equal(_moduleEnrollmentId, result.Id);
    }

    // ── GetModuleEnrollmentsByProgramEnrollmentIdAsync ────────────────────────

    [Fact]
    public async Task GetByProgramEnrollment_ReturnsLatestAttemptPerModule()
    {
        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var m1 = SeedModule(order: 2, name: "Later");
        var m2 = SeedModule(id: _moduleId2, order: 1, name: "Earlier");
        SeedModuleEnrollment(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            moduleId: _moduleId,
            module: m1,
            status: EnrollmentStatus.Failed,
            attemptNumber: 1,
            failureCount: 2);
        SeedModuleEnrollment(
            id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            moduleId: _moduleId,
            module: m1,
            status: EnrollmentStatus.Active,
            attemptNumber: 2,
            failureCount: 0);
        SeedModuleEnrollment(
            id: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            moduleId: _moduleId2,
            module: m2,
            status: EnrollmentStatus.Completed,
            attemptNumber: 1,
            failureCount: 0);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetModuleEnrollmentsByProgramEnrollmentIdAsync(_programEnrollmentId);

        Assert.Equal(2, result.Count);
        Assert.Equal(_moduleId2, result[0].ModuleId);
        Assert.Equal(2, result[1].AttemptNumber);
    }

    [Fact]
    public async Task GetByProgramEnrollment_ReturnsEmpty_WhenNone()
    {
        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetModuleEnrollmentsByProgramEnrollmentIdAsync(_programEnrollmentId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByProgramEnrollment_ThrowsNotFound_WhenProgramEnrollmentMissing()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetModuleEnrollmentsByProgramEnrollmentIdAsync(_programEnrollmentId));
    }

    [Fact]
    public async Task GetByProgramEnrollment_ThrowsBadRequest_WhenIdEmpty()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetModuleEnrollmentsByProgramEnrollmentIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetByProgramEnrollment_ExcludesDeletedModuleNav()
    {
        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var module = SeedModule(isDeleted: true);
        SeedModuleEnrollment(module: module, status: EnrollmentStatus.Active, failureCount: 0);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetModuleEnrollmentsByProgramEnrollmentIdAsync(_programEnrollmentId);

        Assert.Empty(result);
    }
}
