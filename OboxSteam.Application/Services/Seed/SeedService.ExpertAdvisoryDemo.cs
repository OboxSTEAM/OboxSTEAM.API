using System.Text.Json;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// FE-test fixtures for Milestone B advisory / review-submission flows.
/// Idempotent on <c>PRG-ADV-DRAFT-ADVICE</c>; notifications skip when any ADV program
/// already has a Program-scoped inbox row.
/// </summary>
public partial class SeedService
{
    internal const string SeedMakerAdvisoryFrameworkName = "Maker Advisory Family";
    internal const string SeedExpertAdvisorySeriesName = "Expert Advisory Demo";
    internal const string SeedAdvDraftAdviceCode = "PRG-ADV-DRAFT-ADVICE";
    internal const string SeedAdvDraftFixCode = "PRG-ADV-DRAFT-FIX";
    internal const string SeedAdvPendingCode = "PRG-ADV-PENDING";
    internal const string SeedAdvResubmitCode = "PRG-ADV-RESUBMIT";
    internal const string SeedAdvApprovedCode = "PRG-ADV-APPROVED";
    internal const string SeedAdvActiveCode = "PRG-ADV-ACTIVE";
    internal const string SeedAdvPinV1Code = "PRG-ADV-PIN-V1";
    internal const string SeedAdvShareACode = "PRG-ADV-SHARE-A";
    internal const string SeedAdvShareBCode = "PRG-ADV-SHARE-B";

    private static readonly decimal SeedAdvCatalogPrice = 1_200_000m;

    private static readonly JsonSerializerOptions SeedAdvJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private async Task SeedExpertAdvisoryDemoAsync()
    {
        _loggerService.LogInformation("Starting seed expert advisory demo (Milestone B)");

        var existingAnchor = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == SeedAdvDraftAdviceCode && !p.IsDeleted);
        if (existingAnchor != null)
        {
            _loggerService.LogInformation(
                "Expert advisory demo already present ({Code}). Skipping domain seeding.",
                SeedAdvDraftAdviceCode);
            return;
        }

        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        var expert002 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-002" && !e.IsDeleted);
        var manager = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNG-001" && !u.IsDeleted);

        if (expert001 == null || expert002 == null || manager == null)
        {
            _loggerService.LogWarning(
                "EXP-001 / EXP-002 / MNG-001 missing. Skipping expert advisory demo seed.");
            return;
        }

        var (framework, publishedV1, _, publishedCriteria) =
            await EnsureMakerAdvisoryFrameworkAsync(expert001.Id);

        // A — Draft advice with open suggestions
        var progA = await CreateAdvProgramAsync(
            SeedAdvDraftAdviceCode,
            "ADV Draft Advice",
            "Scenario A: open Suggestion threads; EXP-002 on board.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        var currA = await EnsureAdvCurriculumAsync(progA, "DRAFT-ADVICE", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progA.Id, "Advisor");
        await EnsureExpertOnProgramBoardAsync(expert002, progA.Id, "Board Contributor");
        await SeedAdvSuggestionThreadAsync(
            progA,
            expert002.UserId ?? Guid.Empty,
            ProgramAdvisoryTargetType.Program,
            progA.Id,
            progA.Name,
            "Overall curriculum framing",
            "Consider strengthening the safety briefing before the offline lab.");
        await SeedAdvSuggestionThreadAsync(
            progA,
            expert002.UserId ?? Guid.Empty,
            ProgramAdvisoryTargetType.Module,
            currA.TheoryModule.Id,
            currA.TheoryModule.Name,
            "Theory module outcomes",
            "Optional: add a short checkpoint quiz after the SelfPaced reading.");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvDraftAdviceCode);

        // B — Draft with addressed RequiredChange
        var progB = await CreateAdvProgramAsync(
            SeedAdvDraftFixCode,
            "ADV Draft Fix",
            "Scenario B: RequiredChange Addressed; manager replied.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        var currB = await EnsureAdvCurriculumAsync(progB, "DRAFT-FIX", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progB.Id, "Advisor");
        var threadB = await SeedAdvRequiredChangeAddressedAsync(
            progB,
            currB.OfflineActivity,
            expert001.UserId ?? Guid.Empty,
            manager.Id);
        _ = threadB;
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvDraftFixCode);

