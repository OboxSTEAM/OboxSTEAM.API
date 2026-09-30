using System.Text.Json;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

// Legacy submission-based review flow; it still reads the obsolete PendingReview status.
#pragma warning disable CS0618

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
        => await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => SubmitForReviewCoreAsync(programId));

    private async Task<ProgramsResponseDto> SubmitForReviewCoreAsync(Guid programId)
    {
        var actor = await RequireManagerOrAdminAsync();
        var program = await GetActiveProgramAsync(programId);

        if (program.Status != ProgramStatus.Draft)
        {
            throw ErrorHelper.Conflict("Only Draft programs can be submitted for review.");
        }

        if (!program.AdvisorExpertId.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Assign a responsible expert before submitting for review.",
                "ADVISOR_REQUIRED");
        }

        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == programId && !m.IsDeleted);
        if (modules.Count == 0)
        {
            throw ErrorHelper.BadRequest(
                "Add at least one module before submitting for review.",
                "MODULES_REQUIRED");
        }

        try
        {
            await ProgramFrameworkValidator.ValidateForSubmitAsync(_unitOfWork, programId);
        }
        catch (ConflictException ex)
        {
            throw ErrorHelper.Conflict(ex.Message, ex.ErrorCode ?? "FRAMEWORK_UNAVAILABLE");
        }
        catch (BadRequestException ex)
        {
            throw ErrorHelper.BadRequest(ex.Message, ex.ErrorCode ?? "FRAMEWORK_CHECK_FAILED");
        }

        ProgramFramework? framework = null;
        if (program.FrameworkId.HasValue)
        {
            framework = await RequireActiveFrameworkAsync(program.FrameworkId.Value);
        }

        var reviewers = await ResolveSubmitReviewersAsync(program);
        var advisor = reviewers[0];
        var now = _currentTime.GetCurrentTime().ToUniversalTime();

        var requiredChanges = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == program.Id
                 && t.Type == ProgramAdvisoryThreadType.RequiredChange
                 && !t.IsDeleted);
        var openRequiredCount = requiredChanges.Count(t => t.Status == ProgramAdvisoryThreadStatus.Open);
        if (openRequiredCount > 0)
        {
            throw ErrorHelper.Conflict(
                $"Mark every required change as fixed before submitting ({openRequiredCount} still open).",
                "REQUIRED_CHANGES_NOT_FIXED");
        }

        var outstandingRequirements = requiredChanges
            .Where(t => t.Status != ProgramAdvisoryThreadStatus.Resolved)
            .ToList();
        var reviewRoundIntent = outstandingRequirements.Count > 0
            ? ProgramReviewSubmissionIntent.RevisionVerification
            : ProgramReviewSubmissionIntent.InitialReview;

        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
        var curriculumJson = CurriculumReviewSnapshotBuilder.BuildCurriculumSnapshotJson(tree);

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
            ReviewRoundIntent = reviewRoundIntent,
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
        => await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => WithdrawReviewCoreAsync(programId));

    private async Task<ProgramsResponseDto> WithdrawReviewCoreAsync(Guid programId)
    {
        var actor = await RequireManagerOrAdminAsync();
        var program = await GetActiveProgramAsync(programId);

        if (program.Status is not (ProgramStatus.PendingReview or ProgramStatus.Approved))
        {
            throw ErrorHelper.Conflict("Only programs pending expert review or approved for publish can be withdrawn.");
        }

        var now = _currentTime.GetCurrentTime().ToUniversalTime();
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

        return ordered
            .Select(review => MapReview(review, expertsById.GetValueOrDefault(review.ExpertId)))
            .ToList();
    }

    public async Task<CurriculumReviewResponseDto> ApproveAsync(
        Guid programId,
        ApproveCurriculumReviewRequest? request)
        => await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => ApproveCoreAsync(programId, request));

    private async Task<CurriculumReviewResponseDto> ApproveCoreAsync(
        Guid programId,
        ApproveCurriculumReviewRequest? request)
    {
        var (program, expert, actor) = await RequirePendingDecisionAsync(programId);
        var submission = await ResolvePendingSubmissionAsync(program.Id, request?.SubmissionId);
        EnsureSubmissionConcurrency(submission, request?.ConcurrencyVersion);

        var comment = CurriculumReviewValidator.NormalizeOptionalComment(request?.Comment);
        var reviewId = Guid.NewGuid();
        var now = _currentTime.GetCurrentTime().ToUniversalTime();

        var unresolvedRequired = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == program.Id
                 && t.Type == ProgramAdvisoryThreadType.RequiredChange
                 && t.Status != ProgramAdvisoryThreadStatus.Resolved
                 && !t.IsDeleted);
        if (unresolvedRequired.Count > 0)
        {
            throw ErrorHelper.Conflict(
                "Unresolved required changes must be accepted before approval.",
                "APPROVAL_BLOCKED");
        }

        var review = await PersistDecisionAsync(
            program,
            expert,
            CurriculumReviewDecision.Approved,
            comment,
            reviewId,
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
        await AcceptOutstandingRequirementsAsync(program, submission, actor, now);
        await AddNotificationIntentAsync(
            program.Id,
            review.Id,
            "CurriculumReviewApproved",
            NotificationType.CurriculumReviewApproved,
            new { programId = program.Id, reviewId = review.Id, submissionId = submission.Id },
            actor.Id,
            now);
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
        => await _unitOfWork.ExecuteAdvisoryTransactionAsync(
            programId,
            () => RequestChangesCoreAsync(programId, request));

    private async Task<CurriculumReviewResponseDto> RequestChangesCoreAsync(
        Guid programId,
        RequestCurriculumChangesRequest request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Request body is required.");
        }

        var operationId = NormalizeClientOperationId(request.ClientOperationId);
        if (operationId != null)
        {
            var existing = await _unitOfWork.CurriculumReviews.FirstOrDefaultAsync(
                r => r.ProgramId == programId
                     && r.ClientOperationId == operationId
                     && !r.IsDeleted);
            if (existing != null)
            {
                return await MapReviewAsync(existing);
            }
        }

        var (program, expert, actor) = await RequirePendingDecisionAsync(programId);
        var submission = await ResolvePendingSubmissionAsync(program.Id, request.SubmissionId);
        EnsureSubmissionConcurrency(submission, request.ConcurrencyVersion);

        var outstandingRequirements = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == program.Id
                 && t.Type == ProgramAdvisoryThreadType.RequiredChange
                 && t.Status != ProgramAdvisoryThreadStatus.Resolved
                 && !t.IsDeleted);
        var comment = outstandingRequirements.Count > 0
            ? CurriculumReviewValidator.NormalizeOptionalComment(request.Comment)
            : CurriculumReviewValidator.RequireComment(request.Comment);
        var reviewId = Guid.NewGuid();
        var now = _currentTime.GetCurrentTime().ToUniversalTime();

        var review = await PersistDecisionAsync(
            program,
            expert,
            CurriculumReviewDecision.ChangesRequested,
            comment,
            reviewId,
            submission.Id,
            snapshotAvailable: true,
            clientOperationId: operationId);

        var linkedRequirementIds = outstandingRequirements.Select(t => t.Id).ToList();
        if (outstandingRequirements.Count == 0)
        {
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
                ConcurrencyVersion = Guid.NewGuid(),
                LatestActivitySequence = 1,
                LastMessageAt = now,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };
            var message = new ProgramAdvisoryMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AuthorUserId = actor.Id,
                StreamSequence = 1,
                Message = comment!,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };
            await _unitOfWork.ProgramAdvisoryThreads.AddAsync(thread);
            await _unitOfWork.ProgramAdvisoryMessages.AddAsync(message);
            var createdEvent = new ProgramAdvisoryThreadEvent
            {
                Id = Guid.NewGuid(),
                ProgramId = program.Id,
                ThreadId = thread.Id,
                Sequence = 1,
                EventType = ProgramAdvisoryThreadEventType.Created,
                ActorUserId = actor.Id,
                NewStatus = thread.Status,
                Message = comment,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };
            await _unitOfWork.ProgramAdvisoryThreadEvents.AddAsync(createdEvent);
            await AddNotificationIntentAsync(
                program.Id,
                createdEvent.Id,
                "AdvisoryRequirementCreated",
                NotificationType.AdvisoryFeedbackPublished,
                new { programId = program.Id, threadId = thread.Id, reviewId = review.Id },
                actor.Id,
                now);
            linkedRequirementIds.Add(thread.Id);
        }

        foreach (var requirementId in linkedRequirementIds)
        {
            await _unitOfWork.CurriculumReviewRequirements.AddAsync(new CurriculumReviewRequirement
            {
                Id = Guid.NewGuid(),
                ProgramId = program.Id,
                CurriculumReviewId = review.Id,
                ThreadId = requirementId,
                CreatedAt = now,
                CreatedBy = actor.Id,
            });
        }

        foreach (var thread in outstandingRequirements.Where(t => t.Status == ProgramAdvisoryThreadStatus.Addressed))
        {
            await ReopenAddressedRequirementAsync(program, thread, submission, actor, now);
        }

        submission.Status = ProgramReviewSubmissionStatus.ChangesRequested;
        submission.ClosedAt = now;
        submission.ConcurrencyVersion = Guid.NewGuid();
        submission.UpdatedAt = now;
        submission.UpdatedBy = actor.Id;
        await _unitOfWork.ProgramReviewSubmissions.Update(submission);

        program.Status = ProgramStatus.Draft;
        await _unitOfWork.Programs.Update(program);
        await AddNotificationIntentAsync(
            program.Id,
            review.Id,
            "CurriculumReviewChangesRequested",
            NotificationType.CurriculumReviewChangesRequested,
            new { programId = program.Id, reviewId = review.Id, submissionId = submission.Id },
            actor.Id,
            now);
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
        return await ProgramFrameworkCheck.RunAsync(_unitOfWork, program);
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

        var comment = CurriculumReviewValidator.NormalizeOptionalComment(request.OverallComment);

        if (draft == null)
        {
            draft = new ProgramReviewDraft
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                AdvisorExpertId = expert.Id,
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
            throw ErrorHelper.Conflict(
                "Submission was updated elsewhere. Reload and try again.",
                "SUBMISSION_CONCURRENCY_STALE");
        }
    }

    private static string? NormalizeClientOperationId(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        var normalized = operationId.Trim();
        if (normalized.Length > 100)
        {
            throw ErrorHelper.BadRequest("ClientOperationId must be at most 100 characters.");
        }

        return normalized;
    }

    private async Task AcceptOutstandingRequirementsAsync(
        Program program,
        ProgramReviewSubmission submission,
        User actor,
        DateTime now)
    {
        var outstanding = await _unitOfWork.ProgramAdvisoryThreads.GetAllAsync(
            t => t.ProgramId == program.Id
                 && t.Type == ProgramAdvisoryThreadType.RequiredChange
                 && t.Status != ProgramAdvisoryThreadStatus.Resolved
                 && !t.IsDeleted);
        foreach (var thread in outstanding)
        {
            var prior = thread.Status;
            var sequence = await AllocateThreadSequenceAsync(thread);
            thread.Status = ProgramAdvisoryThreadStatus.Resolved;
            thread.ConcurrencyVersion = Guid.NewGuid();
            thread.UpdatedAt = now;
            thread.UpdatedBy = actor.Id;
            await _unitOfWork.ProgramAdvisoryThreads.Update(thread);
            await _unitOfWork.ProgramAdvisoryThreadEvents.AddAsync(new ProgramAdvisoryThreadEvent
            {
                Id = Guid.NewGuid(),
                ProgramId = program.Id,
                ThreadId = thread.Id,
                Sequence = sequence,
                EventType = ProgramAdvisoryThreadEventType.VerificationRecorded,
                ActorUserId = actor.Id,
                PriorStatus = prior,
                NewStatus = ProgramAdvisoryThreadStatus.Resolved,
                Message = "Accepted on approval",
                ResolutionKind = AdvisoryResolutionKind.Verified,
                VerifiedAgainstSubmissionId = submission.Id,
                CorrectionReferenceIdsJson = "[]",
                CreatedAt = now,
                CreatedBy = actor.Id,
            });
        }
    }

    private async Task ReopenAddressedRequirementAsync(
        Program program,
        ProgramAdvisoryThread thread,
        ProgramReviewSubmission submission,
        User actor,
        DateTime now)
    {
        var sequence = await AllocateThreadSequenceAsync(thread);
        thread.Status = ProgramAdvisoryThreadStatus.Open;
        thread.ConcurrencyVersion = Guid.NewGuid();
        thread.UpdatedAt = now;
        thread.UpdatedBy = actor.Id;
        await _unitOfWork.ProgramAdvisoryThreads.Update(thread);
        await _unitOfWork.ProgramAdvisoryThreadEvents.AddAsync(new ProgramAdvisoryThreadEvent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            ThreadId = thread.Id,
            Sequence = sequence,
            EventType = ProgramAdvisoryThreadEventType.StatusChanged,
            ActorUserId = actor.Id,
            PriorStatus = ProgramAdvisoryThreadStatus.Addressed,
            NewStatus = ProgramAdvisoryThreadStatus.Open,
            Message = $"Chưa đạt ở lần {submission.SubmissionNumber}",
            CorrectionReferenceIdsJson = "[]",
            CreatedAt = now,
            CreatedBy = actor.Id,
        });
    }

    private async Task<long> AllocateThreadSequenceAsync(ProgramAdvisoryThread thread)
    {
        var messageMax = (await _unitOfWork.ProgramAdvisoryMessages.GetAllAsync(
                m => m.ThreadId == thread.Id && !m.IsDeleted))
            .Select(m => m.StreamSequence)
            .DefaultIfEmpty(0)
            .Max();
        var eventMax = (await _unitOfWork.ProgramAdvisoryThreadEvents.GetAllAsync(
                e => e.ThreadId == thread.Id && !e.IsDeleted))
            .Select(e => e.Sequence)
            .DefaultIfEmpty(0)
            .Max();
        var next = Math.Max(thread.LatestActivitySequence, Math.Max(messageMax, eventMax)) + 1;
        thread.LatestActivitySequence = next;
        return next;
    }

    private async Task<CurriculumReview> PersistDecisionAsync(
        Program program,
        Expert expert,
        CurriculumReviewDecision decision,
        string? comment,
        Guid reviewId,
        Guid submissionId,
        bool snapshotAvailable,
        string? clientOperationId = null)
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
            ClientOperationId = clientOperationId,
        };

        await _unitOfWork.CurriculumReviews.AddAsync(review);
        return review;
    }

    private async Task<(Program Program, Expert Expert, User Actor)> RequirePendingDecisionAsync(Guid programId)
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

        return (program, expert, actor);
    }

    /// <summary>
    /// Legacy frozen-snapshot checks for the advisory board (original four rules only).
    /// Live checks use <see cref="FrameworkRuleEvaluator"/>.
    /// </summary>
    public static List<FrameworkCheckItemDto> BuildStructuredChecks(
        ProgramFrameworkVersion framework,
        CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument snapshot)
    {
        var checks = new List<FrameworkCheckItemDto>();
        if (framework.MinModules.HasValue)
        {
            checks.Add(new FrameworkCheckItemDto
            {
                Code = "MinModules",
                Label = "Minimum modules",
                Expected = framework.MinModules.Value.ToString(),
                Actual = snapshot.Modules.Count.ToString(),
                Passed = snapshot.Modules.Count >= framework.MinModules.Value,
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

        var activities = snapshot.Modules
            .SelectMany(m => m.Courses.SelectMany(c => c.Activities)
                .Concat(m.Milestones.SelectMany(ms => ms.Activities))
                .Concat(m.Activities))
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .ToList();

        AddActivityCheck(
            checks,
            framework.MinOfflineSessions,
            "MinOfflineSessions",
            "Minimum Offline sessions",
            ActivityType.Offline,
            activities);
        AddActivityCheck(
            checks,
            framework.MinLiveSessions,
            "MinLiveSessions",
            "Minimum LiveOnline sessions",
            ActivityType.LiveOnline,
            activities);

        if (framework.RequireCapstoneResearchMilestone == true)
        {
            var capstones = snapshot.Modules
                .SelectMany(m => m.Milestones)
                .Where(m => m.IsCapstone)
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

    private static void AddActivityCheck(
        ICollection<FrameworkCheckItemDto> checks,
        int? minimum,
        string code,
        string label,
        ActivityType type,
        IReadOnlyList<CurriculumReviewSnapshotBuilder.ActivitySnapshot> activities)
    {
        if (!minimum.HasValue)
        {
            return;
        }

        var typeName = type.ToString();
        var matching = activities
            .Where(a =>
                string.Equals(a.ActivityType, typeName, StringComparison.Ordinal)
                || string.Equals(a.Type, typeName, StringComparison.Ordinal))
            .ToList();
        checks.Add(new FrameworkCheckItemDto
        {
            Code = code,
            Label = label,
            Expected = minimum.Value.ToString(),
            Actual = matching.Count.ToString(),
            Passed = matching.Count >= minimum.Value,
            AffectedCurriculumLinks = matching
                .Select(a => new AffectedCurriculumLinkDto
                {
                    TargetType = ProgramAdvisoryTargetType.Activity,
                    Id = a.Id,
                    Label = a.Name,
                })
                .ToList(),
        });
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
            throw ErrorHelper.BadRequest(
                "Assign a responsible expert before submitting for review.",
                "ADVISOR_REQUIRED");
        }

        var advisor = await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value);
        if (advisor == null || advisor.IsDeleted || !advisor.UserId.HasValue || advisor.UserId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest(
                "The responsible expert must have an active linked login before submission.",
                "ADVISOR_LOGIN_REQUIRED");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(advisor.UserId.Value);
        if (user == null || user.IsDeleted || user.Role != RoleType.Expert || user.Status != AccountStatus.Active)
        {
            throw ErrorHelper.BadRequest(
                "The responsible expert must have an active linked login before submission.",
                "ADVISOR_LOGIN_REQUIRED");
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

    private async Task AddNotificationIntentAsync(
        Guid programId,
        Guid eventId,
        string eventType,
        NotificationType notificationType,
        object payload,
        Guid actorUserId,
        DateTime now)
    {
        await _unitOfWork.ProgramAdvisoryNotificationIntents.AddAsync(
            new ProgramAdvisoryNotificationIntent
            {
                Id = Guid.NewGuid(),
                ProgramId = programId,
                EventId = eventId,
                EventType = eventType,
                NotificationType = notificationType,
                PayloadJson = JsonSerializer.Serialize(payload),
                Status = AdvisoryNotificationIntentStatus.Pending,
                AttemptCount = 0,
                NextAttemptAt = now,
                CreatedAt = now,
                CreatedBy = actorUserId,
            });
    }

    private async Task<CurriculumReviewResponseDto> MapReviewAsync(CurriculumReview review)
    {
        var expert = await _unitOfWork.Experts.GetByIdAsync(review.ExpertId);
        return MapReview(review, expert == null || expert.IsDeleted ? null : expert);
    }

    private static CurriculumReviewResponseDto MapReview(CurriculumReview review, Expert? expert)
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
        };

    private static ProgramReviewSubmissionSummaryDto MapSubmissionSummary(ProgramReviewSubmission s)
        => new()
        {
            Id = s.Id,
            SubmissionNumber = s.SubmissionNumber,
            Status = s.Status,
            ReviewRoundIntent = s.ReviewRoundIntent,
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
            ReviewRoundIntent = s.ReviewRoundIntent,
            SubmittedByManagerId = s.SubmittedByManagerId,
            AssignedAdvisorExpertId = s.AssignedAdvisorExpertId,
            FrameworkVersionId = s.FrameworkVersionId,
            CurriculumSnapshotJson = s.CurriculumSnapshotJson,
            SubmittedAt = s.SubmittedAt,
            ClosedAt = s.ClosedAt,
            ConcurrencyVersion = s.ConcurrencyVersion,
        };

    private static ProgramReviewDraftDto MapDraft(ProgramReviewDraft draft) => new()
    {
        Id = draft.Id,
        SubmissionId = draft.SubmissionId,
        OverallComment = draft.OverallComment,
        ConcurrencyVersion = draft.ConcurrencyVersion,
        LastSavedAt = draft.LastSavedAt,
    };
}
