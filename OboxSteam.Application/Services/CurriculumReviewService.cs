using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
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

        await ProgramFrameworkValidator.ValidateForSubmitAsync(_unitOfWork, programId);

        ProgramFramework? framework = null;
        if (program.FrameworkId.HasValue)
        {
            framework = await RequireActiveFrameworkAsync(program.FrameworkId.Value);
            program.Status = ProgramStatus.PendingReview;
            _logger.LogInformation(
                "[SubmitForReview] Program {ProgramId} submitted by {UserId} to PendingReview on framework {FrameworkId}.",
                program.Id,
                actor.Id,
                framework.Id);
        }
        else
        {
            program.Status = ProgramStatus.Approved;
            _logger.LogInformation(
                "[SubmitForReview] Program {ProgramId} submitted by {UserId} to Approved (no framework).",
                program.Id,
                actor.Id);
        }

        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        if (framework != null)
        {
            await PublishCurriculumReviewSubmittedAsync(program, framework, actor);
        }

        return await _programService.GetProgramByIdAsync(programId);
    }

    public async Task<ProgramsResponseDto> WithdrawReviewAsync(Guid programId)
    {
        var actor = await RequireManagerOrAdminAsync();
        var program = await GetActiveProgramAsync(programId);

        if (program.Status != ProgramStatus.PendingReview)
        {
            throw ErrorHelper.Conflict("Only programs pending expert review can be withdrawn.");
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

        return await _programService.GetProgramByIdAsync(programId);
    }

    public async Task<Pagination<ProgramReviewQueueItemDto>> GetReviewQueueAsync(int page, int pageSize)
    {
        var actor = await ResolveReviewActorAsync();

        var pending = await _unitOfWork.Programs.GetAllAsync(
            p => p.Status == ProgramStatus.PendingReview && !p.IsDeleted && p.FrameworkId.HasValue);

        if (actor.Role == RoleType.Expert)
        {
            var expert = await RequireCurrentExpertAsync(actor);
            var frameworks = await _unitOfWork.ProgramFrameworks.GetAllAsync(
                f => f.ExpertId == expert.Id && !f.IsDeleted);
            var ownedIds = frameworks.Select(f => f.Id).ToHashSet();
            pending = pending
                .Where(p => p.FrameworkId.HasValue && ownedIds.Contains(p.FrameworkId.Value))
                .ToList();
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
                frameworksById.TryGetValue(p.FrameworkId!.Value, out var framework);
                return new ProgramReviewQueueItemDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Status = p.Status,
                    FrameworkId = p.FrameworkId!.Value,
                    FrameworkName = framework?.Name,
                    ExpertId = framework?.ExpertId ?? Guid.Empty,
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
            await EnsureExpertOwnsAttachedFrameworkAsync(actor, program);
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
        var (program, expert, _, criteria, actor) = await RequirePendingOwnedReviewAsync(programId);
        var comment = CurriculumReviewValidator.NormalizeOptionalComment(request?.Comment);
        var reviewId = Guid.NewGuid();
        var scoreRows = CurriculumReviewValidator.BuildScores(reviewId, criteria, request?.Scores);

        var review = await PersistDecisionAsync(
            program,
            expert,
            CurriculumReviewDecision.Approved,
            comment,
            reviewId,
            scoreRows);

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

        var (program, expert, _, _, actor) = await RequirePendingOwnedReviewAsync(programId);
        var comment = CurriculumReviewValidator.RequireComment(request.Comment);

        var review = await PersistDecisionAsync(
            program,
            expert,
            CurriculumReviewDecision.ChangesRequested,
            comment,
            Guid.NewGuid(),
            []);

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

    private async Task<CurriculumReview> PersistDecisionAsync(
        Program program,
        Expert expert,
        CurriculumReviewDecision decision,
        string? comment,
        Guid reviewId,
        IReadOnlyList<ReviewCriterionScore> scores)
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
        ProgramFramework Framework,
        List<FrameworkRubricCriterion> Criteria,
        User Actor)> RequirePendingOwnedReviewAsync(Guid programId)
    {
        var actor = await ResolveReviewActorAsync();
        if (actor.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden("Only the owning expert can decide a curriculum review.");
        }

        var program = await GetActiveProgramAsync(programId);
        if (program.Status != ProgramStatus.PendingReview)
        {
            throw ErrorHelper.Conflict("Only programs pending expert review can receive a decision.");
        }

        var expert = await EnsureExpertOwnsAttachedFrameworkAsync(actor, program);
        var framework = await RequireActiveFrameworkAsync(program.FrameworkId!.Value);
        var criteria = await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
            c => c.FrameworkId == framework.Id && !c.IsDeleted);

        return (
            program,
            expert,
            framework,
            criteria.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList(),
            actor);
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

    private async Task<Expert> EnsureExpertOwnsAttachedFrameworkAsync(User actor, Program program)
    {
        var expert = await RequireCurrentExpertAsync(actor);
        if (!program.FrameworkId.HasValue)
        {
            throw ErrorHelper.Forbidden("This program is not attached to a framework.");
        }

        var framework = await RequireActiveFrameworkAsync(program.FrameworkId.Value);
        if (framework.ExpertId != expert.Id)
        {
            throw ErrorHelper.Forbidden("You can only review programs attached to your own frameworks.");
        }

        return expert;
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
        ProgramFramework framework,
        User actor)
    {
        var expert = await _unitOfWork.Experts.GetByIdAsync(framework.ExpertId);
        if (expert == null || expert.IsDeleted || !expert.UserId.HasValue || expert.UserId == Guid.Empty)
        {
            _logger.LogWarning(
                "[SubmitForReview] Skip CurriculumReviewSubmitted; framework {FrameworkId} owner has no login.",
                framework.Id);
            return;
        }

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.CurriculumReviewSubmitted(
                expert.UserId.Value,
                program.Id,
                actor.Id,
                program.Name,
                framework.Name,
                DisplayName(actor)));
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
                        CriterionName = criterion?.Name,
                        Score = s.Score,
                        MaxScore = criterion?.MaxScore ?? 0,
                        Comment = s.Comment,
                    };
                })
                .ToList(),
        };
}