        // C — PendingReview + draft scores
        var progC = await CreateAdvProgramAsync(
            SeedAdvPendingCode,
            "ADV Pending Review",
            "Scenario C: submission #1 Pending with partial review draft.",
            ProgramStatus.PendingReview,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progC, "PENDING", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progC.Id, "Advisor");
        var subC = await SeedAdvSubmissionAsync(
            progC,
            manager.Id,
            expert001.Id,
            publishedV1.Id,
            publishedCriteria,
            submissionNumber: 1,
            ProgramReviewSubmissionStatus.Pending,
            closedAt: null);
        await SeedAdvReviewDraftAsync(subC, expert001.Id, publishedCriteria);
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvPendingCode);

        // D — Resubmit with diff (Added activity)
        var progD = await CreateAdvProgramAsync(
            SeedAdvResubmitCode,
            "ADV Resubmit",
            "Scenario D: submission #1 ChangesRequested closed; #2 Pending with added activity.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        var currD = await EnsureAdvCurriculumAsync(
            progD,
            "RESUBMIT",
            includeAssignment: false);
        await EnsureExpertOnProgramBoardAsync(expert001, progD.Id, "Advisor");

        var treeD1 = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, progD.Id);
        var snapD1 = CurriculumReviewSnapshotBuilder.BuildCurriculumSnapshotJson(treeD1);
        var rubricD = CurriculumReviewSnapshotBuilder.BuildRubricSnapshotJson(publishedCriteria);
        var subD1 = new ProgramReviewSubmission
        {
            Id = Guid.NewGuid(),
            ProgramId = progD.Id,
            SubmissionNumber = 1,
            SubmittedByManagerId = manager.Id,
            AssignedAdvisorExpertId = expert001.Id,
            FrameworkVersionId = publishedV1.Id,
            CurriculumSnapshotJson = snapD1,
            RubricSnapshotJson = rubricD,
            Status = ProgramReviewSubmissionStatus.ChangesRequested,
            SubmittedAt = _seedNow.AddDays(-5),
            ClosedAt = _seedNow.AddDays(-3),
            ConcurrencyVersion = Guid.NewGuid(),
            CreatedAt = _seedNow.AddDays(-5),
            CreatedBy = manager.Id,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramReviewSubmissions.AddAsync(subD1);

        await EnsureAdvActivityAsync(
            currD.TheoryCourse.Id,
            "ACT-ADV-RESUBMIT-TH-SP2",
            "Theory follow-up SelfPaced",
            ActivityType.SelfPaced,
            activityOrder: 2,
            "Second SelfPaced activity added after request-changes (diff Added).",
            durationMinutes: null,
            requireQrCheckin: false);

        var treeD2 = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, progD.Id);
        var snapD2 = CurriculumReviewSnapshotBuilder.BuildCurriculumSnapshotJson(treeD2);
        var subD2 = new ProgramReviewSubmission
        {
            Id = Guid.NewGuid(),
            ProgramId = progD.Id,
            SubmissionNumber = 2,
            SubmittedByManagerId = manager.Id,
            AssignedAdvisorExpertId = expert001.Id,
            FrameworkVersionId = publishedV1.Id,
            CurriculumSnapshotJson = snapD2,
            RubricSnapshotJson = rubricD,
            Status = ProgramReviewSubmissionStatus.Pending,
            SubmittedAt = _seedNow.AddDays(-1),
            ClosedAt = null,
            ConcurrencyVersion = Guid.NewGuid(),
            CreatedAt = _seedNow.AddDays(-1),
            CreatedBy = manager.Id,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramReviewSubmissions.AddAsync(subD2);

        progD.Status = ProgramStatus.PendingReview;
        await _unitOfWork.Programs.Update(progD);

        await SeedAdvSuggestionThreadAsync(
            progD,
            expert001.UserId ?? Guid.Empty,
            ProgramAdvisoryTargetType.Program,
            progD.Id,
            progD.Name,
            "Resubmit overall note",
            "Please confirm the new SelfPaced activity covers the prior required change.");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvResubmitCode);

        // E — Approved with snapshot scores
        var progE = await CreateAdvProgramAsync(
            SeedAdvApprovedCode,
            "ADV Approved",
            "Scenario E: Approved submission + CurriculumReview with scores.",
            ProgramStatus.Approved,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progE, "APPROVED", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progE.Id, "Advisor");
        var subE = await SeedAdvSubmissionAsync(
            progE,
            manager.Id,
            expert001.Id,
            publishedV1.Id,
            publishedCriteria,
            submissionNumber: 1,
            ProgramReviewSubmissionStatus.Approved,
            closedAt: _seedNow.AddDays(-2));
        await SeedAdvCurriculumReviewAsync(
            progE,
            expert001.Id,
            subE.Id,
            snapshotAvailable: true,
            publishedCriteria,
            includeScores: true);
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvApprovedCode);

        // F — Active legacy review (no snapshot)
        var progF = await CreateAdvProgramAsync(
            SeedAdvActiveCode,
            "ADV Active Catalog",
            "Scenario F: Active catalog with legacy CurriculumReview (SnapshotAvailable=false).",
            ProgramStatus.Active,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progF, "ACTIVE", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progF.Id, "Advisor");
        await SeedAdvCurriculumReviewAsync(
            progF,
            expert001.Id,
            submissionId: null,
            snapshotAvailable: false,
            publishedCriteria,
            includeScores: false);
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvActiveCode);

        // G — Pin published v1 while draft v2 exists
        var progG = await CreateAdvProgramAsync(
            SeedAdvPinV1Code,
            "ADV Pin Published V1",
            "Scenario G: pins published framework v1 while draft v2 exists on the family.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progG, "PIN-V1", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progG.Id, "Advisor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvPinV1Code);

        // H — Shared published version; different advisors
        var progHa = await CreateAdvProgramAsync(
            SeedAdvShareACode,
            "ADV Share A",
            "Scenario H: shares FrameworkVersionId with SHARE-B; EXP-001 advisor.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progHa, "SHARE-A", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progHa.Id, "Advisor");
        await EnsureExpertOnProgramBoardAsync(expert002, progHa.Id, "Board Contributor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvShareACode);

        var progHb = await CreateAdvProgramAsync(
            SeedAdvShareBCode,
            "ADV Share B",
            "Scenario H: same FrameworkVersionId as SHARE-A; EXP-002 advisor; EXP-001 board suggester.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert002.Id);
        await EnsureAdvCurriculumAsync(progHb, "SHARE-B", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert002, progHb.Id, "Advisor");
        await EnsureExpertOnProgramBoardAsync(expert001, progHb.Id, "Board Contributor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvShareBCode);

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed expert advisory demo");
    }

    private async Task SeedExpertAdvisoryNotificationsAsync()
    {
        _loggerService.LogInformation("Starting seed expert advisory notifications");

        var advCodes = new[]
        {
            SeedAdvDraftAdviceCode,
            SeedAdvDraftFixCode,
            SeedAdvPendingCode,
            SeedAdvResubmitCode,
            SeedAdvApprovedCode,
            SeedAdvActiveCode,
            SeedAdvPinV1Code,
            SeedAdvShareACode,
            SeedAdvShareBCode,
        };

        var advPrograms = await _unitOfWork.Programs.GetAllAsync(
            p => advCodes.Contains(p.Code) && !p.IsDeleted);
        if (advPrograms.Count == 0)
        {
            _loggerService.LogWarning("No ADV programs found. Skipping advisory notifications.");
            return;
        }

        var advIds = advPrograms.Select(p => p.Id).ToHashSet();
        var existingAdvNotifs = await _unitOfWork.Notifications.GetAllAsync(
            n => n.EntityType == "Program" && n.EntityId != null && advIds.Contains(n.EntityId.Value));
        if (existingAdvNotifs.Count > 0)
        {
            _loggerService.LogInformation(
                "Advisory notifications already present for ADV programs. Skipping.");
            return;
        }

        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        var manager = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNG-001" && !u.IsDeleted);
        if (expert001?.UserId == null || manager == null)
        {
            _loggerService.LogWarning(
                "EXP-001 user or MNG-001 missing. Skipping advisory notifications.");
            return;
        }

        var expertUserId = expert001.UserId.Value;
        var expertUser = await _unitOfWork.Users.GetByIdAsync(expertUserId);
        var expertName = expertUser == null
            ? expert001.FullName
            : (string.IsNullOrWhiteSpace(expertUser.FullName) ? expertUser.Email : expertUser.FullName!);
        var managerName = string.IsNullOrWhiteSpace(manager.FullName) ? manager.Email : manager.FullName!;
        var studentNamePlaceholder = "Seed Student";

        Program? Find(string code) => advPrograms.FirstOrDefault(p => p.Code == code);

        var progA = Find(SeedAdvDraftAdviceCode);
        var progB = Find(SeedAdvDraftFixCode);
        var progC = Find(SeedAdvPendingCode);
        var progD = Find(SeedAdvResubmitCode);
        var progE = Find(SeedAdvApprovedCode);

        var threadA = progA == null
            ? null
            : await _unitOfWork.ProgramAdvisoryThreads.FirstOrDefaultAsync(
                t => t.ProgramId == progA.Id
                     && t.Type == ProgramAdvisoryThreadType.Suggestion
                     && t.TargetType == ProgramAdvisoryTargetType.Program
                     && !t.IsDeleted);
        var threadB = progB == null
            ? null
            : await _unitOfWork.ProgramAdvisoryThreads.FirstOrDefaultAsync(
                t => t.ProgramId == progB.Id
                     && t.Type == ProgramAdvisoryThreadType.RequiredChange
                     && !t.IsDeleted);
        var reviewE = progE == null
            ? null
            : await _unitOfWork.CurriculumReviews.FirstOrDefaultAsync(
                r => r.ProgramId == progE.Id && !r.IsDeleted);
        var reviewD = progD == null
            ? null
            : (await _unitOfWork.CurriculumReviews.GetAllAsync(
                  r => r.ProgramId == progD.Id && !r.IsDeleted))
              .OrderBy(r => r.Round)
              .FirstOrDefault();

        var now = _seedNow;
        var samples = new List<(NotificationCommand Command, Guid RecipientId, RoleType Role, DateTime? ReadAt)>();

        if (progC != null)
        {
            samples.Add((
                NotificationCatalog.CurriculumReviewSubmitted(
                    expertUserId,
                    progC.Id,
                    actorUserId: manager.Id,
                    programName: progC.Name,
                    frameworkName: SeedMakerAdvisoryFrameworkName,
                    actorName: managerName),
                expertUserId,
                RoleType.Expert,
                null));
        }

        if (progD != null)
        {
            samples.Add((
                NotificationCatalog.CurriculumReviewSubmitted(
                    expertUserId,
                    progD.Id,
                    actorUserId: manager.Id,
                    programName: progD.Name,
                    frameworkName: SeedMakerAdvisoryFrameworkName,
                    actorName: managerName),
                expertUserId,
                RoleType.Expert,
                null));
            samples.Add((
                NotificationCatalog.CurriculumReviewChangesRequested(
                    progD.Id,
                    comment: "Please add a follow-up SelfPaced activity on the theory course.",
                    reviewId: reviewD?.Id,
                    actorUserId: expertUserId,
                    programName: progD.Name,
                    actorName: expertName),
                manager.Id,
                RoleType.Manager,
                now.AddDays(-3)));
        }

        if (progB != null && threadB != null)
        {
            samples.Add((
                NotificationCatalog.AdvisoryFeedbackPublished(
                    manager.Id,
                    progB.Id,
                    threadB.Id,
                    actorUserId: expertUserId,
                    programName: progB.Name,
                    actorName: expertName,
                    feedbackType: ProgramAdvisoryThreadType.RequiredChange.ToString()),
                manager.Id,
                RoleType.Manager,
                null));
            samples.Add((
                NotificationCatalog.AdvisoryFeedbackPublished(
                    expertUserId,
                    progB.Id,
                    threadB.Id,
                    actorUserId: expertUserId,
                    programName: progB.Name,
                    actorName: expertName,
                    feedbackType: ProgramAdvisoryThreadType.RequiredChange.ToString()),
                expertUserId,
                RoleType.Expert,
                now.AddHours(-20)));
            samples.Add((
                NotificationCatalog.AdvisoryCorrectionAddressed(
                    expertUserId,
                    progB.Id,
                    threadB.Id,
                    actorUserId: manager.Id,
                    programName: progB.Name,
                    actorName: managerName),
                expertUserId,
                RoleType.Expert,
                null));
        }

        if (progA != null && threadA != null)
        {
            samples.Add((
                NotificationCatalog.AdvisoryReply(
                    expertUserId,
                    progA.Id,
                    threadA.Id,
                    actorUserId: manager.Id,
                    programName: progA.Name,
                    actorName: managerName),
                expertUserId,
                RoleType.Expert,
                null));
        }

        if (progE != null)
        {
            samples.Add((
                NotificationCatalog.CurriculumReviewApproved(
                    progE.Id,
                    reviewId: reviewE?.Id,
                    actorUserId: expertUserId,
                    programName: progE.Name,
                    actorName: expertName),
                manager.Id,
                RoleType.Manager,
                null));
        }

        if (samples.Count == 0)
        {
            _loggerService.LogWarning("No advisory notification samples built. Skipping.");
            return;
        }

        var notifications = new List<Notification>(samples.Count);
        for (var i = 0; i < samples.Count; i++)
        {
            var (command, recipientId, role, readAt) = samples[i];
            notifications.Add(ToSeedNotification(
                command,
                recipientId,
                role,
                studentNamePlaceholder,
                readAt,
                now.AddMinutes(-(samples.Count - i) * 11)));
        }

        await _unitOfWork.Notifications.AddRangeAsync(notifications);
        for (var i = 0; i < notifications.Count; i++)
        {
            notifications[i].CreatedAt = now.AddMinutes(-(notifications.Count - i) * 11);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed expert advisory notifications — {Count} inbox row(s).",
            notifications.Count);
    }

    private async Task<(
        ProgramFramework Framework,
        ProgramFrameworkVersion PublishedV1,
        ProgramFrameworkVersion DraftV2,
        List<FrameworkRubricCriterion> PublishedCriteria)> EnsureMakerAdvisoryFrameworkAsync(Guid expertId)
    {
        var framework = await _unitOfWork.ProgramFrameworks.FirstOrDefaultAsync(
            f => f.ExpertId == expertId && f.Name == SeedMakerAdvisoryFrameworkName && !f.IsDeleted);
        if (framework == null)
        {
            framework = new ProgramFramework
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                Name = SeedMakerAdvisoryFrameworkName,
                Category = ProgramCategory.Technology,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramFrameworks.AddAsync(framework);
        }

        var criteriaDefs = new (string Name, string Description, string EvidenceGuidance, int MaxScore, int DisplayOrder)[]
        {
            (
                "Safety and facilitation",
                "Facilitators keep makerspace sessions safe and well-paced.",
                "Offline lab checklist, facilitator notes, and session photos showing reset workspace.",
                10,
                1),
            (
                "Learning progression",
                "Theory and practice build a clear progression toward the offline lab.",
                "Module outcomes map, SelfPaced checkpoint, and Offline activity brief.",
                10,
                2),
        };

        var published = await _unitOfWork.ProgramFrameworkVersions.FirstOrDefaultAsync(
            v => v.FrameworkId == framework.Id && v.VersionNumber == 1 && !v.IsDeleted);
        if (published == null)
        {
            published = new ProgramFrameworkVersion
            {
                Id = Guid.NewGuid(),
                FrameworkId = framework.Id,
                VersionNumber = 1,
                Description = "Description for maker programs: offline lab plus theory SelfPaced progression.",
                AcademicGuidance =
                    "Prioritize safe facilitation in Offline labs and a clear theory→practice progression.",
                MinOfflineSessions = 1,
                IsPublished = true,
                PublishedAt = _seedNow,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramFrameworkVersions.AddAsync(published);
        }
        else if (string.IsNullOrWhiteSpace(published.AcademicGuidance))
        {
            published.AcademicGuidance =
                "Prioritize safe facilitation in Offline labs and a clear theory→practice progression.";
            published.Description ??=
                "Description for maker programs: offline lab plus theory SelfPaced progression.";
            published.MinOfflineSessions ??= 1;
            if (!published.IsPublished)
            {
                published.IsPublished = true;
                published.PublishedAt = _seedNow;
            }

            await _unitOfWork.ProgramFrameworkVersions.Update(published);
        }

        var publishedCriteria = new List<FrameworkRubricCriterion>();
        foreach (var item in criteriaDefs)
        {
            var existing = await _unitOfWork.FrameworkRubricCriteria.FirstOrDefaultAsync(
                c => c.FrameworkVersionId == published.Id && c.Name == item.Name && !c.IsDeleted);
            if (existing != null)
            {
                if (string.IsNullOrWhiteSpace(existing.EvidenceGuidance))
                {
                    existing.EvidenceGuidance = item.EvidenceGuidance;
                    await _unitOfWork.FrameworkRubricCriteria.Update(existing);
                }

                publishedCriteria.Add(existing);
                continue;
            }

            var criterion = new FrameworkRubricCriterion
            {
                Id = Guid.NewGuid(),
                FrameworkId = framework.Id,
                FrameworkVersionId = published.Id,
                Name = item.Name,
                Description = item.Description,
                EvidenceGuidance = item.EvidenceGuidance,
                MaxScore = item.MaxScore,
                DisplayOrder = item.DisplayOrder,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.FrameworkRubricCriteria.AddAsync(criterion);
            publishedCriteria.Add(criterion);
        }

        var draft = await _unitOfWork.ProgramFrameworkVersions.FirstOrDefaultAsync(
            v => v.FrameworkId == framework.Id && v.VersionNumber == 2 && !v.IsDeleted);
        if (draft == null)
        {
            draft = new ProgramFrameworkVersion
            {
                Id = Guid.NewGuid(),
                FrameworkId = framework.Id,
                VersionNumber = 2,
                Description =
                    "Description for maker programs (draft v2 refinements). Not auto-adopted by pinned programs.",
                AcademicGuidance = "Draft v2 — not auto-adopted",
                MinOfflineSessions = 1,
                IsPublished = false,
                PublishedAt = null,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramFrameworkVersions.AddAsync(draft);

            foreach (var source in publishedCriteria)
            {
                await _unitOfWork.FrameworkRubricCriteria.AddAsync(new FrameworkRubricCriterion
                {
                    Id = Guid.NewGuid(),
                    FrameworkId = framework.Id,
                    FrameworkVersionId = draft.Id,
                    Name = source.Name,
                    Description = source.Description,
                    EvidenceGuidance = source.EvidenceGuidance,
                    MaxScore = source.MaxScore,
                    DisplayOrder = source.DisplayOrder,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return (framework, published, draft, publishedCriteria);
    }

    private async Task<Program> CreateAdvProgramAsync(
        string code,
        string name,
        string description,
        ProgramStatus status,
        Guid frameworkId,
        Guid frameworkVersionId,
        Guid advisorExpertId)
    {
        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            SeriesName = SeedExpertAdvisorySeriesName,
            Description = description,
            Level = DifficultyLevel.Beginner,
            Category = ProgramCategory.Technology,
            EstimatedDuration = "6 weeks at 3 hours a week",
            SkillsGained = "Maker safety, facilitation, learning progression",
            ThumbnailUrl =
                "https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?q=80&w=1170&auto=format&fit=crop",
            Status = status,
            Price = SeedAdvCatalogPrice,
            RetakeFee = CatalogRetakeFee(SeedAdvCatalogPrice),
            FrameworkId = frameworkId,
            FrameworkVersionId = frameworkVersionId,
            AdvisorExpertId = advisorExpertId,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Programs.AddAsync(program);
        await _unitOfWork.SaveChangesAsync();
        return program;
    }

    private async Task<AdvCurriculumBundle> EnsureAdvCurriculumAsync(
        Program program,
        string slug,
        bool includeAssignment)
    {
        var experiential = await EnsureAdvModuleAsync(
            program.Id,
            $"MOD-ADV-{slug}-01",
            "Maker Lab (Experiential)",
            ModuleType.Experiential,
            moduleOrder: 1,
            ["Set up a safe maker workspace", "Complete one Offline facilitation loop"]);
        var theory = await EnsureAdvModuleAsync(
            program.Id,
            $"MOD-ADV-{slug}-02",
            "Maker Theory",
            ModuleType.Theory,
            moduleOrder: 2,
            ["Explain progression from reading to lab", "Identify facilitation checkpoints"],
            prerequisiteModuleId: experiential.Id);

        var expCourse = await EnsureAdvCourseAsync(
            experiential.Id,
            $"CRS-ADV-{slug}-01",
            "Offline Lab Course",
            "Hands-on Offline activity to satisfy MinOfflineSessions.");
        var theoryCourse = await EnsureAdvCourseAsync(
            theory.Id,
            $"CRS-ADV-{slug}-02",
            "Theory Studio",
            "SelfPaced theory before or after the lab.");

        var offline = await EnsureAdvActivityAsync(
            expCourse.Id,
            $"ACT-ADV-{slug}-EX-OFF",
            "Offline maker lab",
            ActivityType.Offline,
            activityOrder: 1,
            "Facilitated Offline session (MinOfflineSessions).",
            durationMinutes: 90,
            requireQrCheckin: true,
            requireMediaEvidence: true);
        var theorySp = await EnsureAdvActivityAsync(
            theoryCourse.Id,
            $"ACT-ADV-{slug}-TH-SP",
            "Theory SelfPaced reading",
            ActivityType.SelfPaced,
            activityOrder: 1,
            "Self-paced reading for learning progression.",
            durationMinutes: null,
            requireQrCheckin: false);

        if (includeAssignment)
        {
            await EnsureAdvModuleAssignmentAsync(
                experiential.Id,
                $"ASG-ADV-{slug}-01",
                "Maker lab reflection");
        }

        return new AdvCurriculumBundle(experiential, theory, expCourse, theoryCourse, offline, theorySp);
    }

    private async Task<Module> EnsureAdvModuleAsync(
        Guid programId,
        string code,
        string name,
        ModuleType moduleType,
        int moduleOrder,
        string[] learningOutcomes,
        Guid? prerequisiteModuleId = null)
    {
        var existing = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == code && !m.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var module = new Module
        {
            Id = Guid.NewGuid(),
            Code = code,
            ProgramId = programId,
            Name = name,
            ModuleType = moduleType,
            ModuleOrder = moduleOrder,
            PrerequisiteModuleId = prerequisiteModuleId,
            IsMandatory = true,
            LearningOutcomes = learningOutcomes,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Modules.AddAsync(module);
        await _unitOfWork.SaveChangesAsync();
        return module;
    }

    private async Task<Course> EnsureAdvCourseAsync(
        Guid moduleId,
        string code,
        string name,
        string description)
    {
        var existing = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == code && !c.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            Name = name,
            Description = description,
            CourseOrder = 1,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();
        return course;
    }

    private async Task<Activity> EnsureAdvActivityAsync(
        Guid courseId,
        string code,
        string name,
        ActivityType activityType,
        int activityOrder,
        string description,
        int? durationMinutes,
        bool requireQrCheckin,
        bool requireMediaEvidence = false)
    {
        var existing = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Code = code,
            CourseId = courseId,
            Name = name,
            ActivityType = activityType,
            Description = description,
            ActivityOrder = activityOrder,
            DurationMinutes = durationMinutes,
            RequireQrCheckin = requireQrCheckin,
            RequireMediaEvidence = requireMediaEvidence,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Activities.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();
        return activity;
    }

    private async Task EnsureAdvModuleAssignmentAsync(Guid moduleId, string code, string title)
    {
        var existing = await _unitOfWork.Assignments.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            return;
        }

        await _unitOfWork.Assignments.AddAsync(new Assignment
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            CourseId = null,
            Title = title,
            Description = "Module-scoped reflection for advisory FE demos.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = false,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task SeedAdvSuggestionThreadAsync(
        Program program,
        Guid authorUserId,
        ProgramAdvisoryTargetType targetType,
        Guid targetId,
        string targetLabel,
        string targetContext,
        string messageText)
    {
        if (authorUserId == Guid.Empty)
        {
            return;
        }

        var thread = new ProgramAdvisoryThread
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            AuthorUserId = authorUserId,
            SubmissionId = null,
            TargetType = targetType,
            TargetId = targetId,
            TargetLabel = targetLabel,
            TargetContext = targetContext,
            Type = ProgramAdvisoryThreadType.Suggestion,
            Status = ProgramAdvisoryThreadStatus.Open,
            LastMessageAt = _seedNow.AddHours(-6),
            CreatedAt = _seedNow.AddHours(-6),
            CreatedBy = authorUserId,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramAdvisoryThreads.AddAsync(thread);
        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = authorUserId,
            Message = messageText,
            CreatedAt = _seedNow.AddHours(-6),
            CreatedBy = authorUserId,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ProgramAdvisoryThread> SeedAdvRequiredChangeAddressedAsync(
        Program program,
        Activity activity,
        Guid expertUserId,
        Guid managerUserId)
    {
        var thread = new ProgramAdvisoryThread
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            AuthorUserId = expertUserId,
            SubmissionId = null,
            TargetType = ProgramAdvisoryTargetType.Activity,
            TargetId = activity.Id,
            TargetLabel = activity.Name,
            TargetContext = "Offline lab facilitation checklist",
            Type = ProgramAdvisoryThreadType.RequiredChange,
            Status = ProgramAdvisoryThreadStatus.Addressed,
            LastMessageAt = _seedNow.AddHours(-2),
            CreatedAt = _seedNow.AddDays(-2),
            CreatedBy = expertUserId,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramAdvisoryThreads.AddAsync(thread);
        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = expertUserId,
            Message = "Required: add an explicit safety reset step at the end of the Offline lab.",
            CreatedAt = _seedNow.AddDays(-2),
            CreatedBy = expertUserId,
            IsDeleted = false,
        });
        await _unitOfWork.ProgramAdvisoryMessages.AddAsync(new ProgramAdvisoryMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            AuthorUserId = managerUserId,
            Message = "đã chỉnh sửa activity description và checklist theo yêu cầu.",
            CreatedAt = _seedNow.AddHours(-2),
            CreatedBy = managerUserId,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
        return thread;
    }

    private async Task<ProgramReviewSubmission> SeedAdvSubmissionAsync(
        Program program,
        Guid managerId,
        Guid advisorExpertId,
        Guid frameworkVersionId,
        IReadOnlyList<FrameworkRubricCriterion> criteria,
        int submissionNumber,
        ProgramReviewSubmissionStatus status,
        DateTime? closedAt)
    {
        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, program.Id);
        var curriculumJson = CurriculumReviewSnapshotBuilder.BuildCurriculumSnapshotJson(tree);
        var rubricJson = CurriculumReviewSnapshotBuilder.BuildRubricSnapshotJson(criteria);

        var submission = new ProgramReviewSubmission
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            SubmissionNumber = submissionNumber,
            SubmittedByManagerId = managerId,
            AssignedAdvisorExpertId = advisorExpertId,
            FrameworkVersionId = frameworkVersionId,
            CurriculumSnapshotJson = curriculumJson,
            RubricSnapshotJson = rubricJson,
            Status = status,
            SubmittedAt = closedAt?.AddDays(-2) ?? _seedNow.AddHours(-4),
            ClosedAt = closedAt,
            ConcurrencyVersion = Guid.NewGuid(),
            CreatedAt = closedAt?.AddDays(-2) ?? _seedNow.AddHours(-4),
            CreatedBy = managerId,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramReviewSubmissions.AddAsync(submission);
        await _unitOfWork.SaveChangesAsync();
        return submission;
    }

    private async Task SeedAdvReviewDraftAsync(
        ProgramReviewSubmission submission,
        Guid advisorExpertId,
        IReadOnlyList<FrameworkRubricCriterion> criteria)
    {
        var first = criteria.OrderBy(c => c.DisplayOrder).FirstOrDefault();
        var scores = first == null
            ? new List<ReviewCriterionScoreRequest>()
            : new List<ReviewCriterionScoreRequest>
            {
                new()
                {
                    CriterionId = first.Id,
                    Score = 7,
                    Comment = "Partial draft score — safety looks solid so far.",
                },
            };

        await _unitOfWork.ProgramReviewDrafts.AddAsync(new ProgramReviewDraft
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            AdvisorExpertId = advisorExpertId,
            ScoresJson = JsonSerializer.Serialize(scores, SeedAdvJsonOptions),
            OverallComment = "Draft in progress — still scoring learning progression.",
            ConcurrencyVersion = Guid.NewGuid(),
            LastSavedAt = _seedNow,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task SeedAdvCurriculumReviewAsync(
        Program program,
        Guid expertId,
        Guid? submissionId,
        bool snapshotAvailable,
        IReadOnlyList<FrameworkRubricCriterion> criteria,
        bool includeScores)
    {
        var reviewId = Guid.NewGuid();
        await _unitOfWork.CurriculumReviews.AddAsync(new CurriculumReview
        {
            Id = reviewId,
            ProgramId = program.Id,
            ExpertId = expertId,
            Round = 1,
            SubmissionId = submissionId,
            SnapshotAvailable = snapshotAvailable,
            Decision = CurriculumReviewDecision.Approved,
            Comment = snapshotAvailable
                ? "Approved with full rubric scores."
                : "Legacy approval before snapshot-backed submissions.",
            ReviewedAt = _seedNow.AddDays(-2),
            CreatedAt = _seedNow.AddDays(-2),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });

        if (includeScores)
        {
            foreach (var criterion in criteria.OrderBy(c => c.DisplayOrder))
            {
                await _unitOfWork.ReviewCriterionScores.AddAsync(new ReviewCriterionScore
                {
                    Id = Guid.NewGuid(),
                    CurriculumReviewId = reviewId,
                    FrameworkRubricCriterionId = criterion.Id,
                    Score = criterion.MaxScore - 1,
                    CriterionNameSnapshot = criterion.Name,
                    CriterionDescriptionSnapshot = criterion.Description,
                    EvidenceGuidanceSnapshot = criterion.EvidenceGuidance,
                    MaxScoreSnapshot = criterion.MaxScore,
                    Comment = $"Scored {criterion.Name} for FE demo.",
                    CreatedAt = _seedNow.AddDays(-2),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private sealed record AdvCurriculumBundle(
        Module ExperientialModule,
        Module TheoryModule,
        Course ExperientialCourse,
        Course TheoryCourse,
        Activity OfflineActivity,
        Activity TheorySelfPaced);
}
