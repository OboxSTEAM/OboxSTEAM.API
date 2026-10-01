using System.Text.Json;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Advisory chat, approval and change-log fixtures on the ADV programs:
/// DRAFT-ADVICE (chat with mentions and pins), PENDING (approval requested),
/// APPROVED (publishable approval) and RESUBMIT (approval revoked by curriculum edits).
/// Idempotent per program on <c>seed:</c> chat messages.
/// </summary>
public partial class SeedService
{
    private const string SeedChatClientPrefix = "seed:";
    private const string SeedAdvTheoryReadingUrl =
        "https://cdn.example.com/seed/expert-advisory/theory-reading-pack.pdf";

    private async Task SeedAdvisoryChatDemoAsync()
    {
        _loggerService.LogInformation("Starting seed advisory chat demo");

        var manager = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNG-001" && !u.IsDeleted);
        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        var expertUser = expert?.UserId is { } expertUserId
            ? await _unitOfWork.Users.GetByIdAsync(expertUserId)
            : null;
        if (manager == null || expert == null || expertUser == null)
        {
            _loggerService.LogWarning(
                "MNG-001 or EXP-001 (with a linked login) missing. Skipping advisory chat demo seed.");
            return;
        }

        var actors = new AdvChatActors(
            manager,
            AdvisoryParticipantAccess.DisplayName(manager),
            expert,
            expertUser,
            AdvisoryParticipantAccess.DisplayName(expertUser));

        await SeedAdvDraftAdviceChatAsync(actors);
        await SeedAdvPendingChatAsync(actors);
        await SeedAdvApprovedChatAsync(actors);
        await SeedAdvResubmitChatAsync(actors);
        _loggerService.LogInformation("Finished seed advisory chat demo");
    }

    private async Task SeedAdvDraftAdviceChatAsync(AdvChatActors actors)
    {
        var program = await FindAdvChatProgramAsync(SeedAdvDraftAdviceCode);
        if (program == null)
        {
            return;
        }

        var (curriculum, _) = await EnsureAdvApprovableCurriculumAsync(program, "DRAFT-ADVICE");
        var start = _seedNow.AddDays(-4);

        await AddSeedChatMessageAsync(
            program,
            actors.Manager,
            "kickoff",
            $"Hi, the first draft is ready. Could you start with {Mention(ProgramAdvisoryTargetType.Module, curriculum.ExperientialModule.Id)} " +
            $"and the lab session {Mention(ProgramAdvisoryTargetType.Activity, curriculum.OfflineActivity.Id)}?",
            start,
            (ProgramAdvisoryTargetType.Module, curriculum.ExperientialModule.Id, curriculum.ExperientialModule.Name),
            (ProgramAdvisoryTargetType.Activity, curriculum.OfflineActivity.Id, curriculum.OfflineActivity.Name));

        var safetyPin = await AddSeedChatMessageAsync(
            program,
            actors.ExpertUser,
            "safety-briefing",
            $"{Mention(ProgramAdvisoryTargetType.Activity, curriculum.OfflineActivity.Id)} needs a safety briefing " +
            "before any tools are handed out. Please add it to the activity description.",
            start.AddHours(20),
            (ProgramAdvisoryTargetType.Activity, curriculum.OfflineActivity.Id, curriculum.OfflineActivity.Name));
        safetyPin.PinStatus = DiscussionPinStatus.Open;
        safetyPin.PinnedByUserId = actors.ExpertUser.Id;
        safetyPin.PinnedAt = start.AddHours(20).AddMinutes(5);

        var outcomesPin = await AddSeedChatMessageAsync(
            program,
            actors.ExpertUser,
            "theory-outcomes",
            $"The learning outcomes of {Mention(ProgramAdvisoryTargetType.Module, curriculum.TheoryModule.Id)} " +
            "should name a measurable checkpoint.",
            start.AddHours(20).AddMinutes(10),
            (ProgramAdvisoryTargetType.Module, curriculum.TheoryModule.Id, curriculum.TheoryModule.Name));
        outcomesPin.PinStatus = DiscussionPinStatus.Addressed;
        outcomesPin.PinnedByUserId = actors.ExpertUser.Id;
        outcomesPin.PinnedAt = start.AddHours(20).AddMinutes(12);
        outcomesPin.AddressedByUserId = actors.Manager.Id;
        outcomesPin.AddressedAt = start.AddDays(2);

        var managerReply = await AddSeedChatMessageAsync(
            program,
            actors.Manager,
            "outcomes-updated",
            "I reworded the theory outcomes around the lab checkpoint and marked that pin as addressed. " +
            "Requesting your approval now.",
            start.AddDays(2).AddMinutes(1));
        await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.ApprovalRequested,
            new { requestedByName = actors.ManagerName },
            start.AddDays(2).AddMinutes(2),
            actors.Manager.Id);

