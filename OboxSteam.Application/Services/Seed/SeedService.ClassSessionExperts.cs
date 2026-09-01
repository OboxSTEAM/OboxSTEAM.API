using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// EXP-001 co-teach fixture on Introduction to Robotics (current cohort):
/// two Accepted Offline invitations — one Completed (empty feedback so the
/// expert can submit), one Scheduled (accepted calendar). Idempotent for re-seed.
/// </summary>
public partial class SeedService
{
    private async Task SeedClassSessionExpertsAsync()
    {
        _loggerService.LogInformation("Starting seed class session experts");

        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        if (expert001 == null)
        {
            _loggerService.LogWarning("EXP-001 missing. Skipping class session expert seed.");
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS" && !p.IsDeleted);
        if (program == null)
        {
            _loggerService.LogWarning("PRG-ROBOTICS missing. Skipping class session expert seed.");
            return;
        }

        await EnsureExpertOnProgramBoardAsync(expert001, program.Id, "Lead Robotics Advisor");

        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == RoboticsCurrentClassCode && !c.IsDeleted);
        if (classEntity == null)
        {
            _loggerService.LogWarning(
                "{ClassCode} missing. Skipping class session expert seed.",
                RoboticsCurrentClassCode);
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
                RoboticsCurrentClassCode);
            return;
        }

        var completed = offlineSessions.FirstOrDefault(s => s.Status == ClassSessionStatus.Completed);
        if (completed == null)
        {
            completed = offlineSessions[0];
            completed.Status = ClassSessionStatus.Completed;
            await _unitOfWork.ClassSessions.Update(completed);
            _loggerService.LogInformation(
                "Forced session {SessionId} to Completed so EXP-001 can test co-teach feedback.",
                completed.Id);
        }

        var scheduled = offlineSessions.FirstOrDefault(
            s => s.Id != completed.Id && s.Status == ClassSessionStatus.Scheduled);
        var second = scheduled
            ?? offlineSessions.FirstOrDefault(s => s.Id != completed.Id);

        var seeded = 0;
        if (await TryEnsureAcceptedCoTeachAsync(completed, expert001.Id))
        {
            seeded++;
        }

        if (second != null && await TryEnsureAcceptedCoTeachAsync(second, expert001.Id))
        {
            seeded++;
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed class session experts — {Count} Accepted co-teach row(s) for EXP-001 on {ClassCode}.",
            seeded,
            RoboticsCurrentClassCode);
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
