using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ClassEnrollmentDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassEnrollmentServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programEnrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _targetClassId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _classEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IClassService> _classService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassEnrollmentService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _classService
            .Setup(c => c.TryAutoStartClassIfReadyAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ClassEnrollmentService(
            _db,
            _claimsService.Object,
            _classService.Object,
            NullLogger<ClassEnrollmentService>.Instance,
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

    private void SeedMentor()
    {
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            Role = RoleType.Mentor,
            FullName = "Mentor One",
            IsDeleted = false
        });
        _db.MentorProfiles.Seed(new MentorProfile
        {
            Id = Guid.NewGuid(),
            MentorId = _mentorId,
            Title = "STEM mentor",
            IsDeleted = false
        });
    }

    private void SeedProgramEnrollment(
        EnrollmentStatus status = EnrollmentStatus.Active,
        Guid? studentId = null,
        bool isDeleted = false,
        Guid? id = null,
        Guid? programId = null)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = id ?? _programEnrollmentId,
            StudentId = studentId ?? _studentId,
            ProgramId = programId ?? _programId,
            Status = status,
            IsDeleted = isDeleted
        });
    }

    private Class SeedClass(
        Guid? id = null,
        string code = "CLS-001",
        string name = "Cohort A",
        ClassStatus status = ClassStatus.Open,
        int maxCapacity = 30,
        Guid? mentorId = null,
        int minHours = 48,
        Guid? programId = null)
    {
        var entity = new Class
        {
            Id = id ?? _classId,
            Code = code,
            Name = name,
            ProgramId = programId ?? _programId,
            MentorId = mentorId,
            Status = status,
            MaxCapacity = maxCapacity,
            MinHoursBeforeAssignmentJoin = minHours,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            IsDeleted = false
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private ClassEnrollment SeedClassEnrollment(
        Guid? id = null,
        Guid? classId = null,
        ClassEnrollmentStatus status = ClassEnrollmentStatus.Active,
        Class? classEntity = null,
        DateTime? enrolledAt = null,
        bool isDeleted = false,
        Guid? programEnrollmentId = null)
    {
        var enrollment = new ClassEnrollment
        {
            Id = id ?? _classEnrollmentId,
            ClassId = classId ?? _classId,
            Class = classEntity!,
            StudentId = _studentId,
            ProgramEnrollmentId = programEnrollmentId ?? _programEnrollmentId,
            Status = status,
            EnrolledAt = enrolledAt ?? DateTime.UtcNow.AddDays(-3),
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            IsDeleted = isDeleted
        };
        _db.ClassEnrollments.Seed(enrollment);
        return enrollment;
    }

    private void SeedSession(
        Guid classId,
        DateTime start,
        DateTime end,
        string title = "Session",
        ClassSessionStatus status = ClassSessionStatus.Scheduled)
    {
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = Guid.NewGuid(),
            Title = title,
            StartTime = start,
            EndTime = end,
            SessionKind = SessionKind.LiveOnline,
            Status = status,
            IsDeleted = false,
        });
    }

    // ── EnrollClassAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Enroll_CreatesActiveEnrollment_AndNotifies()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedClass();
        var sut = CreateSut();

        var result = await sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
        {
            ProgramEnrollmentId = _programEnrollmentId,
            ClassId = _classId
        });

        Assert.Equal(ClassEnrollmentStatus.Active, result.Status);
        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_classId, result.Class.Id);
        Assert.Equal("CLS-001", result.Class.Code);
        Assert.Single(_db.ClassEnrollments.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _classService.Verify(c => c.TryAutoStartClassIfReadyAsync(_classId), Times.Once);
    }

    [Fact]
    public async Task Enroll_ThrowsConflict_WhenActiveClassCapReached()
    {
        SeedStudent();
        var pe1 = Guid.Parse("33333333-3333-3333-3333-333333333301");
        var pe2 = Guid.Parse("33333333-3333-3333-3333-333333333302");
        var pe3 = Guid.Parse("33333333-3333-3333-3333-333333333303");
        var program2 = Guid.Parse("22222222-2222-2222-2222-222222222201");
        var program3 = Guid.Parse("22222222-2222-2222-2222-222222222202");
        var class2 = Guid.Parse("44444444-4444-4444-4444-444444444401");
        var class3 = Guid.Parse("44444444-4444-4444-4444-444444444402");

        SeedProgramEnrollment(id: pe1, programId: _programId);
        SeedProgramEnrollment(id: pe2, programId: program2);
        SeedProgramEnrollment(id: pe3, programId: program3);
        SeedClass(id: _classId, programId: _programId);
        SeedClass(id: class2, code: "CLS-002", programId: program2);
        SeedClass(id: class3, code: "CLS-003", programId: program3);
        SeedClassEnrollment(classId: _classId);
        SeedClassEnrollment(
            id: Guid.Parse("55555555-5555-5555-5555-555555555501"),
            classId: class2);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = pe3,
                ClassId = class3,
            }));
        Assert.Contains("maximum of 2", ex.Message);
    }

    [Fact]
    public async Task Enroll_MapsMentor_WhenAssigned()
    {
        SeedStudent();
        SeedMentor();
        SeedProgramEnrollment();
        SeedClass(mentorId: _mentorId);
        var sut = CreateSut();

        var result = await sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
        {
            ProgramEnrollmentId = _programEnrollmentId,
            ClassId = _classId
        });

        Assert.NotNull(result.Class.Mentor);
        Assert.Equal(_mentorId, result.Class.Mentor!.Id);
    }

    [Fact]
    public async Task Enroll_ThrowsConflict_WhenActiveEnrollmentExists()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var cls = SeedClass();
        SeedClass(id: _targetClassId, code: "CLS-002", name: "Cohort B");
        SeedClassEnrollment(classEntity: cls);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _targetClassId
            }));
    }

    [Fact]
    public async Task Enroll_ThrowsConflict_WhenAlreadyInSameClass()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var cls = SeedClass();
        // Non-active historical enrollment in same class still blocks
        SeedClassEnrollment(classEntity: cls, status: ClassEnrollmentStatus.Transferred);
        // Clear active-for-program check by using Transferred - then existingInClass check fires
        var sut = CreateSut();

        // First need no Active for program - Transferred is fine for ValidateNoActiveClassEnrollment
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _classId
            }));
    }

    [Fact]
    public async Task Enroll_ThrowsConflict_WhenClassFull()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedClass(maxCapacity: 1);
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = _otherStudentId,
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _classId
            }));
    }

    [Fact]
    public async Task Enroll_ThrowsBadRequest_WhenLateJoinBlocked()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedClass(minHours: 48);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            ModuleId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Title = "Assignment window",
            SessionKind = SessionKind.AssignmentWindow,
            StartTime = DateTime.UtcNow.AddHours(10),
            EndTime = DateTime.UtcNow.AddHours(12),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _classId
            }));
        Assert.Contains("Cannot join within", ex.Message);
    }

    [Fact]
    public async Task Enroll_ThrowsBadRequest_WhenClassNotOpen()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedClass(status: ClassStatus.Completed);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _classId
            }));
    }

    [Fact]
    public async Task Enroll_ThrowsBadRequest_WhenClassInProgress()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedClass(status: ClassStatus.InProgress);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _classId
            }));
        Assert.Contains("not open for enrollment", ex.Message);
    }

    [Fact]
    public async Task Enroll_ThrowsBadRequest_WhenClassWrongProgram()
    {
        SeedStudent();
        SeedProgramEnrollment();
        SeedClass(programId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _classId
            }));
    }

    [Fact]
    public async Task Enroll_ThrowsForbidden_WhenNotStudent()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = _classId
            }));
    }

    [Fact]
    public async Task Enroll_ThrowsBadRequest_WhenIdsEmpty()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = Guid.Empty,
                ClassId = _classId
            }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = _programEnrollmentId,
                ClassId = Guid.Empty
            }));
    }

    // ── TransferClassAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_MovesStudentToTargetClass()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var source = SeedClass();
        SeedClass(id: _targetClassId, code: "CLS-002", name: "Cohort B");
        SeedClassEnrollment(classEntity: source);
        var sut = CreateSut();

        var result = await sut.TransferClassAsync(
            _classEnrollmentId,
            new UpdateClassEnrollmentRequestDto { ClassId = _targetClassId });

        Assert.Equal(_targetClassId, result.Class.Id);
        Assert.Equal(_targetClassId, _db.ClassEnrollments.Items[0].ClassId);
        Assert.Equal(ClassEnrollmentStatus.Active, result.Status);
        _classService.Verify(c => c.TryAutoStartClassIfReadyAsync(_targetClassId), Times.Once);
    }

    [Fact]
    public async Task Transfer_ThrowsBadRequest_WhenSameClass()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var source = SeedClass();
        SeedClassEnrollment(classEntity: source);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.TransferClassAsync(
                _classEnrollmentId,
                new UpdateClassEnrollmentRequestDto { ClassId = _classId }));
    }

    [Fact]
    public async Task Transfer_ThrowsBadRequest_WhenEnrollmentNotActive()
    {
        SeedStudent();
        SeedProgramEnrollment();
        var source = SeedClass();
        SeedClassEnrollment(classEntity: source, status: ClassEnrollmentStatus.Transferred);
        SeedClass(id: _targetClassId, code: "CLS-002");
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.TransferClassAsync(
                _classEnrollmentId,
                new UpdateClassEnrollmentRequestDto { ClassId = _targetClassId }));
    }

    [Fact]
    public async Task Transfer_ThrowsForbidden_WhenNotOwner()
    {
        SeedStudent();
        SeedStudent(_otherStudentId);
        SeedProgramEnrollment();
        var source = SeedClass();
        SeedClassEnrollment(classEntity: source);
        SeedClass(id: _targetClassId, code: "CLS-002");
        var sut = CreateSut(currentUserId: _otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.TransferClassAsync(
                _classEnrollmentId,
                new UpdateClassEnrollmentRequestDto { ClassId = _targetClassId }));
    }

    [Fact]
    public async Task Transfer_ThrowsNotFound_WhenEnrollmentMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.TransferClassAsync(
                _classEnrollmentId,
                new UpdateClassEnrollmentRequestDto { ClassId = _targetClassId }));
    }

    // ── TransferClassByManagerAsync ───────────────────────────────────────────

    [Fact]
    public async Task ManagerTransfer_MarksOldTransferred_AndCreatesNew()
    {
        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var source = SeedClass();
        SeedClass(id: _targetClassId, code: "CLS-002", name: "Cohort B");
        SeedClassEnrollment(classEntity: source);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.TransferClassByManagerAsync(
            _studentId,
            new ManagerTransferClassRequestDto { ClassId = _targetClassId });

        Assert.Equal(ClassEnrollmentStatus.Active, result.Status);
        Assert.Equal(_targetClassId, result.Class.Id);
        Assert.Equal(2, _db.ClassEnrollments.Items.Count);
        Assert.Equal(ClassEnrollmentStatus.Transferred, _db.ClassEnrollments.Items[0].Status);
        _classService.Verify(c => c.TryAutoStartClassIfReadyAsync(_targetClassId), Times.Once);
    }

    [Fact]
    public async Task ManagerTransfer_ThrowsForbidden_WhenNotManager()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.TransferClassByManagerAsync(
                _studentId,
                new ManagerTransferClassRequestDto { ClassId = _targetClassId }));
    }

    [Fact]
    public async Task ManagerTransfer_ThrowsBadRequest_WhenTargetNotOpen()
    {
        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var source = SeedClass();
        SeedClassEnrollment(classEntity: source);
        SeedClass(id: _targetClassId, code: "CLS-002", status: ClassStatus.InProgress);
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.TransferClassByManagerAsync(
                _studentId,
                new ManagerTransferClassRequestDto { ClassId = _targetClassId }));
    }

    [Fact]
    public async Task ManagerTransfer_ThrowsNotFound_WhenNoActiveEnrollment()
    {
        SeedStudent();
        SeedManager();
        SeedClass(id: _targetClassId, code: "CLS-002");
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.TransferClassByManagerAsync(
                _studentId,
                new ManagerTransferClassRequestDto { ClassId = _targetClassId }));
    }

    // ── GetClassEnrollmentByIdAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsDto()
    {
        SeedStudent();
        var cls = SeedClass(mentorId: null);
        SeedClassEnrollment(classEntity: cls);
        var sut = CreateSut();

        var result = await sut.GetClassEnrollmentByIdAsync(_classEnrollmentId);

        Assert.Equal(_classEnrollmentId, result.Id);
        Assert.Equal("CLS-001", result.Class.Code);
        Assert.Null(result.Class.Mentor);
    }

    [Fact]
    public async Task GetById_ThrowsNotFound_WhenMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassEnrollmentByIdAsync(_classEnrollmentId));
    }

    [Fact]
    public async Task GetById_AllowsManager()
    {
        SeedStudent();
        SeedManager();
        var cls = SeedClass();
        SeedClassEnrollment(classEntity: cls);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetClassEnrollmentByIdAsync(_classEnrollmentId);

        Assert.Equal(_classEnrollmentId, result.Id);
    }

    // ── GetClassEnrollmentsByProgramEnrollmentAsync ───────────────────────────

    [Fact]
    public async Task GetByProgramEnrollment_ReturnsPaginated()
    {
        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var c1 = SeedClass(code: "CLS-A", name: "Alpha");
        var c2 = SeedClass(id: _targetClassId, code: "CLS-B", name: "Beta");
        SeedClassEnrollment(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            classId: _classId,
            classEntity: c1,
            enrolledAt: DateTime.UtcNow.AddDays(-2));
        SeedClassEnrollment(
            id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            classId: _targetClassId,
            classEntity: c2,
            status: ClassEnrollmentStatus.Transferred,
            enrolledAt: DateTime.UtcNow.AddDays(-1));
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetClassEnrollmentsByProgramEnrollmentAsync(
            _programEnrollmentId, "enrolledAt", true, 1, 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetByProgramEnrollment_SortsByClassName()
    {
        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var c1 = SeedClass(code: "CLS-Z", name: "Zebra");
        var c2 = SeedClass(id: _targetClassId, code: "CLS-A", name: "Apple");
        SeedClassEnrollment(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            classId: _classId,
            classEntity: c1);
        SeedClassEnrollment(
            id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            classId: _targetClassId,
            classEntity: c2);
        var sut = CreateSut(currentUserId: _managerId);

        var result = await sut.GetClassEnrollmentsByProgramEnrollmentAsync(
            _programEnrollmentId, "className", false, 1, 10);

        Assert.Equal("Apple", result.Items[0].Class.Name);
        Assert.Equal("Zebra", result.Items[1].Class.Name);

        var byStatus = await sut.GetClassEnrollmentsByProgramEnrollmentAsync(
            _programEnrollmentId, "status", false, 1, 10);
        Assert.True(byStatus.TotalCount >= 1);
        var byCreated = await sut.GetClassEnrollmentsByProgramEnrollmentAsync(
            _programEnrollmentId, "createdat", true, 1, 10);
        Assert.True(byCreated.TotalCount >= 1);
        var byClassCode = await sut.GetClassEnrollmentsByProgramEnrollmentAsync(
            _programEnrollmentId, "classcode", false, 1, 10);
        Assert.True(byClassCode.TotalCount >= 1);
        var byDefault = await sut.GetClassEnrollmentsByProgramEnrollmentAsync(
            _programEnrollmentId, "xxx", false, 1, 10);
        Assert.True(byDefault.TotalCount >= 1);
    }

    [Fact]
    public async Task GetByProgramEnrollment_ThrowsNotFound_WhenProgramEnrollmentMissing()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassEnrollmentsByProgramEnrollmentAsync(_programEnrollmentId, null, true, 1, 10));
    }

    [Fact]
    public async Task GetByProgramEnrollment_ThrowsBadRequest_WhenPaginationInvalid()
    {
        SeedManager();
        SeedProgramEnrollment();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetClassEnrollmentsByProgramEnrollmentAsync(_programEnrollmentId, null, true, 0, 10));
    }

    [Fact]
    public async Task Enroll_ThrowsConflict_WhenSessionOverlapsOtherProgramClass()
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddHours(2);
        var otherProgramId = Guid.Parse("26262626-2626-2626-2626-262626262626");
        var otherPeId = Guid.Parse("36363636-3636-3636-3636-363636363636");
        var otherClassId = Guid.Parse("46464646-4646-4646-4646-464646464646");

        SeedStudent();
        SeedProgramEnrollment();
        var classA = SeedClass();
        SeedClassEnrollment(classEntity: classA);
        SeedSession(classA.Id, start, end, "Robotics Saturday");

        SeedProgramEnrollment(id: otherPeId, programId: otherProgramId);
        SeedClass(id: otherClassId, code: "CLS-B", name: "Cohort B", programId: otherProgramId);
        SeedSession(otherClassId, start.AddMinutes(30), end.AddMinutes(30), "Coding Saturday");

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
            {
                ProgramEnrollmentId = otherPeId,
                ClassId = otherClassId,
            }));

        Assert.Contains("overlaps", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enroll_Succeeds_WhenOtherClassSessionsDoNotOverlap()
    {
        var saturday = DateTime.UtcNow.AddDays(10);
        var sunday = saturday.AddDays(1);
        var otherProgramId = Guid.Parse("26262626-2626-2626-2626-262626262626");
        var otherPeId = Guid.Parse("36363636-3636-3636-3636-363636363636");
        var otherClassId = Guid.Parse("46464646-4646-4646-4646-464646464646");

        SeedStudent();
        SeedProgramEnrollment();
        var classA = SeedClass();
        SeedClassEnrollment(classEntity: classA);
        SeedSession(classA.Id, saturday, saturday.AddHours(2), "Saturday");

        SeedProgramEnrollment(id: otherPeId, programId: otherProgramId);
        SeedClass(id: otherClassId, code: "CLS-B", programId: otherProgramId);
        SeedSession(otherClassId, sunday, sunday.AddHours(2), "Sunday");

        var sut = CreateSut();

        var result = await sut.EnrollClassAsync(new CreateClassEnrollmentRequestDto
        {
            ProgramEnrollmentId = otherPeId,
            ClassId = otherClassId,
        });

        Assert.Equal(otherClassId, result.Class.Id);
    }

    [Fact]
    public async Task Transfer_ThrowsConflict_WhenTargetOverlapsOtherProgramClass()
    {
        var start = DateTime.UtcNow.AddDays(12);
        var end = start.AddHours(2);
        var otherProgramId = Guid.Parse("26262626-2626-2626-2626-262626262626");
        var otherPeId = Guid.Parse("36363636-3636-3636-3636-363636363636");
        var otherClassId = Guid.Parse("46464646-4646-4646-4646-464646464646");
        var otherEnrollmentId = Guid.Parse("56565656-5656-5656-5656-565656565656");

        SeedStudent();
        SeedProgramEnrollment();
        var classA = SeedClass();
        SeedClassEnrollment(classEntity: classA);
        SeedSession(classA.Id, start.AddDays(7), start.AddDays(7).AddHours(2), "Later A");

        SeedProgramEnrollment(id: otherPeId, programId: otherProgramId);
        var classB = SeedClass(id: otherClassId, code: "CLS-B", programId: otherProgramId);
        SeedClassEnrollment(
            id: otherEnrollmentId,
            classId: otherClassId,
            classEntity: classB,
            programEnrollmentId: otherPeId);
        SeedSession(otherClassId, start, end, "Busy B");

        var classC = SeedClass(id: _targetClassId, code: "CLS-C", name: "Cohort C");
        SeedSession(_targetClassId, start, end, "Clash C");

        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.TransferClassAsync(
                _classEnrollmentId,
                new UpdateClassEnrollmentRequestDto { ClassId = _targetClassId }));
    }

    [Fact]
    public async Task ManagerTransfer_ThrowsConflict_WhenTargetOverlapsOtherProgramClass()
    {
        var start = DateTime.UtcNow.AddDays(12);
        var end = start.AddHours(2);
        var otherProgramId = Guid.Parse("26262626-2626-2626-2626-262626262626");
        var otherPeId = Guid.Parse("36363636-3636-3636-3636-363636363636");
        var otherClassId = Guid.Parse("46464646-4646-4646-4646-464646464646");
        var otherEnrollmentId = Guid.Parse("56565656-5656-5656-5656-565656565656");

        SeedStudent();
        SeedManager();
        SeedProgramEnrollment();
        var classA = SeedClass();
        SeedClassEnrollment(classEntity: classA);

        SeedProgramEnrollment(id: otherPeId, programId: otherProgramId);
        var classB = SeedClass(id: otherClassId, code: "CLS-B", programId: otherProgramId);
        SeedClassEnrollment(
            id: otherEnrollmentId,
            classId: otherClassId,
            classEntity: classB,
            programEnrollmentId: otherPeId);
        SeedSession(otherClassId, start, end, "Busy B");

        SeedClass(id: _targetClassId, code: "CLS-C");
        SeedSession(_targetClassId, start, end, "Clash C");

        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.TransferClassByManagerAsync(
                _studentId,
                new ManagerTransferClassRequestDto { ClassId = _targetClassId }));
    }

    [Fact]
    public async Task GetMySchedule_ReturnsActiveClassIntervals_ExcludingCancelled()
    {
        var start = DateTime.UtcNow.AddDays(3);
        SeedStudent();
        SeedProgramEnrollment();
        var classA = SeedClass();
        SeedClassEnrollment(classEntity: classA);
        SeedSession(classA.Id, start, start.AddHours(2), "Keep");
        SeedSession(
            classA.Id,
            start.AddDays(1),
            start.AddDays(1).AddHours(2),
            "Cancelled",
            ClassSessionStatus.Cancelled);

        var sut = CreateSut();

        var result = await sut.GetMyScheduleAsync();

        Assert.Single(result);
        Assert.Equal("Keep", result[0].Title);
        Assert.Equal("CLS-001", result[0].ClassCode);
        Assert.Equal(classA.Id, result[0].ClassId);
    }

    [Fact]
    public async Task GetMySchedule_ThrowsForbidden_WhenNotStudent()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetMyScheduleAsync());
    }
}
