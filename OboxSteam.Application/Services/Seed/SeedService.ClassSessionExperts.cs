using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// EXP co-teach fixtures:
/// <list type="bullet">
/// <item>Maker Lab Adventures (CLS-DEMO-MAKER-2026A) — Slice-2 QR/check-in + feedback Offline.</item>
/// <item>Active Art/Math cohorts — Accepted Offline co-teach so catalog programs show expert presence.</item>
/// </list>
/// Idempotent for re-seed.
/// </summary>
public partial class SeedService
{
    private const string MakerCoTeachClassCode = "CLS-DEMO-MAKER-2026A";
    private const string MakerCoTeachProgramCode = "PRG-DEMO-MAKER";
    private const string MakerJoinableOfflineActivityCode = "ACT-DEMO-MAKER-02-03";

    /// <summary>
    /// InProgress Art/Math cohorts that should show at least one Accepted Offline co-teach.
    /// </summary>
    private static readonly (string ClassCode, string ProgramCode, string ExpertCode, string BoardRole)[]
        ArtMathCoTeachTargets =
        [
            ("CLS-DIGART-CURRENT", "PRG-DIGART", "EXP-001", "Digital Art Studio Advisor"),
            ("CLS-MATHFUN-CURRENT", "PRG-MATHFUN", "EXP-002", "Math Fun Workshop Advisor"),
            ("CLS-MUSICTECH-CURRENT", "PRG-MUSICTECH", "EXP-003", "Music Production Coach"),
            ("CLS-DATAMATH-CURRENT", "PRG-DATAMATH", "EXP-004", "Data Math Lab Advisor"),
        ];

