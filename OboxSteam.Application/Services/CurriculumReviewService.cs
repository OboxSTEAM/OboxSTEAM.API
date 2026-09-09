using System.Text.Json;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class CurriculumReviewService : ICurriculumReviewService
{
    private static readonly JsonSerializerOptions DraftJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IProgramService _programService;
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<CurriculumReviewService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public CurriculumReviewService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IProgramService programService,
        ICurrentTime currentTime,
        ILogger<CurriculumReviewService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _programService = programService;
        _currentTime = currentTime;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<ProgramsResponseDto> SubmitForReviewAsync(Guid programId)
    {
        var actor = await RequireManagerOrAdminAsync();
        var program = await GetActiveProgramAsync(programId);

        if (program.Status != ProgramStatus.Draft)
        {
            throw ErrorHelper.Conflict("Only Draft programs can be submitted for review.");
        }

        if (!program.AdvisorExpertId.HasValue)
        {
            throw ErrorHelper.BadRequest("Assign a responsible expert before submitting for review.");
        }

        await ProgramFrameworkValidator.ValidateForSubmitAsync(_unitOfWork, programId);

        ProgramFramework? framework = null;
        if (program.FrameworkId.HasValue)
        {
            framework = await RequireActiveFrameworkAsync(program.FrameworkId.Value);
        }

        var reviewers = await ResolveSubmitReviewersAsync(program);
        var advisor = reviewers[0];
        var now = _currentTime.GetCurrentTime();

        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
        var criteria = await LoadVersionCriteriaAsync(program.FrameworkVersionId);
        var curriculumJson = CurriculumReviewSnapshotBuilder.BuildCurriculumSnapshotJson(tree);
        var rubricJson = CurriculumReviewSnapshotBuilder.BuildRubricSnapshotJson(criteria);

        var existing = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
            s => s.ProgramId == program.Id && !s.IsDeleted);
        var nextNumber = existing.Count == 0 ? 1 : existing.Max(s => s.SubmissionNumber) + 1;

        var submission = new ProgramReviewSubmission
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            SubmissionNumber = nextNumber,
            SubmittedByManagerId = actor.Id,
            AssignedAdvisorExpertId = advisor.Id,
            FrameworkVersionId = program.FrameworkVersionId,
            CurriculumSnapshotJson = curriculumJson,
            RubricSnapshotJson = rubricJson,
            Status = ProgramReviewSubmissionStatus.Pending,
            SubmittedAt = now,
            ConcurrencyVersion = Guid.NewGuid(),
            CreatedAt = now,
            CreatedBy = actor.Id,
        };

        program.Status = ProgramStatus.PendingReview;
        await _unitOfWork.ProgramReviewSubmissions.AddAsync(submission);
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[SubmitForReview] Program {ProgramId} submission {SubmissionNumber} by {UserId}.",
            program.Id,
            submission.SubmissionNumber,
            actor.Id);

        await PublishCurriculumReviewSubmittedAsync(program, framework, reviewers, actor);
        return await _programService.GetProgramByIdAsync(programId);
    }

    public async Task<ProgramsResponseDto> WithdrawReviewAsync(Guid programId)
    {
        var actor = await RequireManagerOrAdminAsync();
        var program = await GetActiveProgramAsync(programId);

        if (program.Status is not (ProgramStatus.PendingReview or ProgramStatus.Approved))
        {
            throw ErrorHelper.Conflict("Only programs pending expert review or approved for publish can be withdrawn.");
        }

        var now = _currentTime.GetCurrentTime();
        if (program.Status == ProgramStatus.PendingReview)
        {
            var pending = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
                s => s.ProgramId == program.Id
                     && s.Status == ProgramReviewSubmissionStatus.Pending
                     && !s.IsDeleted);
            foreach (var submission in pending)
            {
                submission.Status = ProgramReviewSubmissionStatus.Withdrawn;
                submission.ClosedAt = now;
                submission.ConcurrencyVersion = Guid.NewGuid();
                submission.UpdatedAt = now;
                submission.UpdatedBy = actor.Id;
                await _unitOfWork.ProgramReviewSubmissions.Update(submission);
            }
        }

        program.Status = ProgramStatus.Draft;
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[WithdrawReview] Program {ProgramId} withdrawn to Draft by {UserId}.",
            program.Id,
            actor.Id);

        return await _programService.GetProgramByIdAsync(programId);
    }

    public async Task<ProgramsResponseDto> PublishAsync(Guid programId)
    {
        var actor = await RequireManagerOrAdminAsync();
        var program = await GetActiveProgramAsync(programId);

        if (program.Status != ProgramStatus.Approved)
        {
            throw ErrorHelper.Conflict("Only Approved programs can be published.");
        }

        program.Status = ProgramStatus.Active;
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[Publish] Program {ProgramId} published to Active by {UserId}.",
            program.Id,
            actor.Id);

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.CurriculumReviewPublished(
                program.Id,
                actor.Id,
                program.Name,
                DisplayName(actor)));

        return await _programService.GetProgramByIdAsync(programId);
    }

    public async Task<Pagination<ProgramReviewQueueItemDto>> GetReviewQueueAsync(int page, int pageSize)
    {
        var actor = await ResolveReviewActorAsync();

        var pending = await _unitOfWork.Programs.GetAllAsync(
            p => p.Status == ProgramStatus.PendingReview && !p.IsDeleted);

        if (actor.Role == RoleType.Expert)
        {
            var expert = await RequireCurrentExpertAsync(actor);
            pending = pending.Where(p => p.AdvisorExpertId == expert.Id).ToList();
        }

        var frameworkIds = pending
            .Where(p => p.FrameworkId.HasValue)
            .Select(p => p.FrameworkId!.Value)
            .Distinct()
            .ToList();
        var frameworksById = frameworkIds.Count == 0
            ? new Dictionary<Guid, ProgramFramework>()
            : (await _unitOfWork.ProgramFrameworks.GetAllAsync(
                f => frameworkIds.Contains(f.Id) && !f.IsDeleted))
            .ToDictionary(f => f.Id);

        var ordered = pending
            .OrderBy(p => p.UpdatedAt ?? p.CreatedAt)
            .ThenBy(p => p.Name)
            .ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p =>
            {
                ProgramFramework? framework = null;
                if (p.FrameworkId.HasValue)
                {
                    frameworksById.TryGetValue(p.FrameworkId.Value, out framework);
                }

                return new ProgramReviewQueueItemDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Status = p.Status,
                    FrameworkId = p.FrameworkId,
                    FrameworkName = framework?.Name,
                    ExpertId = p.AdvisorExpertId,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                };
            })
            .ToList();

        return new Pagination<ProgramReviewQueueItemDto>(pageItems, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<CurriculumReviewResponseDto>> GetReviewsAsync(Guid programId)
    {
        var actor = await ResolveReviewActorAsync();
        var program = await GetActiveProgramAsync(programId);

        if (actor.Role == RoleType.Expert)
        {
            await EnsureExpertCanViewReviewAsync(actor, program);
        }

        var reviews = await _unitOfWork.CurriculumReviews.GetAllAsync(
            r => r.ProgramId == programId && !r.IsDeleted);
        var ordered = reviews.OrderBy(r => r.Round).ToList();
        if (ordered.Count == 0)
        {
            return [];
        }

        var expertIds = ordered.Select(r => r.ExpertId).Distinct().ToList();
        var experts = await _unitOfWork.Experts.GetAllAsync(e => expertIds.Contains(e.Id) && !e.IsDeleted);
        var expertsById = experts.ToDictionary(e => e.Id);

        var reviewIds = ordered.Select(r => r.Id).ToList();
        var scores = await _unitOfWork.ReviewCriterionScores.GetAllAsync(
            s => reviewIds.Contains(s.CurriculumReviewId) && !s.IsDeleted);
        var scoresByReviewId = scores
            .GroupBy(s => s.CurriculumReviewId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var criterionIds = scores.Select(s => s.FrameworkRubricCriterionId).Distinct().ToList();
        var criteria = criterionIds.Count == 0
            ? []
            : await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
                c => criterionIds.Contains(c.Id));
        var criteriaById = criteria.ToDictionary(c => c.Id);

        return ordered
            .Select(review => MapReview(
                review,
                expertsById.GetValueOrDefault(review.ExpertId),
                scoresByReviewId.GetValueOrDefault(review.Id) ?? [],
                criteriaById))
            .ToList();
    }

    public async Task<CurriculumReviewResponseDto> ApproveAsync(
        Guid programId,
        ApproveCurriculumReviewRequest? request)
    {
        var (program, expert, criteria, actor) = await RequirePendingDecisionAsync(programId);
        var submission = await ResolvePendingSubmissionAsync(program.Id, request?.SubmissionId);
        EnsureSubmissionConcurrency(submission, request?.ConcurrencyVersion);

        var openRequired = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == program.Id
                 && t.Type == ProgramAdvisoryThreadType.RequiredChange
                 && t.Status != ProgramAdvisoryThreadStatus.Resolved
                 && !t.IsDeleted);
        if (openRequired.Count > 0)
        {
            throw ErrorHelper.Conflict(
                "Resolve all required-change threads before approving this program.");
        }

        var comment = CurriculumReviewValidator.NormalizeOptionalComment(request?.Comment);
        var reviewId = Guid.NewGuid();
        var scoreRows = CurriculumReviewValidator.BuildScores(reviewId, criteria, request?.Scores);
        var now = _currentTime.GetCurrentTime();

        var review = await PersistDecisionAsync(
            program,
            expert,
            CurriculumReviewDecision.Approved,
            comment,
            reviewId,
            scoreRows,
            submission.Id,
            snapshotAvailable: true);

        submission.Status = ProgramReviewSubmissionStatus.Approved;
        submission.ClosedAt = now;
        submission.ConcurrencyVersion = Guid.NewGuid();
        submission.UpdatedAt = now;
        submission.UpdatedBy = actor.Id;
        await _unitOfWork.ProgramReviewSubmissions.Update(submission);

        program.Status = ProgramStatus.Approved;
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[ApproveReview] Expert {ExpertId} approved program {ProgramId} round {Round}.",
            expert.Id,
            program.Id,
            review.Round);

        await PublishCurriculumReviewDecisionAsync(program, review, actor);
        return await MapReviewAsync(review);
    }

    public async Task<CurriculumReviewResponseDto> RequestChangesAsync(
        Guid programId,
        RequestCurriculumChangesRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var (program, expert, criteria, actor) = await RequirePendingDecisionAsync(programId);
        var submission = await ResolvePendingSubmissionAsync(program.Id, request.SubmissionId);
        EnsureSubmissionConcurrency(submission, request.ConcurrencyVersion);

        var comment = CurriculumReviewValidator.RequireComment(request.Comment);
        var reviewId = Guid.NewGuid();
        var scoreRows = CurriculumReviewValidator.BuildPartialScores(reviewId, criteria, request.Scores);
        var now = _currentTime.GetCurrentTime();

        var review = await PersistDecisionAsync(
            program,
            expert,
            CurriculumReviewDecision.ChangesRequested,
            comment,
            reviewId,
            scoreRows,
            submission.Id,
            snapshotAvailable: true);

        var thread = new ProgramAdvisoryThread
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            AuthorUserId = actor.Id,
            SubmissionId = submission.Id,
            TargetType = ProgramAdvisoryTargetType.Program,
            TargetId = program.Id,
            TargetLabel = program.Name,
            TargetContext = "Overall request-changes decision",
            Type = ProgramAdvisoryThreadType.RequiredChange,
            Status = ProgramAdvisoryThreadStatus.Open,
            LastMessageAt = now,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        var message = new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = actor.Id,
            Message = comment,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
        await _unitOfWork.ProgramAdvisoryThreads.AddAsync(thread);
        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(message);

        submission.Status = ProgramReviewSubmissionStatus.ChangesRequested;
        submission.ClosedAt = now;
        submission.ConcurrencyVersion = Guid.NewGuid();
        submission.UpdatedAt = now;
        submission.UpdatedBy = actor.Id;
        await _unitOfWork.ProgramReviewSubmissions.Update(submission);

        program.Status = ProgramStatus.Draft;
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[RequestChanges] Expert {ExpertId} requested changes on program {ProgramId} round {Round}.",
            expert.Id,
            program.Id,
            review.Round);

        await PublishCurriculumReviewDecisionAsync(program, review, actor);
        return await MapReviewAsync(review);
    }

    public async Task<FrameworkCheckDto> GetFrameworkCheckAsync(Guid programId)
    {
        await ResolveReviewActorAsync();
        var program = await GetActiveProgramAsync(programId);
        var dto = new FrameworkCheckDto
        {
            ProgramId = program.Id,
            FrameworkVersionId = program.FrameworkVersionId,
            AllPassed = true,
        };

        if (!program.FrameworkVersionId.HasValue)
        {
            return dto;
        }

        var version = await _unitOfWork.ProgramFrameworkVersions.GetByIdAsync(program.FrameworkVersionId.Value);
        if (version == null || version.IsDeleted || !version.IsPublished)
        {
            throw ErrorHelper.Conflict("The assigned framework version is unavailable or not published.");
        }

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
        dto.Checks = BuildStructuredChecks(version, snapshot);
        dto.AllPassed = dto.Checks.TrueForAll(c => c.Passed);
        return dto;
    }

    public async Task<IReadOnlyList<ProgramReviewSubmissionSummaryDto>> GetSubmissionsAsync(Guid programId)
    {
        await EnsureCanAccessSubmissionsAsync(programId);
        var rows = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
            s => s.ProgramId == programId && !s.IsDeleted);
        return rows
            .OrderByDescending(s => s.SubmissionNumber)
            .Select(MapSubmissionSummary)
            .ToList();
    }

    public async Task<ProgramReviewSubmissionDetailDto> GetSubmissionAsync(Guid programId, Guid submissionId)
    {
        await EnsureCanAccessSubmissionsAsync(programId);
        var submission = await RequireSubmissionAsync(programId, submissionId);
        return MapSubmissionDetail(submission);
    }

    public async Task<SubmissionChangesDto> GetSubmissionChangesAsync(Guid programId, Guid submissionId)
    {
        await EnsureCanAccessSubmissionsAsync(programId);
        var submission = await RequireSubmissionAsync(programId, submissionId);
        var previous = (await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
                s => s.ProgramId == programId
                     && s.SubmissionNumber < submission.SubmissionNumber
                     && !s.IsDeleted))
            .OrderByDescending(s => s.SubmissionNumber)
            .FirstOrDefault();

        return CurriculumReviewSnapshotBuilder.Diff(
            submission.Id,
            previous?.Id,
            previous?.CurriculumSnapshotJson,
            submission.CurriculumSnapshotJson);
    }

    public async Task<ProgramReviewDraftDto> GetDraftAsync(Guid programId, Guid submissionId)
    {
        var (submission, expert) = await RequireAdvisorDraftAccessAsync(programId, submissionId);
        var draft = await _unitOfWork.ProgramReviewDrafts.FirstOrDefaultAsync(
            d => d.SubmissionId == submission.Id && d.AdvisorExpertId == expert.Id && !d.IsDeleted);

        if (draft == null)
        {
            return new ProgramReviewDraftDto
            {
                SubmissionId = submission.Id,
                Scores = [],
                OverallComment = null,
                ConcurrencyVersion = Guid.Empty,
                LastSavedAt = null,
            };
        }

        return MapDraft(draft);
    }

    public async Task<ProgramReviewDraftDto> SaveDraftAsync(
        Guid programId,
        Guid submissionId,
        SaveProgramReviewDraftRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var (submission, expert) = await RequireAdvisorDraftAccessAsync(programId, submissionId);
        if (submission.Status != ProgramReviewSubmissionStatus.Pending)
        {
            throw ErrorHelper.Conflict("Drafts can only be saved for pending submissions.");
        }

        var now = _currentTime.GetCurrentTime();
        var actor = await GetCurrentUserAsync();
        var draft = await _unitOfWork.ProgramReviewDrafts.FirstOrDefaultAsync(
            d => d.SubmissionId == submission.Id && d.AdvisorExpertId == expert.Id && !d.IsDeleted);

        var scoresJson = JsonSerializer.Serialize(request.Scores ?? [], DraftJsonOptions);
        var comment = CurriculumReviewValidator.NormalizeOptionalComment(request.OverallComment);

        if (draft == null)
        {
            draft = new ProgramReviewDraft
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                AdvisorExpertId = expert.Id,
                ScoresJson = scoresJson,
                OverallComment = comment,
                ConcurrencyVersion = Guid.NewGuid(),
                LastSavedAt = now,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };
            await _unitOfWork.ProgramReviewDrafts.AddAsync(draft);
        }
        else
        {
            if (draft.ConcurrencyVersion != request.ConcurrencyVersion)
            {
                throw ErrorHelper.Conflict("Draft was updated elsewhere. Reload and try again.");
            }

            draft.ScoresJson = scoresJson;
            draft.OverallComment = comment;
            draft.ConcurrencyVersion = Guid.NewGuid();
            draft.LastSavedAt = now;
            draft.UpdatedAt = now;
            draft.UpdatedBy = actor.Id;
            await _unitOfWork.ProgramReviewDrafts.Update(draft);
        }

        await _unitOfWork.SaveChangesAsync();
        return MapDraft(draft);
    }

    private async Task EnsureCanAccessSubmissionsAsync(Guid programId)
    {
        var actor = await ResolveReviewActorAsync();
        var program = await GetActiveProgramAsync(programId);
        if (actor.Role == RoleType.Expert)
        {
            await EnsureExpertCanViewReviewAsync(actor, program);
        }
    }

    private async Task<(ProgramReviewSubmission Submission, Expert Expert)> RequireAdvisorDraftAccessAsync(
        Guid programId,
        Guid submissionId)
    {
        var actor = await ResolveReviewActorAsync();
        if (actor.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden("Only the assigned advisor can access review drafts.");
        }

        var program = await GetActiveProgramAsync(programId);
        var expert = await RequireCurrentExpertAsync(actor);
        if (program.AdvisorExpertId != expert.Id)
        {
            throw ErrorHelper.Forbidden("Only the assigned advisor can access review drafts.");
        }

        var submission = await RequireSubmissionAsync(programId, submissionId);
        return (submission, expert);
    }

    private async Task<ProgramReviewSubmission> RequireSubmissionAsync(Guid programId, Guid submissionId)
    {
        var submission = await _unitOfWork.ProgramReviewSubmissions.GetByIdAsync(submissionId);
        if (submission == null || submission.IsDeleted || submission.ProgramId != programId)
        {
            throw ErrorHelper.NotFound($"Review submission '{submissionId}' was not found.");
        }

        return submission;
    }

    private async Task<ProgramReviewSubmission> ResolvePendingSubmissionAsync(
        Guid programId,
        Guid? submissionId)
    {
        if (submissionId.HasValue)
        {
            var specific = await RequireSubmissionAsync(programId, submissionId.Value);
            if (specific.Status != ProgramReviewSubmissionStatus.Pending)
            {
                throw ErrorHelper.Conflict("This submission is no longer pending a decision.");
            }

            return specific;
        }

        var pending = await _unitOfWork.ProgramReviewSubmissions.GetAllAsync(
            s => s.ProgramId == programId
                 && s.Status == ProgramReviewSubmissionStatus.Pending
                 && !s.IsDeleted);
        if (pending.Count == 0)
        {
            throw ErrorHelper.Conflict("No pending review submission exists for this program.");
        }

        if (pending.Count > 1)
        {
            throw ErrorHelper.Conflict("Multiple pending submissions exist; specify submissionId.");
        }

        return pending[0];
    }

    private static void EnsureSubmissionConcurrency(ProgramReviewSubmission submission, Guid? concurrencyVersion)
    {
        if (submission.Status != ProgramReviewSubmissionStatus.Pending)
        {
            throw ErrorHelper.Conflict("This submission is no longer pending a decision.");
        }

        if (concurrencyVersion.HasValue && concurrencyVersion.Value != submission.ConcurrencyVersion)
        {
            throw ErrorHelper.Conflict("Submission was updated elsewhere. Reload and try again.");
        }
    }

    private async Task<CurriculumReview> PersistDecisionAsync(
        Program program,
        Expert expert,
        CurriculumReviewDecision decision,
        string? comment,
        Guid reviewId,
        IReadOnlyList<ReviewCriterionScore> scores,
        Guid submissionId,
        bool snapshotAvailable)
    {
        var existing = await _unitOfWork.CurriculumReviews.GetAllAsync(
            r => r.ProgramId == program.Id && !r.IsDeleted);
        var nextRound = existing.Count == 0 ? 1 : existing.Max(r => r.Round) + 1;

        var review = new CurriculumReview
        {
            Id = reviewId,
            ProgramId = program.Id,
            ExpertId = expert.Id,
            Round = nextRound,
            SubmissionId = submissionId,
            SnapshotAvailable = snapshotAvailable,
            Decision = decision,
            Comment = comment,
            ReviewedAt = _currentTime.GetCurrentTime(),
        };

        await _unitOfWork.CurriculumReviews.AddAsync(review);
        if (scores.Count > 0)
        {
            await _unitOfWork.ReviewCriterionScores.AddRangeAsync(scores.ToList());
        }

        return review;
    }

    private async Task<(
        Program Program,
        Expert Expert,
        List<FrameworkRubricCriterion> Criteria,
        User Actor)> RequirePendingDecisionAsync(Guid programId)
    {
        var actor = await ResolveReviewActorAsync();
        if (actor.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden("Only an expert can decide a curriculum review.");
        }

        var program = await GetActiveProgramAsync(programId);
        if (program.Status != ProgramStatus.PendingReview)
        {
            throw ErrorHelper.Conflict("Only programs pending expert review can receive a decision.");
        }

        var expert = await RequireCurrentExpertAsync(actor);
        if (program.AdvisorExpertId != expert.Id)
        {
            throw ErrorHelper.Forbidden("Only the assigned responsible expert can decide this program review.");
        }

        var criteria = await LoadVersionCriteriaAsync(program.FrameworkVersionId);
        return (program, expert, criteria, actor);
    }

    private async Task<List<FrameworkRubricCriterion>> LoadVersionCriteriaAsync(Guid? frameworkVersionId)
    {
        if (!frameworkVersionId.HasValue)
        {
            return [];
        }

        var version = await _unitOfWork.ProgramFrameworkVersions.GetByIdAsync(frameworkVersionId.Value);
        if (version == null || version.IsDeleted || !version.IsPublished)
        {
            throw ErrorHelper.Conflict("The pinned framework version is unavailable.");
        }

        var rows = await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
            c => c.FrameworkVersionId == version.Id && !c.IsDeleted);
        return rows.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList();
    }

    private static List<FrameworkCheckItemDto> BuildStructuredChecks(
        ProgramFrameworkVersion framework,
        ProgramCurriculumTreeSnapshot snapshot)
    {
        var checks = new List<FrameworkCheckItemDto>();
        var moduleCount = snapshot.Modules.Count;
        if (framework.MinModules.HasValue)
        {
            checks.Add(new FrameworkCheckItemDto
            {
                Code = "MinModules",
                Label = "Minimum modules",
                Expected = framework.MinModules.Value.ToString(),
                Actual = moduleCount.ToString(),
                Passed = moduleCount >= framework.MinModules.Value,
                AffectedCurriculumLinks = snapshot.Modules
                    .Select(m => new AffectedCurriculumLinkDto
                    {
                        TargetType = ProgramAdvisoryTargetType.Module,
                        Id = m.Id,
                        Label = m.Name,
                    })
                    .ToList(),
            });
        }

        var offline = snapshot.ActivitiesById.Values
            .Where(a => a.ActivityType == ActivityType.Offline)
            .ToList();
        if (framework.MinOfflineSessions.HasValue)
        {
            checks.Add(new FrameworkCheckItemDto
            {
                Code = "MinOfflineSessions",
                Label = "Minimum Offline sessions",
                Expected = framework.MinOfflineSessions.Value.ToString(),
                Actual = offline.Count.ToString(),
                Passed = offline.Count >= framework.MinOfflineSessions.Value,
                AffectedCurriculumLinks = offline
                    .Select(a => new AffectedCurriculumLinkDto
                    {
                        TargetType = ProgramAdvisoryTargetType.Activity,
                        Id = a.Id,
                        Label = a.Name,
                    })
                    .ToList(),
            });
        }

        var live = snapshot.ActivitiesById.Values
            .Where(a => a.ActivityType == ActivityType.LiveOnline)
            .ToList();
        if (framework.MinLiveSessions.HasValue)
        {
            checks.Add(new FrameworkCheckItemDto
            {
                Code = "MinLiveSessions",
                Label = "Minimum LiveOnline sessions",
                Expected = framework.MinLiveSessions.Value.ToString(),
                Actual = live.Count.ToString(),
                Passed = live.Count >= framework.MinLiveSessions.Value,
                AffectedCurriculumLinks = live
                    .Select(a => new AffectedCurriculumLinkDto
                    {
                        TargetType = ProgramAdvisoryTargetType.Activity,
                        Id = a.Id,
                        Label = a.Name,
                    })
                    .ToList(),
            });
        }

        if (framework.RequireCapstoneResearchMilestone == true)
        {
            var capstones = snapshot.MilestonesByModuleId.Values
                .SelectMany(m => m)
                .Where(m => m.IsCapstone && !m.IsDeleted)
                .ToList();
            checks.Add(new FrameworkCheckItemDto
            {
                Code = "RequireCapstoneResearchMilestone",
                Label = "Capstone research milestone",
                Expected = "At least 1",
                Actual = capstones.Count.ToString(),
                Passed = capstones.Count >= 1,
                AffectedCurriculumLinks = capstones
                    .Select(m => new AffectedCurriculumLinkDto
                    {
                        TargetType = ProgramAdvisoryTargetType.ResearchMilestone,
                        Id = m.Id,
                        Label = m.Title,
                    })
                    .ToList(),
            });
        }

        return checks;
    }

    private async Task<Program> GetActiveProgramAsync(Guid programId)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        return program;
    }

    private async Task<ProgramFramework> RequireActiveFrameworkAsync(Guid frameworkId)
    {
        var framework = await _unitOfWork.ProgramFrameworks.GetByIdAsync(frameworkId);
        if (framework == null || framework.IsDeleted)
        {
            throw ErrorHelper.BadRequest(
                "Assigned program framework is no longer available. Clear it or assign another before continuing.");
        }

        return framework;
    }

    private async Task EnsureExpertCanViewReviewAsync(User actor, Program program)
    {
        var expert = await RequireCurrentExpertAsync(actor);
        if (program.AdvisorExpertId == expert.Id || await IsBoardMemberAsync(program.Id, expert.Id))
        {
            return;
        }

        if (program.FrameworkId.HasValue)
        {
            var framework = await RequireActiveFrameworkAsync(program.FrameworkId.Value);
            if (framework.ExpertId == expert.Id)
            {
                return;
            }
        }

        throw ErrorHelper.Forbidden(
            "You can only view curriculum reviews for programs on your board or frameworks you own.");
    }

    private async Task<bool> IsBoardMemberAsync(Guid programId, Guid expertId)
    {
        var board = await _unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == programId && b.ExpertId == expertId && !b.IsDeleted);
        return board != null;
    }

    private async Task<List<Expert>> ResolveSubmitReviewersAsync(Program program)
    {
        if (!program.AdvisorExpertId.HasValue)
        {
            throw ErrorHelper.BadRequest("Assign a responsible expert before submitting for review.");
        }

        var advisor = await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value);
        if (advisor == null || advisor.IsDeleted || !advisor.UserId.HasValue || advisor.UserId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("The responsible expert must have an active linked login before submission.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(advisor.UserId.Value);
        if (user == null || user.IsDeleted || user.Role != RoleType.Expert || user.Status != AccountStatus.Active)
        {
            throw ErrorHelper.BadRequest("The responsible expert must have an active linked login before submission.");
        }

        return [advisor];
    }

    private async Task<User> RequireManagerOrAdminAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user.Role is not (RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("Only Manager or Admin can submit, withdraw, or publish programs.");
        }

        return user;
    }

    private async Task<User> ResolveReviewActorAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user.Role is not (RoleType.Expert or RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("Only Expert, Manager, or Admin can access curriculum reviews.");
        }

        return user;
    }

    private async Task<User> GetCurrentUserAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        return user;
    }

    private async Task<Expert> RequireCurrentExpertAsync(User actor)
    {
        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(
            e => e.UserId == actor.Id && !e.IsDeleted);
        if (expert == null)
        {
            throw ErrorHelper.Forbidden("Current user is not linked to an expert profile.");
        }

        return expert;
    }

    private async Task PublishCurriculumReviewSubmittedAsync(
        Program program,
        ProgramFramework? framework,
        IReadOnlyList<Expert> reviewers,
        User actor)
    {
        var actorName = DisplayName(actor);
        var commands = reviewers
            .Where(e => e.UserId.HasValue && e.UserId.Value != Guid.Empty)
            .Select(e => NotificationCatalog.CurriculumReviewSubmitted(
                e.UserId!.Value,
                program.Id,
                actor.Id,
                program.Name,
                framework?.Name,
                actorName))
            .ToList();

        if (commands.Count == 0)
        {
            return;
        }

        await _notificationPublisher.PublishManyAsync(commands);
    }

    private async Task PublishCurriculumReviewDecisionAsync(
        Program program,
        CurriculumReview review,
        User actor)
    {
        var actorName = DisplayName(actor);
        var command = review.Decision == CurriculumReviewDecision.Approved
            ? NotificationCatalog.CurriculumReviewApproved(
                program.Id,
                review.Id,
                actor.Id,
                program.Name,
                actorName)
            : NotificationCatalog.CurriculumReviewChangesRequested(
                program.Id,
                review.Comment ?? string.Empty,
                review.Id,
                actor.Id,
                program.Name,
                actorName);

        await _notificationPublisher.PublishAsync(command);
    }

    private static string DisplayName(User user)
        => string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;

    private async Task<CurriculumReviewResponseDto> MapReviewAsync(CurriculumReview review)
    {
        var expert = await _unitOfWork.Experts.GetByIdAsync(review.ExpertId);
        var scores = await _unitOfWork.ReviewCriterionScores.GetAllAsync(
            s => s.CurriculumReviewId == review.Id && !s.IsDeleted);
        var criterionIds = scores.Select(s => s.FrameworkRubricCriterionId).Distinct().ToList();
        var criteria = criterionIds.Count == 0
            ? []
            : await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
                c => criterionIds.Contains(c.Id));
        return MapReview(
            review,
            expert == null || expert.IsDeleted ? null : expert,
            scores,
            criteria.ToDictionary(c => c.Id));
    }

    private static CurriculumReviewResponseDto MapReview(
        CurriculumReview review,
        Expert? expert,
        IReadOnlyList<ReviewCriterionScore> scores,
        IReadOnlyDictionary<Guid, FrameworkRubricCriterion> criteriaById)
        => new()
        {
            Id = review.Id,
            ProgramId = review.ProgramId,
            ExpertId = review.ExpertId,
            ExpertName = expert?.FullName,
            Round = review.Round,
            SubmissionId = review.SubmissionId,
            SnapshotAvailable = review.SnapshotAvailable,
            Decision = review.Decision,
            Comment = review.Comment,
            ReviewedAt = review.ReviewedAt,
            Scores = scores
                .Select(s =>
                {
                    criteriaById.TryGetValue(s.FrameworkRubricCriterionId, out var criterion);
                    return new ReviewCriterionScoreResponseDto
                    {
                        Id = s.Id,
                        CriterionId = s.FrameworkRubricCriterionId,
                        CriterionName = !string.IsNullOrWhiteSpace(s.CriterionNameSnapshot)
                            ? s.CriterionNameSnapshot
                            : criterion?.Name,
                        Score = s.Score,
                        MaxScore = s.MaxScoreSnapshot > 0
                            ? s.MaxScoreSnapshot
                            : criterion?.MaxScore ?? 0,
                        Comment = s.Comment,
                    };
                })
                .ToList(),
        };

    private static ProgramReviewSubmissionSummaryDto MapSubmissionSummary(ProgramReviewSubmission s)
        => new()
        {
            Id = s.Id,
            SubmissionNumber = s.SubmissionNumber,
            Status = s.Status,
            AssignedAdvisorExpertId = s.AssignedAdvisorExpertId,
            FrameworkVersionId = s.FrameworkVersionId,
            SubmittedAt = s.SubmittedAt,
            ClosedAt = s.ClosedAt,
            ConcurrencyVersion = s.ConcurrencyVersion,
        };

    private static ProgramReviewSubmissionDetailDto MapSubmissionDetail(ProgramReviewSubmission s)
        => new()
        {
            Id = s.Id,
            ProgramId = s.ProgramId,
            SubmissionNumber = s.SubmissionNumber,
            Status = s.Status,
            SubmittedByManagerId = s.SubmittedByManagerId,
            AssignedAdvisorExpertId = s.AssignedAdvisorExpertId,
            FrameworkVersionId = s.FrameworkVersionId,
            CurriculumSnapshotJson = s.CurriculumSnapshotJson,
            RubricSnapshotJson = s.RubricSnapshotJson,
            SubmittedAt = s.SubmittedAt,
            ClosedAt = s.ClosedAt,
            ConcurrencyVersion = s.ConcurrencyVersion,
        };

    private static ProgramReviewDraftDto MapDraft(ProgramReviewDraft draft)
    {
        var scores = string.IsNullOrWhiteSpace(draft.ScoresJson)
            ? []
            : JsonSerializer.Deserialize<List<ReviewCriterionScoreRequest>>(draft.ScoresJson, DraftJsonOptions)
              ?? [];

        return new ProgramReviewDraftDto
        {
            Id = draft.Id,
            SubmissionId = draft.SubmissionId,
            Scores = scores,
            OverallComment = draft.OverallComment,
            ConcurrencyVersion = draft.ConcurrencyVersion,
            LastSavedAt = draft.LastSavedAt,
        };
    }
}
