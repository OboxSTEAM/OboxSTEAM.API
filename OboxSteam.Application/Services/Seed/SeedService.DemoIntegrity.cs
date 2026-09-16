using Microsoft.Extensions.Logging;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Fails the seed run when demo-critical integrity checks fail after a fresh or re-seed.
    /// </summary>
    private async Task VerifySeedDemoIntegrityAsync()
    {
        _loggerService.LogInformation("Verifying seed demo integrity");
        var failures = new List<string>();

        await CollectOrphanResearchSubmissionFailuresAsync(failures);
        await CollectDuplicateAssignmentWindowFailuresAsync(failures);
        await CollectMissingVenueFailuresAsync(failures);
        await CollectCurrentClassRosterFailuresAsync(failures);
        await CollectCurrentClassLiveStatusFailuresAsync(failures);
        await CollectHeroFixtureFailuresAsync(failures);
        await CollectActiveEnrollmentPaymentFailuresAsync(failures);
        await CollectCompletedEnrollmentCertificateFailuresAsync(failures);

        if (failures.Count == 0)
        {
            _loggerService.LogInformation("Seed demo integrity checks passed.");
            return;
        }

        var summary = string.Join(" | ", failures.Take(12));
        if (failures.Count > 12)
        {
            summary += $" | …and {failures.Count - 12} more";
        }

        throw ErrorHelper.Internal($"Seed demo integrity failed ({failures.Count}): {summary}");
    }

    private async Task CollectOrphanResearchSubmissionFailuresAsync(List<string> failures)
    {
        var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(rm => !rm.IsDeleted);
        if (milestones.Count == 0)
        {
            return;
        }

        var assignmentIds = milestones.Select(m => m.AssignmentId).Distinct().ToList();
        var orphans = await _unitOfWork.Submissions.GetAllAsync(
            s => assignmentIds.Contains(s.AssignmentId)
                 && !s.IsDeleted
                 && s.ResearchMilestoneId == null);
        if (orphans.Count > 0)
        {
            failures.Add(
                $"{orphans.Count} research submission(s) missing ResearchMilestoneId "
                + $"(e.g. {orphans[0].Code})");
        }
    }

    private async Task CollectDuplicateAssignmentWindowFailuresAsync(List<string> failures)
    {
        var windows = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.SessionKind == SessionKind.AssignmentWindow
                  && cs.AssignmentId != null
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);
        var duplicates = windows
            .GroupBy(cs => (cs.ClassId, AssignmentId: cs.AssignmentId!.Value))
            .Where(g => g.Count() > 1)
            .Take(5)
            .ToList();
        foreach (var group in duplicates)
        {
            failures.Add(
                $"Duplicate AssignmentWindows for class {group.Key.ClassId:N} / assignment {group.Key.AssignmentId:N}");
        }
    }

    private async Task CollectMissingVenueFailuresAsync(List<string> failures)
    {
        var liveSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && (cs.SessionKind == SessionKind.LiveOnline || cs.SessionKind == SessionKind.Offline));
        var missing = liveSessions.Count(cs =>
            string.IsNullOrWhiteSpace(cs.Location)
            && string.IsNullOrWhiteSpace(cs.MeetingUrl));
        if (missing > 0)
        {
            failures.Add($"{missing} LiveOnline/Offline session(s) missing Location and MeetingUrl");
        }
    }

    private async Task CollectCurrentClassRosterFailuresAsync(List<string> failures)
    {
        var currentClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == RoboticsCurrentClassCode && !c.IsDeleted);
        if (currentClass == null)
        {
            failures.Add($"Missing class {RoboticsCurrentClassCode}");
            return;
        }

        var roster = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == currentClass.Id
                  && !ce.IsDeleted
                  && ce.Status == ClassEnrollmentStatus.Active);
        if (roster.Count == 0)
        {
            failures.Add($"{RoboticsCurrentClassCode} has empty Active roster");
        }
    }

    private async Task CollectCurrentClassLiveStatusFailuresAsync(List<string> failures)
    {
        var currentClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == RoboticsCurrentClassCode && !c.IsDeleted);
        if (currentClass == null)
        {
            return;
        }

        var lives = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == currentClass.Id
                  && !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && (cs.SessionKind == SessionKind.LiveOnline || cs.SessionKind == SessionKind.Offline));
        if (lives.Count == 0)
        {
            failures.Add($"{RoboticsCurrentClassCode} has no live sessions");
            return;
        }

        if (!lives.Any(s => s.Status == ClassSessionStatus.Completed))
        {
            failures.Add($"{RoboticsCurrentClassCode} missing Completed live session");
        }

        if (!lives.Any(s => s.Status is ClassSessionStatus.InProgress or ClassSessionStatus.Scheduled))
        {
            failures.Add($"{RoboticsCurrentClassCode} missing InProgress/Scheduled live session");
        }
    }

    private async Task CollectHeroFixtureFailuresAsync(List<string> failures)
    {
        await RequireUserAsync("STD-001", failures);
        await RequireUserAsync("STD-002", failures);
        await RequireUserAsync("STD-009", failures);
        await RequireUserAsync("STD-010", failures);
        await RequireUserAsync("MNT-001", failures);
        await RequireUserAsync("MNT-002", failures);
        await RequireUserAsync("MNG-001", failures);
        await RequireUserAsync("EXP-001", failures);
        await RequireUserAsync("EXP-002", failures);

        var designBrief = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.Code == DesignBriefSubmissionCode && !s.IsDeleted);
        if (designBrief == null)
        {
            failures.Add($"Missing Design Brief fixture {DesignBriefSubmissionCode}");
        }
        else if (designBrief.Status != SubmissionStatus.ReturnedForRevision)
        {
            failures.Add(
                $"{DesignBriefSubmissionCode} expected ReturnedForRevision, got {designBrief.Status}");
        }
        else if (!designBrief.ResearchMilestoneId.HasValue)
        {
            failures.Add($"{DesignBriefSubmissionCode} missing ResearchMilestoneId");
        }
        else if (string.IsNullOrWhiteSpace(designBrief.FileUrl))
        {
            failures.Add($"{DesignBriefSubmissionCode} missing FileUrl");
        }

        var capstone = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.Code == GradedCapstoneSubmissionCode && !s.IsDeleted);
        if (capstone == null)
        {
            failures.Add($"Missing graded Capstone fixture {GradedCapstoneSubmissionCode}");
        }
        else if (capstone.Status != SubmissionStatus.Graded)
        {
            failures.Add($"{GradedCapstoneSubmissionCode} expected Graded, got {capstone.Status}");
        }
        else if (!capstone.ResearchMilestoneId.HasValue)
        {
            failures.Add($"{GradedCapstoneSubmissionCode} missing ResearchMilestoneId");
        }

        var portfolio = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.Code == "OBOX-PF-STD001" && !p.IsDeleted);
        if (portfolio == null)
        {
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001" && !u.IsDeleted);
            if (student != null)
            {
                portfolio = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
                    p => p.StudentId == student.Id && !p.IsDeleted);
            }
        }

        if (portfolio == null)
        {
            failures.Add("Missing STD-001 portfolio fixture");
        }

        var makerClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == "CLS-DEMO-MAKER-2026A" && !c.IsDeleted);
        if (makerClass == null)
        {
            failures.Add("Missing Maker demo class CLS-DEMO-MAKER-2026A");
        }
    }

    private async Task CollectActiveEnrollmentPaymentFailuresAsync(List<string> failures)
    {
        var active = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted && pe.Status == EnrollmentStatus.Active);
        if (active.Count == 0)
        {
            return;
        }

        var payments = await _unitOfWork.Payments.GetAllAsync(
            p => !p.IsDeleted && p.Status == PaymentStatus.Success);
        var paidEnrollmentIds = payments
            .Where(p => p.ProgramEnrollmentId.HasValue)
            .Select(p => p.ProgramEnrollmentId!.Value)
            .ToHashSet();

        var pendingRequests = await _unitOfWork.PaymentRequests.GetAllAsync(
            pr => !pr.IsDeleted && pr.Status == PaymentRequestStatus.Pending);
        var pendingEnrollmentIds = pendingRequests
            .Where(pr => pr.ProgramEnrollmentId.HasValue)
            .Select(pr => pr.ProgramEnrollmentId!.Value)
            .ToHashSet();

        var unpaid = active
            .Where(pe => !paidEnrollmentIds.Contains(pe.Id) && !pendingEnrollmentIds.Contains(pe.Id))
            .Take(5)
            .ToList();
        foreach (var enrollment in unpaid)
        {
            failures.Add(
                $"Active enrollment {enrollment.Id:N} has no successful payment or pending request");
        }
    }

    private async Task CollectCompletedEnrollmentCertificateFailuresAsync(List<string> failures)
    {
        // Hero completed Robotics (STD-009) must have a program certificate for demo.
        var student009 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-009" && !u.IsDeleted);
        var robotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS" && !p.IsDeleted);
        if (student009 == null || robotics == null)
        {
            return;
        }

        var completed = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student009.Id
                  && pe.ProgramId == robotics.Id
                  && pe.Status == EnrollmentStatus.Completed
                  && !pe.IsDeleted);
        if (completed == null)
        {
            return;
        }

        var cert = await _unitOfWork.Certificates.FirstOrDefaultAsync(
            c => c.StudentId == student009.Id
                 && c.ProgramId == robotics.Id
                 && c.ModuleId == null
                 && !c.IsDeleted);
        if (cert == null)
        {
            failures.Add("STD-009 Completed Robotics enrollment missing program certificate");
        }
    }

    private async Task RequireUserAsync(string code, List<string> failures)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == code && !u.IsDeleted);
        if (user == null)
        {
            failures.Add($"Missing hero user {code}");
        }
    }

    /// <summary>
    /// Pure helper for unit tests: CURRENT class must expose Completed plus an upcoming live.
    /// </summary>
    internal static bool HasRequiredCurrentClassLiveMix(
        IEnumerable<(ClassSessionStatus Status, SessionKind Kind)> sessions)
    {
        var lives = sessions
            .Where(s => s.Kind is SessionKind.LiveOnline or SessionKind.Offline)
            .Select(s => s.Status)
            .ToList();
        if (lives.Count == 0)
        {
            return false;
        }

        return lives.Contains(ClassSessionStatus.Completed)
               && lives.Any(s => s is ClassSessionStatus.InProgress or ClassSessionStatus.Scheduled);
    }
}
