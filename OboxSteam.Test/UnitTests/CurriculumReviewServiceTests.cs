using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class CurriculumReviewServiceTests
{
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _expertUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherExpertUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _expertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _otherExpertId = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _frameworkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _otherFrameworkId = Guid.Parse("abababab-abab-abab-abab-abababababab");
    private readonly Guid _criterionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly DateTime _now = new(2026, 8, 31, 6, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private CurriculumReviewService CreateSut(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        _currentTime.Setup(c => c.GetCurrentTime()).Returns(_now);
        var programService = new ProgramService(
            _db,
            Mock.Of<IBlobService>(),
            NullLogger<ProgramService>.Instance);
        return new CurriculumReviewService(
            _db,
            _claimsService.Object,
            programService,
            _currentTime.Object,
            NullLogger<CurriculumReviewService>.Instance);
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
            Status = AccountStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedExpert(Guid expertId, Guid userId, string code)
    {
        _db.Experts.Seed(new Expert
        {
            Id = expertId,
            Code = code,
            FullName = code,
            UserId = userId,
            IsDeleted = false,
        });
    }

    private Program SeedProgram(
        Guid? id = null,
        ProgramStatus status = ProgramStatus.Draft,
        Guid? frameworkId = null,
        string code = "PRG-001")
    {
        var program = new Program
        {
            Id = id ?? _programId,
            Code = code,
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = status,
            FrameworkId = frameworkId,
            IsDeleted = false,
        };
        _db.Programs.Seed(program);
        return program;
    }

    private ProgramFramework SeedFramework(
        Guid? id = null,
        Guid? expertId = null,
        int? minModules = null)
    {
        var framework = new ProgramFramework
        {
            Id = id ?? _frameworkId,
            ExpertId = expertId ?? _expertId,
            Name = "Robotics blueprint",
            Category = ProgramCategory.Technology,
            MinModules = minModules,
            IsDeleted = false,
        };
        _db.ProgramFrameworks.Seed(framework);
        return framework;
    }

    private void SeedStaffAndOwner()
    {
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedUser(_otherExpertUserId, RoleType.Expert, "USR-EXP2");
        SeedExpert(_expertId, _expertUserId, "EXP-001");
        SeedExpert(_otherExpertId, _otherExpertUserId, "EXP-002");
    }

    [Fact]
    public async Task Submit_NoFramework_GoesToApproved()
    {
        SeedStaffAndOwner();
        SeedProgram();
        var sut = CreateSut(_managerId);

        var result = await sut.SubmitForReviewAsync(_programId);

        Assert.Equal(ProgramStatus.Approved, result.Status);
        Assert.Equal(ProgramStatus.Approved, _db.Programs.Items.Single().Status);
    }

    [Fact]
    public async Task Submit_WithFramework_GoesToPendingReview()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(frameworkId: _frameworkId);
        var sut = CreateSut(_managerId);

        var result = await sut.SubmitForReviewAsync(_programId);

        Assert.Equal(ProgramStatus.PendingReview, result.Status);
    }

    [Fact]
    public async Task Submit_PreCheckFailure_DoesNotChangeStatus()
    {
        SeedStaffAndOwner();
        SeedFramework(minModules: 2);
        SeedProgram(frameworkId: _frameworkId);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.SubmitForReviewAsync(_programId));
        Assert.Equal(ProgramStatus.Draft, _db.Programs.Items.Single().Status);
    }

    [Fact]
    public async Task Submit_NotDraft_Conflict()
    {
        SeedStaffAndOwner();
        SeedProgram(status: ProgramStatus.Active);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.SubmitForReviewAsync(_programId));
    }

    [Fact]
    public async Task Submit_AsExpert_Forbidden()
    {
        SeedStaffAndOwner();
        SeedProgram();
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.SubmitForReviewAsync(_programId));
    }

    [Fact]
    public async Task Withdraw_PendingReview_ReturnsToDraft()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_managerId);

        var result = await sut.WithdrawReviewAsync(_programId);

        Assert.Equal(ProgramStatus.Draft, result.Status);
        Assert.Empty(_db.CurriculumReviews.Items);
    }

    [Fact]
    public async Task Withdraw_NotPending_Conflict()
    {
        SeedStaffAndOwner();
        SeedProgram(status: ProgramStatus.Draft);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.WithdrawReviewAsync(_programId));
    }

    [Fact]
    public async Task Publish_Approved_GoesToActive()
    {
        SeedStaffAndOwner();
        SeedProgram(status: ProgramStatus.Approved);
        var sut = CreateSut(_managerId);

        var result = await sut.PublishAsync(_programId);

        Assert.Equal(ProgramStatus.Active, result.Status);
    }

    [Fact]
    public async Task Publish_NotApproved_Conflict()
    {
        SeedStaffAndOwner();
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.PublishAsync(_programId));
    }

    [Fact]
    public async Task Queue_ExpertSeesOnlyOwnFrameworkPrograms()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedFramework(id: _otherFrameworkId, expertId: _otherExpertId);
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        SeedProgram(
            id: _otherProgramId,
            status: ProgramStatus.PendingReview,
            frameworkId: _otherFrameworkId,
            code: "PRG-002");
        var sut = CreateSut(_expertUserId);

        var result = await sut.GetReviewQueueAsync(1, 10);

        Assert.Single(result.Items);
        Assert.Equal(_programId, result.Items[0].Id);
        Assert.Equal(_frameworkId, result.Items[0].FrameworkId);
    }

    [Fact]
    public async Task Queue_ManagerSeesAllPending()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedFramework(id: _otherFrameworkId, expertId: _otherExpertId);
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        SeedProgram(
            id: _otherProgramId,
            status: ProgramStatus.PendingReview,
            frameworkId: _otherFrameworkId,
            code: "PRG-002");
        var sut = CreateSut(_managerId);

        var result = await sut.GetReviewQueueAsync(1, 10);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Approve_OwnerWithoutCriteria_MovesToApproved()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_expertUserId);

        var result = await sut.ApproveAsync(_programId, null);

        Assert.Equal(CurriculumReviewDecision.Approved, result.Decision);
        Assert.Equal(1, result.Round);
        Assert.Equal(_now, result.ReviewedAt);
        Assert.Equal(ProgramStatus.Approved, _db.Programs.Items.Single().Status);
        Assert.Single(_db.CurriculumReviews.Items);
    }

    [Fact]
    public async Task Approve_RequiresScores_WhenCriteriaExist()
    {
        SeedStaffAndOwner();
        SeedFramework();
        _db.FrameworkRubricCriteria.Seed(new FrameworkRubricCriterion
        {
            Id = _criterionId,
            FrameworkId = _frameworkId,
            Name = "Outcomes",
            MaxScore = 10,
            DisplayOrder = 1,
            IsDeleted = false,
        });
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.ApproveAsync(_programId, null));
        Assert.Equal(ProgramStatus.PendingReview, _db.Programs.Items.Single().Status);
    }

    [Fact]
    public async Task Approve_WithValidScores_PersistsScores()
    {
        SeedStaffAndOwner();
        SeedFramework();
        _db.FrameworkRubricCriteria.Seed(new FrameworkRubricCriterion
        {
            Id = _criterionId,
            FrameworkId = _frameworkId,
            Name = "Outcomes",
            MaxScore = 10,
            DisplayOrder = 1,
            IsDeleted = false,
        });
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_expertUserId);

        var result = await sut.ApproveAsync(_programId, new ApproveCurriculumReviewRequest
        {
            Comment = "Solid track",
            Scores =
            [
                new ReviewCriterionScoreRequest
                {
                    CriterionId = _criterionId,
                    Score = 8,
                    Comment = "Clear outcomes",
                },
            ],
        });

        Assert.Single(result.Scores);
        Assert.Equal(8, result.Scores[0].Score);
        Assert.Equal(10, result.Scores[0].MaxScore);
        Assert.Equal("Solid track", result.Comment);
    }

    [Fact]
    public async Task Approve_OtherExpert_Forbidden()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_otherExpertUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.ApproveAsync(_programId, null));
    }

    [Fact]
    public async Task Approve_Manager_Forbidden()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.ApproveAsync(_programId, null));
    }

    [Fact]
    public async Task RequestChanges_RequiresComment_AndReturnsToDraft()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sut.RequestChangesAsync(_programId, new RequestCurriculumChangesRequest { Comment = "  " }));

        var result = await sut.RequestChangesAsync(_programId, new RequestCurriculumChangesRequest
        {
            Comment = "Chỗ A sai, điều chỉnh lại.",
        });

        Assert.Equal(CurriculumReviewDecision.ChangesRequested, result.Decision);
        Assert.Equal("Chỗ A sai, điều chỉnh lại.", result.Comment);
        Assert.Equal(ProgramStatus.Draft, _db.Programs.Items.Single().Status);
    }

    [Fact]
    public async Task RequestChanges_ThenResubmit_IncrementsRound()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(status: ProgramStatus.PendingReview, frameworkId: _frameworkId);
        var sut = CreateSut(_expertUserId);

        await sut.RequestChangesAsync(_programId, new RequestCurriculumChangesRequest
        {
            Comment = "Need another live session.",
        });

        var managerSut = CreateSut(_managerId);
        await managerSut.SubmitForReviewAsync(_programId);

        var second = await CreateSut(_expertUserId).ApproveAsync(_programId, null);

        Assert.Equal(2, second.Round);
        Assert.Equal(2, _db.CurriculumReviews.Items.Count);
        Assert.Equal(ProgramStatus.Approved, _db.Programs.Items.Single().Status);
    }

    [Fact]
    public async Task GetReviews_ReturnsHistoryForOwner()
    {
        SeedStaffAndOwner();
        SeedFramework();
        SeedProgram(status: ProgramStatus.Draft, frameworkId: _frameworkId);
        _db.CurriculumReviews.Seed(new CurriculumReview
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            ProgramId = _programId,
            ExpertId = _expertId,
            Round = 1,
            Decision = CurriculumReviewDecision.ChangesRequested,
            Comment = "Fix module 1",
            ReviewedAt = _now,
            IsDeleted = false,
        });
        var sut = CreateSut(_expertUserId);

        var result = await sut.GetReviewsAsync(_programId);

        Assert.Single(result);
        Assert.Equal("Fix module 1", result[0].Comment);
    }
}
