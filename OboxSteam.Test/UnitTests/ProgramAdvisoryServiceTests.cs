using Moq;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramAdvisoryServiceTests
{
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _expertUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherExpertUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _outsiderUserId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _expertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _otherExpertId = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _outsiderExpertId = Guid.Parse("68686868-6868-6868-6868-686868686868");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _frameworkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _frameworkVersionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly Guid _criterionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly DateTime _now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly List<NotificationCommand> _published = [];

    private ProgramAdvisoryService CreateAdvisorySut(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        _currentTime.Setup(c => c.GetCurrentTime()).Returns(_now);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationCommand, CancellationToken>((command, _) => _published.Add(command))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<NotificationCommand>, CancellationToken>((commands, _) => _published.AddRange(commands))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => $"https://signed.test/{key}");
        return new ProgramAdvisoryService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            _notificationPublisher.Object,
            _blobService.Object);
    }

    private ProgramAdvisoryService CreateAdvisorySutWithReferences(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        _currentTime.Setup(c => c.GetCurrentTime()).Returns(_now);
        var resolver = new AdvisoryReferenceResolver(_db, _currentTime.Object);
        return new ProgramAdvisoryService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            _notificationPublisher.Object,
            _blobService.Object,
            resolver);
    }

    private ProgramAdvisoryDiscussionService CreateDiscussionSut(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        _currentTime.Setup(c => c.GetCurrentTime()).Returns(_now);
        return new ProgramAdvisoryDiscussionService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            new AdvisoryReferenceResolver(_db, _currentTime.Object));
    }

    private CurriculumReviewService CreateReviewSut(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        _currentTime.Setup(c => c.GetCurrentTime()).Returns(_now);
        var programService = new ProgramService(
            _db,
            Mock.Of<IBlobService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramService>.Instance);
        return new CurriculumReviewService(
            _db,
            _claimsService.Object,
            programService,
            _currentTime.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CurriculumReviewService>.Instance,
            _notificationPublisher.Object);
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

    private void SeedBase()
    {
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedUser(_otherExpertUserId, RoleType.Expert, "USR-EXP2");
        SeedUser(_outsiderUserId, RoleType.Expert, "USR-OUT");
        SeedExpert(_expertId, _expertUserId, "EXP-001");
        SeedExpert(_otherExpertId, _otherExpertUserId, "EXP-002");
        SeedExpert(_outsiderExpertId, _outsiderUserId, "EXP-OUT");
        _db.ProgramFrameworks.Seed(new ProgramFramework
        {
            Id = _frameworkId,
            ExpertId = _expertId,
            Name = "Blueprint",
            Category = ProgramCategory.Technology,
            IsDeleted = false,
        });
        _db.ProgramFrameworkVersions.Seed(new ProgramFrameworkVersion
        {
            Id = _frameworkVersionId,
            FrameworkId = _frameworkId,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAt = _now,
            IsDeleted = false,
        });
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = ProgramStatus.Draft,
            FrameworkId = _frameworkId,
            FrameworkVersionId = _frameworkVersionId,
            AdvisorExpertId = _expertId,
            IsDeleted = false,
        });
        _db.ProgramBoards.Seed(new ProgramBoard
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            ExpertId = _otherExpertId,
            RoleInBoard = "Reviewer",
            IsDeleted = false,
        });
    }

    private Guid SeedPendingSubmission()
    {
        var id = Guid.NewGuid();
        _db.ProgramReviewSubmissions.Seed(new ProgramReviewSubmission
        {
            Id = id,
            ProgramId = _programId,
            SubmissionNumber = 1,
            SubmittedByManagerId = _managerId,
            AssignedAdvisorExpertId = _expertId,
            FrameworkVersionId = _frameworkVersionId,
            CurriculumSnapshotJson = """{"programId":"22222222-2222-2222-2222-222222222222","modules":[]}""",
            RubricSnapshotJson = "[]",
            Status = ProgramReviewSubmissionStatus.Pending,
            SubmittedAt = _now,
            ConcurrencyVersion = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            IsDeleted = false,
        });
        _db.Programs.Items.Single().Status = ProgramStatus.PendingReview;
        return id;
    }

    [Fact]
    public async Task OnlyAdvisor_CanApprove()
    {
        SeedBase();
        SeedPendingSubmission();
        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateReviewSut(_otherExpertUserId).ApproveAsync(_programId, null));
        var result = await CreateReviewSut(_expertUserId).ApproveAsync(_programId, null);
        Assert.Equal(CurriculumReviewDecision.Approved, result.Decision);
    }

    [Fact]
    public async Task BoardExpert_CanSuggest_NotRequiredChange()
    {
        SeedBase();
        var advisory = CreateAdvisorySut(_otherExpertUserId);

        var suggestion = await advisory.CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.Suggestion,
            Message = "Consider another offline session.",
        });
        Assert.Equal(ProgramAdvisoryThreadType.Suggestion, suggestion.Type);

        await Assert.ThrowsAsync<ForbiddenException>(() => advisory.CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.RequiredChange,
            Message = "Must fix outcomes.",
        }));
    }

    [Fact]
    public async Task RequiredChange_BlocksApproval_SuggestionDoesNot()
    {
        SeedBase();
        var submissionId = SeedPendingSubmission();
        await CreateAdvisorySut(_expertUserId).CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            SubmissionId = submissionId,
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.RequiredChange,
            Message = "Fix module order.",
        });

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateReviewSut(_expertUserId).ApproveAsync(_programId, null));

        _db.ProgramAdvisoryThreads.Items.Single().Status = ProgramAdvisoryThreadStatus.Resolved;
        await CreateAdvisorySut(_otherExpertUserId).CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            SubmissionId = submissionId,
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.Suggestion,
            Message = "Optional polish.",
        });

        var approved = await CreateReviewSut(_expertUserId).ApproveAsync(_programId, null);
        Assert.Equal(CurriculumReviewDecision.Approved, approved.Decision);
    }

    [Fact]
    public async Task ActiveReview_RequiresSubmissionIdForNewThreads()
    {
        SeedBase();
        SeedPendingSubmission();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateAdvisorySut(_expertUserId).CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
            {
                TargetType = ProgramAdvisoryTargetType.Program,
                Type = ProgramAdvisoryThreadType.Suggestion,
                Message = "Anchor this suggestion to the submitted review.",
            }));
    }

    [Fact]
    public async Task Manager_CannotCreateAdvisoryThread()
    {
        SeedBase();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateAdvisorySut(_managerId).CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
            {
                TargetType = ProgramAdvisoryTargetType.Program,
                Type = ProgramAdvisoryThreadType.Suggestion,
                Message = "Managers only address expert feedback.",
            }));
    }

    [Fact]
    public async Task FieldAnchor_AndLatestPreview_AreReturned()
    {
        SeedBase();
        var submissionId = SeedPendingSubmission();

        var result = await CreateAdvisorySut(_expertUserId).CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            SubmissionId = submissionId,
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.Suggestion,
            AnchorKind = ProgramAdvisoryAnchorKind.Field,
            AnchorField = "description",
            Message = "Please make the program description more specific.",
        });

        Assert.Equal(ProgramAdvisoryAnchorKind.Field, result.AnchorKind);
        Assert.Equal("description", result.AnchorField);
        Assert.Equal("Please make the program description more specific.", result.LatestMessagePreview);
        Assert.Equal(ProgramAdvisoryAnchorKind.Field, _db.ProgramAdvisoryThreads.Items.Single().AnchorKind);
    }

    [Fact]
    public async Task Board_ReturnsSubmissionTreePinsAndChanges()
    {
        SeedBase();
        var submissionId = SeedPendingSubmission();
        await CreateAdvisorySut(_expertUserId).CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            SubmissionId = submissionId,
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.Suggestion,
            Message = "Use a stronger project brief.",
        });

        var board = await CreateAdvisorySut(_expertUserId).GetBoardAsync(_programId, submissionId);

        Assert.Equal(submissionId, board.SubmissionId);
        Assert.Equal(_programId, board.Program.Id);
        Assert.Equal("Robotics", board.Program.Name);
        Assert.Empty(board.Curriculum.Modules);
        var pin = Assert.Single(board.ThreadPins);
        Assert.Equal(submissionId, pin.SubmissionId);
        Assert.Equal("Use a stronger project brief.", pin.LastMessagePreview);
        Assert.Equal(1, pin.MessageCount);
    }

    [Fact]
    public async Task DraftAutosave_AndStaleConcurrency_Conflict()
    {
        SeedBase();
        var submissionId = SeedPendingSubmission();
        var review = CreateReviewSut(_expertUserId);

        var first = await review.SaveDraftAsync(_programId, submissionId, new SaveProgramReviewDraftRequest
        {
            ConcurrencyVersion = Guid.Empty,
            OverallComment = "WIP",
            Scores =
            [
                new ReviewCriterionScoreRequest { CriterionId = _criterionId, Score = 5 },
            ],
        });
        Assert.Equal("WIP", first.OverallComment);
        Assert.NotEqual(Guid.Empty, first.ConcurrencyVersion);

        await Assert.ThrowsAsync<ConflictException>(() => review.SaveDraftAsync(
            _programId,
            submissionId,
            new SaveProgramReviewDraftRequest
            {
                ConcurrencyVersion = Guid.Empty,
                OverallComment = "stale",
            }));

        var second = await review.SaveDraftAsync(_programId, submissionId, new SaveProgramReviewDraftRequest
        {
            ConcurrencyVersion = first.ConcurrencyVersion,
            OverallComment = "updated",
        });
        Assert.Equal("updated", second.OverallComment);
    }

    [Fact]
    public async Task Submit_CreatesSnapshotSubmission()
    {
        SeedBase();
        var result = await CreateReviewSut(_managerId).SubmitForReviewAsync(_programId);
        Assert.Equal(ProgramStatus.PendingReview, result.Status);
        var submission = Assert.Single(_db.ProgramReviewSubmissions.Items);
        Assert.Equal(ProgramReviewSubmissionStatus.Pending, submission.Status);
        Assert.False(string.IsNullOrWhiteSpace(submission.CurriculumSnapshotJson));
        Assert.Contains("modules", submission.CurriculumSnapshotJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestChanges_RetainsScores()
    {
        SeedBase();
        _db.FrameworkRubricCriteria.Seed(new FrameworkRubricCriterion
        {
            Id = _criterionId,
            FrameworkId = _frameworkId,
            FrameworkVersionId = _frameworkVersionId,
            Name = "Outcomes",
            Description = "Clear outcomes",
            EvidenceGuidance = "List outcomes",
            MaxScore = 10,
            DisplayOrder = 1,
            IsDeleted = false,
        });
        SeedPendingSubmission();

        var result = await CreateReviewSut(_expertUserId).RequestChangesAsync(
            _programId,
            new RequestCurriculumChangesRequest
            {
                Comment = "Need clearer outcomes.",
                Scores =
                [
                    new ReviewCriterionScoreRequest
                    {
                        CriterionId = _criterionId,
                        Score = 4,
                        Comment = "Incomplete",
                    },
                ],
            });

        Assert.Single(result.Scores);
        Assert.Equal(4, result.Scores[0].Score);
        Assert.Equal("Outcomes", result.Scores[0].CriterionName);
        Assert.Equal(10, result.Scores[0].MaxScore);
        var persisted = Assert.Single(_db.ReviewCriterionScores.Items);
        Assert.Equal("Outcomes", persisted.CriterionNameSnapshot);
        Assert.Equal(10, persisted.MaxScoreSnapshot);
    }

    [Fact]
    public async Task Withdraw_ClosesSubmission()
    {
        SeedBase();
        SeedPendingSubmission();
        await CreateReviewSut(_managerId).WithdrawReviewAsync(_programId);
        Assert.Equal(ProgramReviewSubmissionStatus.Withdrawn, _db.ProgramReviewSubmissions.Items.Single().Status);
        Assert.Equal(ProgramStatus.Draft, _db.Programs.Items.Single().Status);
    }

    [Fact]
    public async Task Unauthorized_CannotReadThreadsOrDrafts()
    {
        SeedBase();
        var submissionId = SeedPendingSubmission();
        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateAdvisorySut(_outsiderUserId).GetThreadsAsync(_programId));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateReviewSut(_outsiderUserId).GetDraftAsync(_programId, submissionId));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateReviewSut(_otherExpertUserId).GetDraftAsync(_programId, submissionId));
    }

    [Fact]
    public async Task LegacyReviews_SnapshotAvailableFalse()
    {
        SeedBase();
        _db.CurriculumReviews.Seed(new CurriculumReview
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            ExpertId = _expertId,
            Round = 1,
            Decision = CurriculumReviewDecision.ChangesRequested,
            Comment = "Legacy",
            ReviewedAt = _now,
            SnapshotAvailable = false,
            SubmissionId = null,
            IsDeleted = false,
        });

        var history = await CreateReviewSut(_expertUserId).GetReviewsAsync(_programId);
        var row = Assert.Single(history);
        Assert.False(row.SnapshotAvailable);
        Assert.Null(row.SubmissionId);
    }

    [Fact]
    public async Task RequestChanges_ReferencesExistingRequirement_WithoutCreatingDuplicate()
    {
        SeedBase();
        var firstSubmissionId = SeedPendingSubmission();
        var advisory = CreateAdvisorySut(_expertUserId);
        var thread = await advisory.CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            SubmissionId = firstSubmissionId,
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.RequiredChange,
            Message = "Clarify the assessment evidence.",
        });

        var review = await CreateReviewSut(_expertUserId).RequestChangesAsync(
            _programId,
            new RequestCurriculumChangesRequest
            {
                SubmissionId = firstSubmissionId,
                ConcurrencyVersion = _db.ProgramReviewSubmissions.Items.Single().ConcurrencyVersion,
                Comment = "Please address the existing requirement.",
                RequiredChangeThreadIds = [thread.Id],
            });

        Assert.Equal(CurriculumReviewDecision.ChangesRequested, review.Decision);
        Assert.Single(_db.ProgramAdvisoryThreads.Items);
        Assert.Single(_db.CurriculumReviewRequirements.Items);
        Assert.Equal(thread.Id, _db.CurriculumReviewRequirements.Items.Single().ThreadId);
    }

    [Fact]
    public async Task ThreadStatus_RequiresVersion_AndRecordsVerifiedNewSubmission()
    {
        SeedBase();
        var firstSubmissionId = SeedPendingSubmission();
        var advisory = CreateAdvisorySut(_expertUserId);
        var thread = await advisory.CreateThreadAsync(_programId, new CreateAdvisoryThreadRequest
        {
            SubmissionId = firstSubmissionId,
            TargetType = ProgramAdvisoryTargetType.Program,
            Type = ProgramAdvisoryThreadType.RequiredChange,
            Message = "Clarify the learning outcome.",
        });

        await Assert.ThrowsAsync<ConflictException>(() => advisory.UpdateThreadStatusAsync(
            _programId,
            thread.Id,
            new UpdateAdvisoryThreadStatusRequest
            {
                Status = ProgramAdvisoryThreadStatus.Open,
            }));

        var addressed = await CreateAdvisorySut(_managerId).UpdateThreadStatusAsync(
            _programId,
            thread.Id,
            new UpdateAdvisoryThreadStatusRequest
            {
                Status = ProgramAdvisoryThreadStatus.Addressed,
                ConcurrencyVersion = thread.ConcurrencyVersion,
                Message = "Updated the outcome text and explained the correction.",
            });

        var secondSubmissionId = Guid.NewGuid();
        _db.ProgramReviewSubmissions.Seed(new ProgramReviewSubmission
        {
            Id = secondSubmissionId,
            ProgramId = _programId,
            SubmissionNumber = 2,
            SubmittedByManagerId = _managerId,
            AssignedAdvisorExpertId = _expertId,
            FrameworkVersionId = _frameworkVersionId,
            CurriculumSnapshotJson = "{\"programId\":\"22222222-2222-2222-2222-222222222222\",\"modules\":[]}",
            RubricSnapshotJson = "[]",
            Status = ProgramReviewSubmissionStatus.Pending,
            SubmittedAt = _now,
            ConcurrencyVersion = Guid.NewGuid(),
            IsDeleted = false,
        });

        var resolved = await CreateAdvisorySut(_expertUserId).UpdateThreadStatusAsync(
            _programId,
            thread.Id,
            new UpdateAdvisoryThreadStatusRequest
            {
                Status = ProgramAdvisoryThreadStatus.Resolved,
                ConcurrencyVersion = addressed.ConcurrencyVersion,
                ResolutionKind = AdvisoryResolutionKind.Verified,
                VerifiedAgainstSubmissionId = secondSubmissionId,
                Message = "Verified against the resubmitted round.",
            });

        Assert.Equal(ProgramAdvisoryThreadStatus.Resolved, resolved.Status);
        Assert.Equal(AdvisoryResolutionKind.Verified, resolved.Events.Single().ResolutionKind);
        Assert.Equal(secondSubmissionId, resolved.Events.Single().VerifiedAgainstSubmissionId);
    }

    [Fact]
    public async Task DiscussionRetry_IsIdempotent_AndForeignCursorIsRejected()
    {
        SeedBase();
        var discussion = CreateDiscussionSut(_managerId);
        var request = new PostAdvisoryDiscussionMessageRequest
        {
            Text = "Can we clarify the activity evidence?",
            ClientMessageId = "client-message-1",
        };

        var first = await discussion.AddMessageAsync(_programId, request);
        var retry = await discussion.AddMessageAsync(_programId, request);

        Assert.Equal(first.Id, retry.Id);
        Assert.Single(_db.ProgramAdvisoryDiscussionMessages.Items);
        await Assert.ThrowsAsync<BadRequestException>(() => discussion.GetMessagesAsync(
            _programId,
            "33333333-3333-3333-3333-333333333333:1",
            null,
            30));
    }

    [Fact]
    public async Task WorkingDraftReference_CapturesAuthorizedFieldContext()
    {
        SeedBase();
        _db.Programs.Items.Single().Description = "Persisted program description";

        var reference = await CreateAdvisorySutWithReferences(_managerId).CreateReferenceAsync(
            _programId,
            new CreateAdvisoryReferenceRequest
            {
                Context = AdvisoryReferenceContext.WorkingDraft,
                TargetType = ProgramAdvisoryTargetType.Program,
                AnchorKind = ProgramAdvisoryAnchorKind.Field,
                FieldKey = "description",
            });

        Assert.Equal(_programId, reference.ProgramId);
        Assert.Equal("Persisted program description", reference.CapturedExcerpt);
        Assert.True(reference.IsAvailable);
    }
}