    private async Task SeedClassSessionExpertsAsync()
    {
        _loggerService.LogInformation("Starting seed class session experts (Maker + Art/Math Offline)");

        var makerSeeded = await SeedMakerLabCoTeachAsync();
        var artMathSeeded = await SeedArtMathOfflineCoTeachAsync();

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed class session experts — Maker={MakerCount}, Art/Math Offline={ArtMathCount}.",
            makerSeeded,
            artMathSeeded);
    }

    private async Task<int> SeedMakerLabCoTeachAsync()
    {
        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        if (expert001 == null)
        {
            _loggerService.LogWarning("EXP-001 missing. Skipping Maker class session expert seed.");
            return 0;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == MakerCoTeachProgramCode && !p.IsDeleted);
        if (program == null)
        {
            _loggerService.LogWarning(
                "{ProgramCode} missing. Skipping Maker class session expert seed.",
                MakerCoTeachProgramCode);
            return 0;
        }

        await EnsureExpertOnProgramBoardAsync(expert001, program.Id, "Maker Co-Teach Advisor");

        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == MakerCoTeachClassCode && !c.IsDeleted);
        if (classEntity == null)
        {
            _loggerService.LogWarning(
                "{ClassCode} missing. Skipping Maker class session expert seed.",
                MakerCoTeachClassCode);
            return 0;
        }

        var offlineSessions = (await _unitOfWork.ClassSessions.GetAllAsync(
                s => s.ClassId == classEntity.Id
                     && s.SessionKind == SessionKind.Offline
                     && s.Status != ClassSessionStatus.Cancelled
                     && !s.IsDeleted))
            .OrderBy(s => s.StartTime)
            .ToList();
        if (offlineSessions.Count == 0)
        {
            _loggerService.LogWarning(
                "No Offline sessions on {ClassCode}. Skipping Maker class session expert seed.",
                MakerCoTeachClassCode);
            return 0;
        }

        var joinableOfflineActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(
            a => a.Code == MakerJoinableOfflineActivityCode && !a.IsDeleted);
        var joinableOffline = joinableOfflineActivity == null
            ? null
            : offlineSessions.FirstOrDefault(s => s.ActivityId == joinableOfflineActivity.Id);

        // Feedback fixture: prefer a non-joinable Offline (research lab). Never force
        // Completed on the Slice-2 joinable Offline — that breaks QR / check-in tests.
        var feedbackOffline = offlineSessions.FirstOrDefault(
            s => joinableOffline == null || s.Id != joinableOffline.Id);
        if (feedbackOffline == null)
        {
            feedbackOffline = offlineSessions[0];
        }

        if (feedbackOffline.Status != ClassSessionStatus.Completed
            && (joinableOffline == null || feedbackOffline.Id != joinableOffline.Id))
        {
            feedbackOffline.Status = ClassSessionStatus.Completed;
            await _unitOfWork.ClassSessions.Update(feedbackOffline);
            _loggerService.LogInformation(
                "Forced Maker research Offline {SessionId} to Completed so EXP-001 can test co-teach feedback.",
                feedbackOffline.Id);
        }

        var seeded = 0;
        if (await TryEnsureAcceptedCoTeachAsync(feedbackOffline, expert001.Id))
        {
            seeded++;
        }

        if (joinableOffline != null
            && joinableOffline.Id != feedbackOffline.Id
            && await TryEnsureAcceptedCoTeachAsync(joinableOffline, expert001.Id))
        {
            seeded++;
        }

        return seeded;
    }

    private async Task<int> SeedArtMathOfflineCoTeachAsync()
    {
        var seeded = 0;

        foreach (var target in ArtMathCoTeachTargets)
        {
            var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(
                e => e.Code == target.ExpertCode && !e.IsDeleted);
            if (expert == null)
            {
                _loggerService.LogWarning(
                    "{ExpertCode} missing. Skipping co-teach on {ClassCode}.",
                    target.ExpertCode,
                    target.ClassCode);
                continue;
            }

            var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
                p => p.Code == target.ProgramCode && !p.IsDeleted);
            if (program == null)
            {
                _loggerService.LogWarning(
                    "{ProgramCode} missing. Skipping co-teach on {ClassCode}.",
                    target.ProgramCode,
                    target.ClassCode);
                continue;
            }

            await EnsureExpertOnProgramBoardAsync(expert, program.Id, target.BoardRole);

            var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code == target.ClassCode && !c.IsDeleted);
            if (classEntity == null)
            {
                _loggerService.LogWarning(
                    "{ClassCode} missing. Skipping Art/Math Offline co-teach.",
                    target.ClassCode);
                continue;
            }

            var offlineSessions = (await _unitOfWork.ClassSessions.GetAllAsync(
                    s => s.ClassId == classEntity.Id
                         && s.SessionKind == SessionKind.Offline
                         && s.Status != ClassSessionStatus.Cancelled
                         && !s.IsDeleted))
                .OrderBy(s => s.StartTime)
                .ToList();
            if (offlineSessions.Count == 0)
            {
                _loggerService.LogWarning(
                    "No Offline sessions on {ClassCode}. Skipping Art/Math Offline co-teach.",
                    target.ClassCode);
                continue;
            }

            // Attach expert to the first two Offline studios when available (theory + experiential).
            var sessionsToAttach = offlineSessions.Take(2).ToList();
            foreach (var session in sessionsToAttach)
            {
                if (await TryEnsureAcceptedCoTeachAsync(session, expert.Id))
                {
                    seeded++;
                }
            }
        }

        return seeded;
    }

    private async Task EnsureExpertOnProgramBoardAsync(Expert expert, Guid programId, string roleInBoard)
    {
        var existing = await _unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            pb => pb.ExpertId == expert.Id && pb.ProgramId == programId && !pb.IsDeleted);
        if (existing != null)
        {
            return;
        }

        await _unitOfWork.ProgramBoards.AddAsync(new ProgramBoard
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ExpertId = expert.Id,
            RoleInBoard = roleInBoard,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    /// <returns>True when this session now has the expert Accepted (created or already present).</returns>
    private async Task<bool> TryEnsureAcceptedCoTeachAsync(ClassSession session, Guid expertId)
    {
        var mine = await _unitOfWork.ClassSessionExperts.FirstOrDefaultAsync(
            e => e.ClassSessionId == session.Id
                 && e.ExpertId == expertId
                 && !e.IsDeleted
                 && (e.Status == ClassSessionExpertStatus.Invited
                     || e.Status == ClassSessionExpertStatus.Accepted));

        if (mine == null)
        {
            await _unitOfWork.ClassSessionExperts.AddAsync(new ClassSessionExpert
            {
                Id = Guid.NewGuid(),
                ClassSessionId = session.Id,
                ExpertId = expertId,
                Status = ClassSessionExpertStatus.Accepted,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            return true;
        }

        if (mine.Status != ClassSessionExpertStatus.Accepted)
        {
            mine.Status = ClassSessionExpertStatus.Accepted;
            await _unitOfWork.ClassSessionExperts.Update(mine);
        }

        return true;
    }
}
