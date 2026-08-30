using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ResearchMilestoneDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ResearchMilestoneServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _theoryModuleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _researchModuleId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _courseId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _activityId = Guid.Parse("36363636-3636-3636-3636-363636363636");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _milestoneId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _milestone2Id = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _assignmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _assignment2Id = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _linkId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _programEnrollmentId = Guid.Parse("98989898-9898-9898-9898-989898989898");

    private readonly DateTime _now = DateTime.UtcNow;

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private ResearchMilestoneService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);

        return new ResearchMilestoneService(
            _claimsService.Object,
            _db,
            NullLogger<ResearchMilestoneService>.Instance);
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            IsDeleted = false,
        });
    }

    private void SeedResearchCurriculum(bool includeTheoryModule = false)
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

        if (includeTheoryModule)
        {
            _db.Modules.Seed(new Module
            {
                Id = _theoryModuleId,
                Code = "MOD-THEORY",
                Name = "Theory",
                ProgramId = _programId,
                ModuleType = ModuleType.Theory,
                ModuleOrder = 1,
                IsDeleted = false,
            });
        }

        _db.Modules.Seed(new Module
        {
            Id = _researchModuleId,
            Code = "MOD-RSH",
            Name = "Research Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 2,
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

    private void SeedClass(Guid? mentorId = null)
    {
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = mentorId ?? _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = _now.AddDays(-7),
            EndDate = _now.AddDays(60),
            IsDeleted = false,
        });
    }

    private Assignment SeedAssignment(
        Guid? id = null,
        string code = "ASG-001",
        Guid? moduleId = null)
    {
        var assignment = new Assignment
        {
            Id = id ?? _assignmentId,
            Code = code,
            Title = "Deliverable",
            ModuleId = moduleId ?? _researchModuleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 70m,
            MaxAttempts = 3,
            TimeLimitMinutes = 60,
            IsRequiredForModulePass = true,
            IsDeleted = false,
        };
        _db.Assignments.Seed(assignment);
        ClassAssignmentWindowSeed.Open(_db, _classId, assignment.ModuleId, assignment.Id);
        return assignment;
    }

    private ResearchMilestone SeedMilestone(
        Guid? id = null,
        int order = 1,
        bool isCapstone = false,
        Guid? assignmentId = null,
        string code = "MLS-001",
        string title = "Proposal")
    {
        var milestone = new ResearchMilestone
        {
            Id = id ?? _milestoneId,
            Code = code,
            Title = title,
            ModuleId = _researchModuleId,
            MilestoneOrder = order,
            IsCapstone = isCapstone,
            AssignmentId = assignmentId ?? _assignmentId,
            IsDeleted = false,
        };
        _db.ResearchMilestones.Seed(milestone);
        return milestone;
    }

    private void SeedActivityLink(
        Guid milestoneId,
        Guid? activityId = null,
        bool required = true,
        int displayOrder = 1)
    {
        var activity = _db.Activities.Items.First(a => a.Id == (activityId ?? _activityId));
        _db.ResearchMilestoneActivities.Seed(new ResearchMilestoneActivity
        {
            Id = _linkId,
            ResearchMilestoneId = milestoneId,
            ActivityId = activity.Id,
            IsRequiredForSubmission = required,
            DisplayOrder = displayOrder,
            Activity = activity,
            IsDeleted = false,
        });
    }

    private void SeedModuleEnrollment(Guid? studentId = null)
    {
        var sid = studentId ?? _studentId;
        ClassAssignmentWindowSeed.ClassWithActiveEnrollment(
            _db,
            _classId,
            _programId,
            sid,
            _programEnrollmentId,
            _mentorId);

        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = sid,
            ModuleId = _researchModuleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        });
    }

    private CreateResearchMilestoneRequestDto BuildCreateRequest(
        int order = 1,
        string code = "MLS-NEW",
        string assignmentCode = "ASG-NEW",
        bool isCapstone = false)
    {
        return new CreateResearchMilestoneRequestDto
        {
            Code = code,
            Title = "  New Milestone  ",
            Description = "Desc",
            MilestoneOrder = order,
            IsCapstone = isCapstone,
            AssignmentCode = assignmentCode,
            AssignmentTitle = "  Deliverable  ",
            AssignmentDescription = "Submit work",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 70m,
            MaxAttempts = 2,
            TimeLimitMinutes = 60,
        };
    }

    // ── CreateMilestone ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsMilestoneAndAssignment()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        var sut = CreateSut();

        var result = await sut.CreateMilestone(_researchModuleId, BuildCreateRequest());

        Assert.Equal("MLS-NEW", result.Code);
        Assert.Equal("New Milestone", result.Title);
        Assert.Equal(1, result.MilestoneOrder);
        Assert.Equal("ASG-NEW", result.Assignment.Code);
        Assert.Equal("Deliverable", result.Assignment.Title);
        Assert.Equal(60, result.Assignment.TimeLimitMinutes);
        Assert.True(result.Assignment.IsRequiredForModulePass);
        Assert.Single(_db.ResearchMilestones.Items);
        Assert.Single(_db.Assignments.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Create_Throws_Forbidden_WhenMentor()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedResearchCurriculum();
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CreateMilestone(_researchModuleId, BuildCreateRequest()));
    }

    [Fact]
    public async Task Create_Throws_WhenModuleNotResearch()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum(includeTheoryModule: true);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateMilestone(_theoryModuleId, BuildCreateRequest()));
    }

    [Fact]
    public async Task Create_Throws_WhenClassInProgress()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedClass();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateMilestone(_researchModuleId, BuildCreateRequest()));
        Assert.Contains("in progress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Throws_WhenDuplicateMilestoneCode()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone(code: "MLS-NEW");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateMilestone(_researchModuleId, BuildCreateRequest()));
    }

    [Fact]
    public async Task Create_Throws_WhenOrderNotExceedingMax()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone(order: 1);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateMilestone(_researchModuleId, BuildCreateRequest(order: 1, code: "MLS-002", assignmentCode: "ASG-002")));
    }

    [Fact]
    public async Task Create_Throws_WhenDuplicateAssignmentCode()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment(code: "ASG-NEW");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateMilestone(_researchModuleId, BuildCreateRequest()));
    }

    [Fact]
    public async Task Create_Throws_WhenCapstoneAlreadyExists()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone(isCapstone: true);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateMilestone(_researchModuleId, BuildCreateRequest(
                order: 2,
                code: "MLS-CAP2",
                assignmentCode: "ASG-CAP2",
                isCapstone: true)));
    }

    // ── GetMilestoneById / GetMilestonesByModule ────────────────────────────

    [Fact]
    public async Task GetById_ReturnsMilestoneWithActivities()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedActivityLink(_milestoneId);
        var sut = CreateSut();

        var result = await sut.GetMilestoneById(_milestoneId);

        Assert.NotNull(result);
        Assert.Equal("Proposal", result!.Title);
        Assert.Single(result.Activities);
        Assert.Equal("ACT-RSH", result.Activities[0].ActivityCode);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenAssignmentMissing()
    {
        SeedResearchCurriculum();
        SeedMilestone();
        var sut = CreateSut();

        Assert.Null(await sut.GetMilestoneById(_milestoneId));
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenMissingOrDeleted()
    {
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        _db.ResearchMilestones.Items[0].IsDeleted = true;
        var sut = CreateSut();

        Assert.Null(await sut.GetMilestoneById(_milestoneId));
        Assert.Null(await sut.GetMilestoneById(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByModule_ReturnsOrderedMilestones()
    {
        SeedResearchCurriculum();
        SeedAssignment(id: _assignmentId, code: "ASG-001");
        SeedAssignment(id: _assignment2Id, code: "ASG-002");
        SeedMilestone(order: 2, code: "MLS-002", title: "Second", assignmentId: _assignment2Id, id: _milestone2Id);
        SeedMilestone(order: 1);
        var sut = CreateSut();

        var result = await sut.GetMilestonesByModule(_researchModuleId);

        Assert.Equal(2, result.Count);
        Assert.Equal("Proposal", result[0].Title);
        Assert.Equal("Second", result[1].Title);
    }

    [Fact]
    public async Task GetByModule_ReturnsEmpty_WhenNoMilestones()
    {
        SeedResearchCurriculum();
        var sut = CreateSut();

        var result = await sut.GetMilestonesByModule(_researchModuleId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByModule_Throws_WhenModuleMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetMilestonesByModule(_researchModuleId));
    }

    // ── UpdateMilestone ─────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesFields()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        var sut = CreateSut();

        var result = await sut.UpdateMilestone(_milestoneId, new UpdateResearchMilestoneRequestDto
        {
            Title = "  Updated  ",
            Description = "  New desc  ",
            MaxPoints = 120,
            PassScore = 80m,
            AssignmentTitle = "  New deliverable  ",
        });

        Assert.NotNull(result);
        Assert.Equal("Updated", result!.Title);
        Assert.Equal("New desc", result.Description);
        Assert.Equal(120, result.Assignment.MaxPoints);
        Assert.Equal(80m, result.Assignment.PassScore);
        Assert.Equal("New deliverable", result.Assignment.Title);
    }

    [Fact]
    public async Task Update_Throws_WhenClassInProgress()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedClass();
        SeedAssignment();
        SeedMilestone();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateMilestone(_milestoneId, new UpdateResearchMilestoneRequestDto { Title = "X" }));
        Assert.Contains("in progress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_Throws_Forbidden_WhenStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateMilestone(_milestoneId, new UpdateResearchMilestoneRequestDto { Title = "X" }));
    }

    [Fact]
    public async Task Update_SetsCapstone_AndClearsDescription()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment(id: _assignmentId);
        SeedAssignment(id: _assignment2Id, code: "ASG-002");
        SeedMilestone(order: 1);
        SeedMilestone(id: _milestone2Id, order: 2, code: "MLS-002", assignmentId: _assignment2Id);
        var sut = CreateSut();

        var result = await sut.UpdateMilestone(_milestone2Id, new UpdateResearchMilestoneRequestDto
        {
            IsCapstone = true,
            Description = "  ",
            AssignmentDescription = "  ",
        });

        Assert.True(result!.IsCapstone);
        Assert.Null(result.Description);
        Assert.Null(result.Assignment.Description);
    }

    [Fact]
    public async Task Update_Throws_WhenDuplicateOrder()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment(id: _assignmentId);
        SeedAssignment(id: _assignment2Id, code: "ASG-002");
        SeedMilestone(order: 1);
        SeedMilestone(id: _milestone2Id, order: 2, code: "MLS-002", assignmentId: _assignment2Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateMilestone(_milestone2Id, new UpdateResearchMilestoneRequestDto { MilestoneOrder = 1 }));
    }

    [Fact]
    public async Task Update_Throws_WhenAssignmentMissing()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedMilestone();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateMilestone(_milestoneId, new UpdateResearchMilestoneRequestDto { Title = "X" }));
    }

    // ── DeleteMilestone ───────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesMilestoneLinksAndAssignment()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedActivityLink(_milestoneId);
        var sut = CreateSut();

        var deleted = await sut.DeleteMilestone(_milestoneId);

        Assert.True(deleted);
        Assert.True(_db.ResearchMilestones.Items[0].IsDeleted);
        Assert.True(_db.Assignments.Items[0].IsDeleted);
        Assert.True(_db.ResearchMilestoneActivities.Items[0].IsDeleted);
    }

    [Fact]
    public async Task Delete_Throws_WhenClassInProgress()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedClass();
        SeedAssignment();
        SeedMilestone();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.DeleteMilestone(_milestoneId));
        Assert.Contains("in progress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_Throws_WhenHasSubmissions()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = _milestoneId,
            Status = SubmissionStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.DeleteMilestone(_milestoneId));
    }

    [Fact]
    public async Task Delete_Throws_Forbidden_WhenMentor()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.DeleteMilestone(_milestoneId));
    }

    // ── LinkActivity / UpdateActivityLink / UnlinkActivity ────────────────────

    [Fact]
    public async Task LinkActivity_CreatesLink_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        var sut = CreateSut();

        var result = await sut.LinkActivity(_milestoneId, new LinkMilestoneActivityRequestDto
        {
            ActivityId = _activityId,
            IsRequiredForSubmission = true,
            DisplayOrder = 1,
        });

        Assert.Equal(_activityId, result.ActivityId);
        Assert.Equal("ACT-RSH", result.ActivityCode);
        Assert.True(result.IsRequiredForSubmission);
        Assert.Single(_db.ResearchMilestoneActivities.Items);
    }

    [Fact]
    public async Task LinkActivity_AllowsMentorWithClassId()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedResearchCurriculum();
        SeedClass();
        SeedAssignment();
        SeedMilestone();
        var sut = CreateSut(_mentorId);

        var result = await sut.LinkActivity(_milestoneId, new LinkMilestoneActivityRequestDto
        {
            ActivityId = _activityId,
            ClassId = _classId,
            DisplayOrder = 1,
        });

        Assert.Equal(_activityId, result.ActivityId);
    }

    [Fact]
    public async Task LinkActivity_Throws_WhenDuplicateOrMentorMissingClassId()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedActivityLink(_milestoneId);
        var managerSut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            managerSut.LinkActivity(_milestoneId, new LinkMilestoneActivityRequestDto
            {
                ActivityId = _activityId,
                DisplayOrder = 2,
            }));

        var mentorSut = CreateSut(_mentorId);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            mentorSut.LinkActivity(_milestoneId, new LinkMilestoneActivityRequestDto
            {
                ActivityId = _activityId,
                DisplayOrder = 2,
            }));
    }

    [Fact]
    public async Task UpdateActivityLink_ChangesFields_ReturnsNullWhenMissing()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedActivityLink(_milestoneId);
        var sut = CreateSut();

        var updated = await sut.UpdateActivityLink(
            _milestoneId,
            _activityId,
            new UpdateMilestoneActivityLinkRequestDto
            {
                IsRequiredForSubmission = false,
                DisplayOrder = 5,
            });

        Assert.NotNull(updated);
        Assert.False(updated!.IsRequiredForSubmission);
        Assert.Equal(5, updated.DisplayOrder);

        var missing = await sut.UpdateActivityLink(
            _milestoneId,
            Guid.NewGuid(),
            new UpdateMilestoneActivityLinkRequestDto { DisplayOrder = 1 });
        Assert.Null(missing);
    }

    [Fact]
    public async Task UnlinkActivity_RemovesLink_ReturnsFalseWhenMissing()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedActivityLink(_milestoneId);
        var sut = CreateSut();

        Assert.True(await sut.UnlinkActivity(_milestoneId, _activityId));
        Assert.True(_db.ResearchMilestoneActivities.Items[0].IsDeleted);
        Assert.False(await sut.UnlinkActivity(_milestoneId, Guid.NewGuid()));
    }

    // ── GetStudentMilestoneProgress ───────────────────────────────────────────

    [Fact]
    public async Task GetProgress_ReturnsUnlockChain_ForStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedResearchCurriculum();
        SeedAssignment(id: _assignmentId);
        SeedAssignment(id: _assignment2Id, code: "ASG-002");
        SeedMilestone(order: 1, title: "First");
        SeedMilestone(id: _milestone2Id, order: 2, code: "MLS-002", title: "Second", assignmentId: _assignment2Id);
        SeedActivityLink(_milestoneId, required: true);
        SeedModuleEnrollment();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = _milestoneId,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 85m,
            AttemptNumber = 1,
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
        var sut = CreateSut(_studentId);

        var result = await sut.GetStudentMilestoneProgress(_moduleEnrollmentId);

        Assert.Equal(2, result.Milestones.Count);
        Assert.True(result.Milestones[0].IsUnlocked);
        Assert.True(result.Milestones[0].Passed);
        Assert.True(result.Milestones[1].IsUnlocked);
        Assert.Null(result.Milestones[1].UnlockReason);
        Assert.True(result.Milestones[1].CanSubmit);
        Assert.Empty(result.Milestones[1].SubmitBlockReasons);
    }

    [Fact]
    public async Task GetProgress_LocksSecondMilestone_UntilFirstPassed()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedResearchCurriculum();
        SeedAssignment(id: _assignmentId);
        SeedAssignment(id: _assignment2Id, code: "ASG-002");
        SeedMilestone(order: 1, title: "First");
        SeedMilestone(id: _milestone2Id, order: 2, code: "MLS-002", title: "Second", assignmentId: _assignment2Id);
        SeedModuleEnrollment();
        var sut = CreateSut(_studentId);

        var result = await sut.GetStudentMilestoneProgress(_moduleEnrollmentId);

        Assert.False(result.Milestones[1].IsUnlocked);
        Assert.Contains("First", result.Milestones[1].UnlockReason!);
    }

    [Fact]
    public async Task GetProgress_BlocksSubmit_WhenRequiredActivityIncomplete()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedResearchCurriculum();
        SeedAssignment();
        SeedMilestone();
        SeedActivityLink(_milestoneId, required: true);
        SeedModuleEnrollment();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = _milestoneId,
            Status = SubmissionStatus.Pending,
            AttemptNumber = 1,
            IsDeleted = false,
        });
        var sut = CreateSut(_studentId);

        var result = await sut.GetStudentMilestoneProgress(_moduleEnrollmentId);

        Assert.False(result.Milestones[0].CanSubmit);
        Assert.Contains("Research Reading", result.Milestones[0].SubmitBlockReasons[0]);
    }

    [Fact]
    public async Task GetProgress_ForbidsMentor_ReturnsEmptyWhenNoMilestones()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedResearchCurriculum();
        SeedModuleEnrollment();
        var mentorSut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            mentorSut.GetStudentMilestoneProgress(_moduleEnrollmentId));

        var studentSut = CreateSut(_studentId);
        var empty = await studentSut.GetStudentMilestoneProgress(_moduleEnrollmentId);
        Assert.Empty(empty.Milestones);
    }

    [Fact]
    public async Task GetProgress_Throws_WhenEnrollmentMissing()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetStudentMilestoneProgress(_moduleEnrollmentId));
    }
}
