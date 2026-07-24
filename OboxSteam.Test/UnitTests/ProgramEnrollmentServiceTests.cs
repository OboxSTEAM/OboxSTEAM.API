using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramEnrollmentServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _parentId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _mentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programId2 = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _enrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _classEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ProgramEnrollmentService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ProgramEnrollmentService(
            _db,
            _claimsService.Object,
            NullLogger<ProgramEnrollmentService>.Instance,
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

    private void SeedParent()
    {
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PAR-001",
            Email = "parent@test.com",
            Role = RoleType.Parent,
            IsDeleted = false
        });
    }

    private Program SeedProgram(Guid? id = null, string name = "Robotics", bool isDeleted = false)
    {
        var program = new Program
        {
            Id = id ?? _programId,
            Code = "PRG-001",
            Name = name,
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = "Published",
            Price = 1000m,
            IsDeleted = isDeleted
        };
        _db.Programs.Seed(program);
        return program;
    }

    private ProgramEnrollment SeedEnrollment(
        Guid? id = null,
        Guid? studentId = null,
        Guid? programId = null,
        EnrollmentStatus status = EnrollmentStatus.Active,
        decimal progress = 0m,
        DateTime? enrolledAt = null,
        bool isDeleted = false,
        Program? program = null)
    {
        var enrollment = new ProgramEnrollment
        {
            Id = id ?? _enrollmentId,
            StudentId = studentId ?? _studentId,
            ProgramId = programId ?? _programId,
            Program = program!,
            Status = status,
            ProgressPercent = progress,
            EnrolledAt = enrolledAt ?? DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = isDeleted
        };
        _db.ProgramEnrollments.Seed(enrollment);
        return enrollment;
    }

    // ── GetOrCreatePendingEnrollmentAsync ─────────────────────────────────────

    [Fact]
    public async Task GetOrCreatePending_CreatesNew_AndPublishes()
    {
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.GetOrCreatePendingEnrollmentAsync(_studentId, _programId);

        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_programId, result.ProgramId);
        Assert.Equal(EnrollmentStatus.PendingPayment, result.Status);
        Assert.Equal(0m, result.ProgressPercent);
        Assert.Single(_db.ProgramEnrollments.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreatePending_ReusesExistingPendingPayment()
    {
        SeedProgram();
        SeedEnrollment(status: EnrollmentStatus.PendingPayment);
        var sut = CreateSut();

        var result = await sut.GetOrCreatePendingEnrollmentAsync(_studentId, _programId);

        Assert.Equal(_enrollmentId, result.Id);
        Assert.Single(_db.ProgramEnrollments.Items);
        Assert.Equal(0, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreatePending_ThrowsConflict_WhenAlreadyActive()
    {
        SeedProgram();
        SeedEnrollment(status: EnrollmentStatus.Active);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.GetOrCreatePendingEnrollmentAsync(_studentId, _programId));
    }

    [Fact]
    public async Task GetOrCreatePending_ThrowsNotFound_WhenProgramMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetOrCreatePendingEnrollmentAsync(_studentId, _programId));
    }

    [Fact]
    public async Task GetOrCreatePending_ThrowsBadRequest_WhenIdsEmpty()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetOrCreatePendingEnrollmentAsync(Guid.Empty, _programId));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetOrCreatePendingEnrollmentAsync(_studentId, Guid.Empty));
    }

    // ── GetProgramEnrollmentByIdAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsDto_ForOwnerStudent()
    {
        SeedStudent();
        var program = SeedProgram();
        SeedEnrollment(program: program);
        var sut = CreateSut();

        var result = await sut.GetProgramEnrollmentByIdAsync(_enrollmentId);

        Assert.Equal(_enrollmentId, result.Id);
        Assert.Equal("PRG-001", result.Code);
        Assert.Equal("Robotics", result.Name);
        Assert.Equal(EnrollmentStatus.Active, result.Status);
    }

    [Fact]
    public async Task GetById_FallsBackToProgramLookup_WhenNavNull()
    {
        SeedStudent();
        SeedProgram();
        SeedEnrollment(); // Program nav null
        var sut = CreateSut();

        var result = await sut.GetProgramEnrollmentByIdAsync(_enrollmentId);

        Assert.Equal("Robotics", result.Name);
    }

    [Fact]
    public async Task GetById_ThrowsNotFound_WhenEnrollmentMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetProgramEnrollmentByIdAsync(_enrollmentId));
    }

    [Fact]
    public async Task GetById_ThrowsNotFound_WhenProgramMissing()
    {
        SeedStudent();
        SeedEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetProgramEnrollmentByIdAsync(_enrollmentId));
    }

    [Fact]
    public async Task GetById_ThrowsForbidden_WhenOtherStudent()
    {
        SeedStudent();
        SeedStudent(_otherStudentId);
        SeedProgram();
        SeedEnrollment();
        var sut = CreateSut(currentUserId: _otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetProgramEnrollmentByIdAsync(_enrollmentId));
    }

    [Fact]
    public async Task GetById_AllowsManager()
    {
        SeedStudent();
        SeedManager();
        var program = SeedProgram();
        SeedEnrollment(program: program);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetProgramEnrollmentByIdAsync(_enrollmentId);

        Assert.Equal(_enrollmentId, result.Id);
    }

    // ── GetMyProgramEnrollmentsAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetMy_ReturnsOnlyCurrentStudentEnrollments()
    {
        SeedStudent();
        var p1 = SeedProgram();
        var p2 = SeedProgram(_programId2, "Art");
        SeedEnrollment(id: _enrollmentId, programId: _programId, program: p1, progress: 10);
        SeedEnrollment(
            id: Guid.NewGuid(),
            studentId: _otherStudentId,
            programId: _programId2,
            program: p2);
        var sut = CreateSut();

        var result = await sut.GetMyProgramEnrollmentsAsync(null, null, true, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_enrollmentId, result.Items[0].Id);
    }

    [Fact]
    public async Task GetMy_FiltersByProgramId()
    {
        SeedStudent();
        var p1 = SeedProgram();
        var p2 = SeedProgram(_programId2, "Art");
        SeedEnrollment(id: Guid.NewGuid(), programId: _programId, program: p1);
        SeedEnrollment(id: Guid.NewGuid(), programId: _programId2, program: p2);
        var sut = CreateSut();

        var result = await sut.GetMyProgramEnrollmentsAsync(_programId2, null, true, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_programId2, result.Items[0].ProgramId);
    }

    [Fact]
    public async Task GetMy_ParentSeesLinkedStudents_Only()
    {
        SeedStudent();
        SeedParent();
        var program = SeedProgram();
        SeedEnrollment(program: program);
        SeedEnrollment(
            id: Guid.NewGuid(),
            studentId: _otherStudentId,
            program: program);
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsDeleted = false
        });
        var sut = CreateSut(currentUserId: _parentId);

        var result = await sut.GetMyProgramEnrollmentsAsync(null, null, true, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_studentId, result.Items[0].StudentId);
    }

    [Fact]
    public async Task GetMy_ParentWithNoLinks_ReturnsEmpty()
    {
        SeedParent();
        SeedProgram();
        SeedEnrollment();
        var sut = CreateSut(currentUserId: _parentId);

        var result = await sut.GetMyProgramEnrollmentsAsync(null, null, true, 1, 10);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetMy_ManagerListsAll()
    {
        SeedManager();
        var program = SeedProgram();
        SeedEnrollment(program: program);
        SeedEnrollment(id: Guid.NewGuid(), studentId: _otherStudentId, program: program);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetMyProgramEnrollmentsAsync(null, null, true, 1, 10);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetMy_ThrowsForbidden_WhenMentor()
    {
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            Role = RoleType.Mentor,
            IsDeleted = false
        });
        var sut = CreateSut(currentUserId: _mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetMyProgramEnrollmentsAsync(null, null, true, 1, 10));
    }

    [Fact]
    public async Task GetMy_ThrowsBadRequest_WhenPaginationInvalid()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetMyProgramEnrollmentsAsync(null, null, true, 0, 10));
    }

    [Fact]
    public async Task GetMy_SortsByProgressPercentAscending()
    {
        SeedStudent();
        var program = SeedProgram();
        SeedEnrollment(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            program: program,
            progress: 80,
            enrolledAt: DateTime.UtcNow.AddDays(-2));
        SeedEnrollment(
            id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            program: program,
            progress: 20,
            enrolledAt: DateTime.UtcNow.AddDays(-1));
        var sut = CreateSut();

        var result = await sut.GetMyProgramEnrollmentsAsync(null, "progressPercent", false, 1, 10);

        Assert.Equal(20m, result.Items[0].ProgressPercent);
        Assert.Equal(80m, result.Items[1].ProgressPercent);
    }

    [Fact]
    public async Task GetMy_ThrowsNotFound_WhenProgramMissingForItem()
    {
        SeedStudent();
        SeedEnrollment(); // no program seeded
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetMyProgramEnrollmentsAsync(null, null, true, 1, 10));
    }

    // ── GetProgramEnrollmentsByStudentIdAsync ─────────────────────────────────

    [Fact]
    public async Task GetByStudentId_ReturnsEnrollments()
    {
        SeedStudent();
        SeedManager();
        var program = SeedProgram();
        SeedEnrollment(program: program);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetProgramEnrollmentsByStudentIdAsync(_studentId, null, true, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_enrollmentId, result.Items[0].Id);
    }

    [Fact]
    public async Task GetByStudentId_ThrowsNotFound_WhenStudentMissing()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetProgramEnrollmentsByStudentIdAsync(_studentId, null, true, 1, 10));
    }

    [Fact]
    public async Task GetByStudentId_ThrowsForbidden_WhenOtherStudent()
    {
        SeedStudent();
        SeedStudent(_otherStudentId);
        var sut = CreateSut(currentUserId: _otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetProgramEnrollmentsByStudentIdAsync(_studentId, null, true, 1, 10));
    }

    [Fact]
    public async Task GetByStudentId_SortsByStatus()
    {
        SeedStudent();
        SeedManager();
        var program = SeedProgram();
        SeedEnrollment(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            program: program,
            status: EnrollmentStatus.Completed,
            enrolledAt: DateTime.UtcNow.AddDays(-2));
        SeedEnrollment(
            id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            program: program,
            status: EnrollmentStatus.Active,
            enrolledAt: DateTime.UtcNow.AddDays(-1));
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetProgramEnrollmentsByStudentIdAsync(
            _studentId, "status", false, 1, 10);

        Assert.Equal(EnrollmentStatus.Active, result.Items[0].Status);
        Assert.Equal(EnrollmentStatus.Completed, result.Items[1].Status);
    }

    // ── GetProgramEnrollmentClassAsync ────────────────────────────────────────

    [Fact]
    public async Task GetClass_ReturnsClassIds_WhenActiveClassEnrollmentExists()
    {
        SeedStudent();
        SeedProgram();
        SeedEnrollment();
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = _classEnrollmentId,
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _enrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false
        });
        var sut = CreateSut();

        var result = await sut.GetProgramEnrollmentClassAsync(_enrollmentId);

        Assert.Equal(_enrollmentId, result.ProgramEnrollmentId);
        Assert.Equal(_classId, result.ClassId);
        Assert.Equal(_classEnrollmentId, result.ClassEnrollmentId);
    }

    [Fact]
    public async Task GetClass_ReturnsNullClass_WhenNoActiveEnrollment()
    {
        SeedStudent();
        SeedProgram();
        SeedEnrollment();
        var sut = CreateSut();

        var result = await sut.GetProgramEnrollmentClassAsync(_enrollmentId);

        Assert.Null(result.ClassId);
        Assert.Null(result.ClassEnrollmentId);
    }

    [Fact]
    public async Task GetClass_ThrowsNotFound_WhenEnrollmentMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetProgramEnrollmentClassAsync(_enrollmentId));
    }
}