        var expertReply = await AddSeedChatMessageAsync(
            program,
            actors.ExpertUser,
            "briefing-still-open",
            "Thanks, the outcomes look good. The safety briefing pin is still open, so I can't approve yet.",
            start.AddDays(3));

        await SaveAdvChatAsync(program, actors.Manager, managerReply.Sequence);
        await AddAdvChatNotificationsAsync(
        [
            (NotificationCatalog.AdvisoryDiscussionMessage(
                    actors.ExpertUser.Id,
                    program.Id,
                    managerReply.Id,
                    actors.Manager.Id,
                    program.Name,
                    actors.ManagerName),
                actors.ExpertUser.Id,
                RoleType.Expert,
                managerReply.CreatedAt),
            (NotificationCatalog.AdvisoryDiscussionMessage(
                    actors.Manager.Id,
                    program.Id,
                    expertReply.Id,
                    actors.ExpertUser.Id,
                    program.Name,
                    actors.ExpertName),
                actors.Manager.Id,
                RoleType.Manager,
                expertReply.CreatedAt),
            (NotificationCatalog.AdvisoryMentionPinned(
                    program.Id,
                    safetyPin.Id,
                    actors.ExpertUser.Id,
                    program.Name,
                    actors.ExpertName),
                actors.Manager.Id,
                RoleType.Manager,
                safetyPin.PinnedAt!.Value),
        ]);
        _loggerService.LogInformation("Seeded advisory chat for {Code}", program.Code);
    }

    private async Task SeedAdvPendingChatAsync(AdvChatActors actors)
    {
        var program = await FindAdvChatProgramAsync(SeedAdvPendingCode);
        if (program == null)
        {
            return;
        }

        var (_, research) = await EnsureAdvApprovableCurriculumAsync(program, "PENDING");
        var start = _seedNow.AddDays(-2);

        var request = await AddSeedChatMessageAsync(
            program,
            actors.Manager,
            "ready",
            $"All four modules are in place, including {Mention(ProgramAdvisoryTargetType.Module, research.Module.Id)}. " +
            "Ready for your approval.",
            start,
            (ProgramAdvisoryTargetType.Module, research.Module.Id, research.Module.Name));
        var requested = await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.ApprovalRequested,
            new { requestedByName = actors.ManagerName },
            start.AddMinutes(1),
            actors.Manager.Id);

        await SaveAdvChatAsync(program, actors.Manager, request.Sequence);
        await AddAdvChatNotificationsAsync(
        [
            (NotificationCatalog.CurriculumApprovalRequested(
                    actors.ExpertUser.Id,
                    program.Id,
                    actors.Manager.Id,
                    program.Name,
                    actors.ManagerName),
                actors.ExpertUser.Id,
                RoleType.Expert,
                requested.CreatedAt),
        ]);
        _loggerService.LogInformation("Seeded advisory chat for {Code}", program.Code);
    }

    private async Task SeedAdvApprovedChatAsync(AdvChatActors actors)
    {
        var program = await FindAdvChatProgramAsync(SeedAdvApprovedCode);
        if (program == null)
        {
            return;
        }

        var (_, research) = await EnsureAdvApprovableCurriculumAsync(program, "APPROVED");
        var start = _seedNow.AddDays(-5);
        const string comment = "Safe lab flow and a clear capstone. Ready to publish.";

        var request = await AddSeedChatMessageAsync(
            program,
            actors.Manager,
            "ready",
            "The curriculum is complete. Requesting your approval so we can publish next week.",
            start);
        await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.ApprovalRequested,
            new { requestedByName = actors.ManagerName },
            start.AddMinutes(1),
            actors.Manager.Id);
        await AddSeedChatMessageAsync(
            program,
            actors.ExpertUser,
            "capstone-checked",
            $"I checked {Mention(ProgramAdvisoryTargetType.ResearchMilestone, research.Milestone.Id)} and the lab safety notes. Approving.",
            start.AddDays(1),
            (ProgramAdvisoryTargetType.ResearchMilestone, research.Milestone.Id, research.Milestone.Title));

        var approvedAt = start.AddDays(1).AddMinutes(2);
        var approval = await FindActiveSeedApprovalAsync(program.Id)
                       ?? await CreateSeedApprovalAsync(program, actors, approvedAt, comment);
        if (approval == null)
        {
            return;
        }

        var approved = await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.Approved,
            new
            {
                approvalId = approval.Id,
                curriculumVersion = approval.CurriculumVersion,
                approvedByName = actors.ApproverName,
                comment,
            },
            approvedAt,
            actors.ExpertUser.Id);
        program.Status = ProgramStatus.Approved;

        await SaveAdvChatAsync(program, actors.Manager, request.Sequence);
        await AddAdvChatNotificationsAsync(
        [
            (NotificationCatalog.CurriculumReviewApproved(
                    program.Id,
                    approval.Id,
                    actors.ExpertUser.Id,
                    program.Name,
                    actors.ApproverName),
                actors.Manager.Id,
                RoleType.Manager,
                approved.CreatedAt),
        ]);
        _loggerService.LogInformation("Seeded advisory chat for {Code}", program.Code);
    }

    private async Task SeedAdvResubmitChatAsync(AdvChatActors actors)
    {
        var program = await FindAdvChatProgramAsync(SeedAdvResubmitCode);
        if (program == null)
        {
            return;
        }

        var (curriculum, research) = await EnsureAdvApprovableCurriculumAsync(program, "RESUBMIT");
        var start = _seedNow.AddDays(-6);
        const string comment = "Approved for the first cohort.";

        await AddSeedChatMessageAsync(
            program,
            actors.Manager,
            "ready",
            "First version of the revised maker curriculum is ready for approval.",
            start);
        await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.ApprovalRequested,
            new { requestedByName = actors.ManagerName },
            start.AddMinutes(1),
            actors.Manager.Id);
        await AddSeedChatMessageAsync(
            program,
            actors.ExpertUser,
            "approve-v1",
            $"{Mention(ProgramAdvisoryTargetType.Module, research.Module.Id)} is well scoped. Approving this version.",
            start.AddDays(1),
            (ProgramAdvisoryTargetType.Module, research.Module.Id, research.Module.Name));

        var approvedAt = start.AddDays(1).AddMinutes(2);
        var approval = (await _unitOfWork.ProgramApprovals.GetAllAsync(
                               a => a.ProgramId == program.Id && !a.IsDeleted))
                           .OrderByDescending(a => a.ApprovedAt)
                           .FirstOrDefault()
                       ?? await CreateSeedApprovalAsync(program, actors, approvedAt, comment);
        if (approval == null)
        {
            return;
        }

        await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.Approved,
            new
            {
                approvalId = approval.Id,
                curriculumVersion = approval.CurriculumVersion,
                approvedByName = actors.ApproverName,
                comment,
            },
            approvedAt,
            actors.ExpertUser.Id);

        var editAt = _seedNow.AddDays(-2);
        var addedActivity = await ApplyAdvResubmitRevisionAsync(program, curriculum, research, actors, editAt);

        if (approval.RevokedAt == null)
        {
            approval.RevokedAt = editAt;
            approval.RevokedByUserId = actors.Manager.Id;
            approval.RevokeReason = ProgramApprovalRevokeReason.CurriculumEdited;
            await _unitOfWork.ProgramApprovals.Update(approval);
        }

        var revoked = await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.ApprovalRevoked,
            new
            {
                approvalId = approval.Id,
                reason = ProgramApprovalRevokeReason.CurriculumEdited.ToString(),
                actorName = actors.ManagerName,
            },
            editAt,
            actors.Manager.Id);
        await AddSeedSystemMessageAsync(
            program,
            DiscussionSystemEventCode.CurriculumUpdated,
            new CurriculumUpdatedPayload
            {
                ActorUserId = actors.Manager.Id,
                ActorName = actors.ManagerName,
                FromVersion = approval.CurriculumVersion,
                ToVersion = program.CurriculumVersion,
                ChangeCount = CurriculumChangeConsolidator.Consolidate(
                    await _unitOfWork.CurriculumChanges.GetAllAsync(
                        c => c.ProgramId == program.Id && c.Version > approval.CurriculumVersion)).Count,
            },
            editAt.AddMinutes(6),
            actors.Manager.Id);
        program.Status = ProgramStatus.Draft;

        var reReview = await AddSeedChatMessageAsync(
            program,
            actors.Manager,
            "re-review",
            $"I added {Mention(ProgramAdvisoryTargetType.Activity, addedActivity.Id)}, tightened the theory outcomes " +
            "and moved the research capstone before theory. Please review again.",
            editAt.AddMinutes(10),
            (ProgramAdvisoryTargetType.Activity, addedActivity.Id, addedActivity.Name));
        await AddSeedChatMessageAsync(
            program,
            actors.ExpertUser,
            "will-review",
            "Got it, I'll go through the changes this week.",
            editAt.AddHours(5));

        await SaveAdvChatAsync(program, actors.Manager, reReview.Sequence);
        await AddAdvChatNotificationsAsync(
        [
            (NotificationCatalog.CurriculumApprovalRevoked(
                    actors.ExpertUser.Id,
                    program.Id,
                    actors.Manager.Id,
                    program.Name,
                    actors.ManagerName),
                actors.ExpertUser.Id,
                RoleType.Expert,
                revoked.CreatedAt),
        ]);
        _loggerService.LogInformation("Seeded advisory chat for {Code}", program.Code);
    }

    /// <summary>
    /// Applies the RESUBMIT revision (added activity and material, outcome and description edits,
    /// module reorder) and writes the change-log rows the recorder would have produced, one version per save.
    /// </summary>
    private async Task<Activity> ApplyAdvResubmitRevisionAsync(
        Program program,
        AdvCurriculumBundle curriculum,
        AdvResearchBundle research,
        AdvChatActors actors,
        DateTime editAt)
    {
        var baseVersion = program.CurriculumVersion;
        var programSegment = new CurriculumPathSegment(ProgramAdvisoryTargetType.Program, program.Id, program.Name);
        var theoryModuleSegment = new CurriculumPathSegment(
            ProgramAdvisoryTargetType.Module, curriculum.TheoryModule.Id, curriculum.TheoryModule.Name);
        var theoryCourseSegment = new CurriculumPathSegment(
            ProgramAdvisoryTargetType.Course, curriculum.TheoryCourse.Id, curriculum.TheoryCourse.Name);

        var added = await EnsureAdvActivityAsync(
            curriculum.TheoryCourse.Id,
            "ACT-ADV-RESUBMIT-TH-SP2",
            "Theory follow-up SelfPaced",
            ActivityType.SelfPaced,
            activityOrder: 2,
            "Second SelfPaced activity with a learner checkpoint.",
            durationMinutes: null,
            requireQrCheckin: false);
        await EnsureAdvMaterialAsync(
            added,
            "Follow-up checkpoint handout",
            "https://cdn.example.com/seed/expert-advisory/follow-up-checkpoint.pdf");
        var material = await _unitOfWork.Materials.FirstOrDefaultAsync(m => m.ActivityId == added.Id && !m.IsDeleted);

        var outcomesBefore = curriculum.TheoryModule.LearningOutcomes.ToList();
        curriculum.TheoryModule.LearningOutcomes =
        [
            "Explain progression from reading to lab",
            "Identify facilitation checkpoints",
            "Evaluate a safe reset plan after peer feedback",
        ];
        var descriptionBefore = curriculum.TheoryCourse.Description;
        curriculum.TheoryCourse.Description = "SelfPaced theory with a checkpoint and a reflection step.";
        var theoryOrderBefore = curriculum.TheoryModule.ModuleOrder;
        var researchOrderBefore = research.Module.ModuleOrder;
        curriculum.TheoryModule.ModuleOrder = 3;
        research.Module.ModuleOrder = 2;
        await _unitOfWork.Modules.Update(curriculum.TheoryModule);
        await _unitOfWork.Modules.Update(research.Module);
        await _unitOfWork.Courses.Update(curriculum.TheoryCourse);

        var rows = new List<CurriculumChange>
        {
            NewSeedChange(program, baseVersion + 1, actors, editAt, ProgramAdvisoryTargetType.Activity, added.Id,
                added.Name, CurriculumChangeKind.Created, [programSegment, theoryModuleSegment, theoryCourseSegment],
                [
                    CreatedField("name", added.Name),
                    CreatedField("code", added.Code),
                    CreatedField("activityType", added.ActivityType),
                    CreatedField("description", added.Description),
                    CreatedField("requireQrCheckin", added.RequireQrCheckin),
                    CreatedField("requireMediaEvidence", added.RequireMediaEvidence),
                ],
                parentBefore: null, parentAfter: (curriculum.TheoryCourse.Id, curriculum.TheoryCourse.Name),
                orderBefore: null, orderAfter: added.ActivityOrder),
        };
        if (material != null)
        {
            rows.Add(NewSeedChange(program, baseVersion + 1, actors, editAt, ProgramAdvisoryTargetType.Material,
                material.Id, material.Title, CurriculumChangeKind.Created,
                [
                    programSegment,
                    theoryModuleSegment,
                    theoryCourseSegment,
                    new CurriculumPathSegment(ProgramAdvisoryTargetType.Activity, added.Id, added.Name),
                ],
                [
                    CreatedField("title", material.Title),
                    CreatedField("materialType", material.MaterialType),
                    CreatedField("fileUrl", material.FileUrl),
                ],
                parentBefore: null, parentAfter: (added.Id, added.Name),
                orderBefore: null, orderAfter: null));
        }

        rows.Add(NewSeedChange(program, baseVersion + 2, actors, editAt.AddMinutes(2),
            ProgramAdvisoryTargetType.Module, curriculum.TheoryModule.Id, curriculum.TheoryModule.Name,
            CurriculumChangeKind.Updated, [programSegment],
            [
                new CurriculumFieldChange
                {
                    FieldKey = "learningOutcomes",
                    Before = CurriculumChangeJson.ToNode(outcomesBefore),
                    After = CurriculumChangeJson.ToNode(curriculum.TheoryModule.LearningOutcomes),
                },
            ],
            parentBefore: (program.Id, program.Name), parentAfter: (program.Id, program.Name),
            orderBefore: theoryOrderBefore, orderAfter: theoryOrderBefore));
        rows.Add(NewSeedChange(program, baseVersion + 3, actors, editAt.AddMinutes(4),
            ProgramAdvisoryTargetType.Course, curriculum.TheoryCourse.Id, curriculum.TheoryCourse.Name,
            CurriculumChangeKind.Updated, [programSegment, theoryModuleSegment],
            [
                new CurriculumFieldChange
                {
                    FieldKey = "description",
                    Before = CurriculumChangeJson.ToNode(descriptionBefore),
                    After = CurriculumChangeJson.ToNode(curriculum.TheoryCourse.Description),
                },
            ],
            parentBefore: (curriculum.TheoryModule.Id, curriculum.TheoryModule.Name),
            parentAfter: (curriculum.TheoryModule.Id, curriculum.TheoryModule.Name),
            orderBefore: curriculum.TheoryCourse.CourseOrder, orderAfter: curriculum.TheoryCourse.CourseOrder));
        rows.Add(NewSeedChange(program, baseVersion + 4, actors, editAt.AddMinutes(6),
            ProgramAdvisoryTargetType.Module, curriculum.TheoryModule.Id, curriculum.TheoryModule.Name,
            CurriculumChangeKind.Reordered, [programSegment], [],
            parentBefore: (program.Id, program.Name), parentAfter: (program.Id, program.Name),
            orderBefore: theoryOrderBefore, orderAfter: curriculum.TheoryModule.ModuleOrder));
        rows.Add(NewSeedChange(program, baseVersion + 4, actors, editAt.AddMinutes(6),
            ProgramAdvisoryTargetType.Module, research.Module.Id, research.Module.Name,
            CurriculumChangeKind.Reordered, [programSegment], [],
            parentBefore: (program.Id, program.Name), parentAfter: (program.Id, program.Name),
            orderBefore: researchOrderBefore, orderAfter: research.Module.ModuleOrder));

        await _unitOfWork.CurriculumChanges.AddRangeAsync(rows);
        foreach (var row in rows)
        {
            row.CreatedAt = row.At;
        }

        program.CurriculumVersion = baseVersion + 4;
        await _unitOfWork.SaveChangesAsync();
        return added;
    }

    /// <summary>
    /// Brings an ADV curriculum up to Maker framework v1 (4 modules, an Offline session,
    /// a capstone milestone). Every SelfPaced activity gets a material so the generic
    /// materials seed cannot change the curriculum after an approval snapshot.
    /// </summary>
    private async Task<(AdvCurriculumBundle Curriculum, AdvResearchBundle Research)> EnsureAdvApprovableCurriculumAsync(
        Program program,
        string slug)
    {
        var curriculum = await EnsureAdvCurriculumAsync(program, slug, includeAssignment: true);
        await EnsureAdvMaterialAsync(curriculum.TheorySelfPaced, "Theory reading pack", SeedAdvTheoryReadingUrl);
        var research = await EnsureAdvResearchFlowAsync(program, curriculum.TheorySelfPaced, slug);

        var showcase = await EnsureAdvModuleAsync(
            program.Id,
            $"MOD-ADV-{slug}-04",
            "Maker Showcase",
            ModuleType.Theory,
            moduleOrder: 4,
            ["Present a maker build to peers", "Collect structured peer feedback"]);
        var showcaseCourse = await EnsureAdvCourseAsync(
            showcase.Id,
            $"CRS-ADV-{slug}-04",
            "Showcase Prep",
            "SelfPaced rehearsal before the final showcase.");
        var rehearsal = await EnsureAdvActivityAsync(
            showcaseCourse.Id,
            $"ACT-ADV-{slug}-SH-SP",
            "Showcase rehearsal",
            ActivityType.SelfPaced,
            activityOrder: 1,
            "Rehearse the build demo with the showcase checklist.",
            durationMinutes: null,
            requireQrCheckin: false);
        await EnsureAdvMaterialAsync(
            rehearsal,
            "Showcase rehearsal checklist",
            "https://cdn.example.com/seed/expert-advisory/showcase-checklist.pdf");

        return (curriculum, research);
    }

    private async Task<Program?> FindAdvChatProgramAsync(string code)
    {
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted);
        if (program == null)
        {
            _loggerService.LogWarning("Advisory program {Code} missing. Skipping its chat seed.", code);
            return null;
        }

        var seeded = await _unitOfWork.ProgramAdvisoryDiscussionMessages.FirstOrDefaultAsync(
            m => m.ProgramId == program.Id && m.ClientMessageId.StartsWith(SeedChatClientPrefix));
        if (seeded != null)
        {
            _loggerService.LogInformation("Advisory chat for {Code} already seeded. Skipping.", code);
            return null;
        }

        return program;
    }

    private async Task<ProgramApproval?> FindActiveSeedApprovalAsync(Guid programId)
        => await _unitOfWork.ProgramApprovals.FirstOrDefaultAsync(
            a => a.ProgramId == programId && a.RevokedAt == null && !a.IsDeleted);

    private async Task<ProgramApproval?> CreateSeedApprovalAsync(
        Program program,
        AdvChatActors actors,
        DateTime approvedAt,
        string comment)
    {
        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, program.Id);
        var check = await ProgramFrameworkCheck.RunAsync(_unitOfWork, program, tree);
        if (!check.AllPassed)
        {
            _loggerService.LogWarning(
                "Advisory program {Code} fails its framework check. Skipping its approval seed.",
                program.Code);
            return null;
        }

        var approval = new ProgramApproval
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            CurriculumVersion = program.CurriculumVersion,
            FromVersion = 0,
            FrameworkVersionId = program.FrameworkVersionId,
            FrameworkCheckJson = JsonSerializer.Serialize(check, CurriculumChangeJson.Options),
            CurriculumSnapshotJson = CurriculumSnapshotBuilder.BuildCurriculumSnapshotJson(tree),
            ApprovedByExpertId = actors.Expert.Id,
            ApprovedAt = approvedAt,
            Comment = comment,
            CreatedBy = actors.ExpertUser.Id,
        };
        await _unitOfWork.ProgramApprovals.AddAsync(approval);
        approval.CreatedAt = approvedAt;
        await _unitOfWork.SaveChangesAsync();
        return approval;
    }

    private async Task<ProgramAdvisoryDiscussionMessage> AddSeedChatMessageAsync(
        Program program,
        User author,
        string key,
        string text,
        DateTime at,
        params (ProgramAdvisoryTargetType TargetType, Guid TargetId, string Label)[] mentions)
    {
        var message = new ProgramAdvisoryDiscussionMessage
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            AuthorUserId = author.Id,
            Kind = DiscussionMessageKind.User,
            Sequence = DiscussionSystemMessageWriter.AllocateSequence(_unitOfWork, program),
            Text = text,
            ClientMessageId = $"{SeedChatClientPrefix}{program.Code}:{key}",
            CreatedBy = author.Id,
        };
        await _unitOfWork.ProgramAdvisoryDiscussionMessages.AddAsync(message);
        message.CreatedAt = at;

        for (var ordinal = 0; ordinal < mentions.Length; ordinal++)
        {
            var (targetType, targetId, label) = mentions[ordinal];
            var reference = new ProgramAdvisoryReference
            {
                Id = Guid.NewGuid(),
                ProgramId = program.Id,
                TargetType = targetType,
                TargetId = targetId,
                AnchorKind = ProgramAdvisoryAnchorKind.Node,
                CapturedLabel = label,
                CapturedExcerpt = label,
                CapturedAt = at,
                CreatedBy = author.Id,
            };
            await _unitOfWork.ProgramAdvisoryReferences.AddAsync(reference);
            reference.CreatedAt = at;
            var link = new ProgramAdvisoryDiscussionMessageReference
            {
                Id = Guid.NewGuid(),
                MessageId = message.Id,
                ReferenceId = reference.Id,
                Ordinal = ordinal,
                CreatedBy = author.Id,
            };
            await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.AddAsync(link);
            link.CreatedAt = at;
        }

        return message;
    }

    private async Task<ProgramAdvisoryDiscussionMessage> AddSeedSystemMessageAsync(
        Program program,
        DiscussionSystemEventCode code,
        object payload,
        DateTime at,
        Guid actorId)
    {
        var message = await DiscussionSystemMessageWriter.AddAsync(_unitOfWork, program, code, payload, at, actorId);
        message.CreatedAt = at;
        return message;
    }

    /// <summary>Persists the chat and moves the manager's read cursor to <paramref name="managerReadSequence"/>.</summary>
    private async Task SaveAdvChatAsync(Program program, User manager, long managerReadSequence)
    {
        await _unitOfWork.Programs.Update(program);
        var read = await _unitOfWork.ProgramAdvisoryStreamReads.FirstOrDefaultAsync(
            r => r.ProgramId == program.Id && r.UserId == manager.Id && !r.IsDeleted);
        if (read == null)
        {
            await _unitOfWork.ProgramAdvisoryStreamReads.AddAsync(new ProgramAdvisoryStreamRead
            {
                Id = Guid.NewGuid(),
                ProgramId = program.Id,
                UserId = manager.Id,
                LastReadSequence = managerReadSequence,
                CreatedBy = manager.Id,
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task AddAdvChatNotificationsAsync(
        List<(NotificationCommand Command, Guid RecipientId, RoleType Role, DateTime CreatedAt)> samples)
    {
        var recipientIds = samples.Select(s => s.RecipientId).ToHashSet();
        var existingKeys = (await _unitOfWork.Notifications.GetAllAsync(
                n => recipientIds.Contains(n.RecipientUserId) && !n.IsDeleted))
            .Select(n => (n.RecipientUserId, n.Type, n.EntityId))
            .ToHashSet();

        var notifications = new List<Notification>();
        foreach (var (command, recipientId, role, createdAt) in samples)
        {
            if (existingKeys.Add((recipientId, command.Type, command.EntityId)))
            {
                notifications.Add(ToSeedNotification(command, recipientId, role, string.Empty, readAt: null, createdAt));
            }
        }

        if (notifications.Count == 0)
        {
            return;
        }

        var createdAts = notifications.Select(n => n.CreatedAt).ToList();
        await _unitOfWork.Notifications.AddRangeAsync(notifications);
        for (var i = 0; i < notifications.Count; i++)
        {
            notifications[i].CreatedAt = createdAts[i];
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static string Mention(ProgramAdvisoryTargetType targetType, Guid targetId)
        => AdvisoryMentionTokens.Format(targetType, targetId);

    private static CurriculumFieldChange CreatedField(string fieldKey, object? value)
        => new() { FieldKey = fieldKey, Before = null, After = CurriculumChangeJson.ToNode(value) };

    private static CurriculumChange NewSeedChange(
        Program program,
        long version,
        AdvChatActors actors,
        DateTime at,
        ProgramAdvisoryTargetType targetType,
        Guid targetId,
        string label,
        CurriculumChangeKind kind,
        List<CurriculumPathSegment> path,
        List<CurriculumFieldChange> fields,
        (Guid Id, string Label)? parentBefore,
        (Guid Id, string Label)? parentAfter,
        int? orderBefore,
        int? orderAfter)
        => new()
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Version = version,
            ActorUserId = actors.Manager.Id,
            ActorName = actors.ManagerName,
            At = at,
            TargetType = targetType,
            TargetId = targetId,
            ChangeKind = kind,
            FieldsJson = CurriculumChangeJson.SerializeFields(fields),
            ParentBefore = parentBefore?.Id,
            ParentAfter = parentAfter?.Id,
            ParentBeforeLabel = parentBefore?.Label,
            ParentAfterLabel = parentAfter?.Label,
            OrderBefore = orderBefore,
            OrderAfter = orderAfter,
            LabelSnapshot = label,
            PathSnapshotJson = CurriculumChangeJson.SerializePath(path),
            CreatedBy = actors.Manager.Id,
        };

    private sealed record AdvChatActors(
        User Manager,
        string ManagerName,
        Expert Expert,
        User ExpertUser,
        string ExpertName)
    {
        public string ApproverName => string.IsNullOrWhiteSpace(Expert.FullName) ? ExpertName : Expert.FullName;
    }
}
