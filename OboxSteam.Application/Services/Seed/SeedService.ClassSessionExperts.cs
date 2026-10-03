using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// EXP co-teach fixtures:
/// <list type="bullet">
/// <item>Smart City capstone (CLS-CAP-SMARTCITY-2026A) — EXP-007 + EXP-001 co-teach the live Module 2 Offline.</item>
/// <item>Active Art/Math cohorts — Accepted Offline co-teach so catalog programs show expert presence.</item>
/// </list>
/// Idempotent for re-seed.
/// </summary>
public partial class SeedService
{

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
        _loggerService.LogInformation("Starting seed class session experts (Capstone + Art/Math Offline)");

        var capstoneSeeded = await SeedCapstoneCoTeachAsync();
        var artMathSeeded = await SeedArtMathOfflineCoTeachAsync();

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed class session experts — Capstone={CapstoneCount}, Art/Math Offline={ArtMathCount}.",
            capstoneSeeded,
            artMathSeeded);
    }

    private async Task<int> SeedCapstoneCoTeachAsync()
    {
        var experts = await EnsureCapstoneProgramBoardsAsync();
        if (experts.Count == 0)
        {
            _loggerService.LogWarning("Capstone co-teach experts missing. Skipping capstone co-teach seed.");
            return 0;
        }

        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == CapstoneLiveClassCode && !c.IsDeleted);
        var sessions = classEntity == null ? null : await LoadCapstoneLiveSessionsAsync(classEntity);
        if (sessions == null)
        {
            _loggerService.LogWarning(
                "{ClassCode} or its Module 2 Offline is missing. Skipping capstone co-teach seed.",
                CapstoneLiveClassCode);
            return 0;
        }

        var seeded = 0;
        foreach (var expert in experts)
        {
            if (await TryEnsureAcceptedCoTeachAsync(sessions.Offline, expert.Id))
            {
                seeded++;
            }
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
