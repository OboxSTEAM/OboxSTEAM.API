using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// EXP-001 co-teach fixture on Maker Lab Adventures (CLS-DEMO-MAKER-2026A):
/// Accepted Offline invitations on the Slice-2 joinable Offline lab (leave status
/// alone for student10 / mentor4 QR/check-in) plus the research Offline forced
/// Completed with empty feedback so the expert can submit. Idempotent for re-seed.
/// </summary>
public partial class SeedService
{
    private const string MakerCoTeachClassCode = "CLS-DEMO-MAKER-2026A";
    private const string MakerCoTeachProgramCode = "PRG-DEMO-MAKER";
    private const string MakerJoinableOfflineActivityCode = "ACT-DEMO-MAKER-02-03";

    private async Task SeedClassSessionExpertsAsync()
    {
        _loggerService.LogInformation("Starting seed class session experts (Maker Lab Adventures)");

        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        if (expert001 == null)
        {
            _loggerService.LogWarning("EXP-001 missing. Skipping class session expert seed.");
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == MakerCoTeachProgramCode && !p.IsDeleted);
        if (program == null)
        {
            _loggerService.LogWarning(
                "{ProgramCode} missing. Skipping class session expert seed.",
                MakerCoTeachProgramCode);
            return;
        }

        await EnsureExpertOnProgramBoardAsync(expert001, program.Id, "Maker Co-Teach Advisor");

        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == MakerCoTeachClassCode && !c.IsDeleted);
        if (classEntity == null)
        {
            _loggerService.LogWarning(
                "{ClassCode} missing. Skipping class session expert seed.",
                MakerCoTeachClassCode);
            return;
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
                "No Offline sessions on {ClassCode}. Skipping class session expert seed.",
                MakerCoTeachClassCode);
            return;
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

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed class session experts — {Count} Accepted co-teach row(s) for EXP-001 on {ClassCode} (STD-010 / MNT-004).",
            seeded,
            MakerCoTeachClassCode);
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
        var active = await _unitOfWork.ClassSessionExperts.FirstOrDefaultAsync(
            e => e.ClassSessionId == session.Id
                 && !e.IsDeleted
                 && (e.Status == ClassSessionExpertStatus.Invited
                     || e.Status == ClassSessionExpertStatus.Accepted));
        if (active != null && active.ExpertId != expertId)
        {
            _loggerService.LogWarning(
                "Session {SessionId} already has another expert. Skipping co-teach seed for that slot.",
                session.Id);
            return false;
        }

        if (active == null)
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

        if (active.Status != ClassSessionExpertStatus.Accepted)
        {
            active.Status = ClassSessionExpertStatus.Accepted;
            await _unitOfWork.ClassSessionExperts.Update(active);
        }

        return true;
    }
}
