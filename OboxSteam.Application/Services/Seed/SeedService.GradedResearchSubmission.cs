using System.Text;
using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private const string GradedCapstoneSubmissionCode = "SUB-RML0303B";
    private const string GradedCapstoneAssignmentCode = "ASG-ROBOTICS-03-03";
    private const string GradedCapstoneMilestoneCode = "RML-ROBOTICS-03-03";
    private const string GradedCapstoneStudentCode = "STD-009";
    private const string GradedCapstoneSeedFileName = "ASG-ROBOTICS-03-03-std009-capstone.pdf";

    private const string DesignBriefSubmissionCode = "SUB-RML0301B";
    private const string DesignBriefSeedFileName = "ASG-ROBOTICS-03-01-std002-design-brief.pdf";

    /// <summary>
    /// Ensures research FileUpload seeds have real S3 files under <c>Seed/Submission/</c>:
    /// Capstone Graded (<c>SUB-RML0303B</c>) for STD-009 and Design Brief PDF (<c>SUB-RML0301B</c>).
    /// Backfills <c>ResearchMilestoneId</c> on every research-assignment submission so cohort
    /// safety-net / elapsed-window rows do not break research GetSubmission (HTTP 400).
    /// </summary>
    private async Task SeedGradedCapstoneSubmissionForUiAsync()
    {
        _loggerService.LogInformation("Starting seed research FileUpload submissions with S3 files");

        await BackfillResearchMilestoneIdsOnSubmissionsAsync();
        await SoftRemoveOrphanSubmissionsOnAllResearchAssignmentsAsync();
        await SeedGradedCapstoneWithFileAsync();
        await EnsureDesignBriefReturnedForRevisionAsync();

        _loggerService.LogInformation("Finished seed research FileUpload submissions with S3 files");
    }

    /// <summary>
    /// Links any live submission on a research milestone assignment that is missing
    /// <see cref="Submission.ResearchMilestoneId"/> (safety-net / elapsed-window leftovers).
    /// </summary>
    private async Task BackfillResearchMilestoneIdsOnSubmissionsAsync()
    {
        var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(rm => !rm.IsDeleted);
        if (milestones.Count == 0)
        {
            return;
        }

        var milestoneIdByAssignmentId = milestones
            .GroupBy(rm => rm.AssignmentId)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var assignmentIds = milestoneIdByAssignmentId.Keys.ToList();
        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => assignmentIds.Contains(s.AssignmentId)
                 && !s.IsDeleted
                 && s.ResearchMilestoneId == null);
        if (submissions.Count == 0)
        {
            _loggerService.LogInformation("No research submissions needed ResearchMilestoneId backfill.");
            return;
        }

        var updated = 0;
        foreach (var submission in submissions)
        {
            if (!milestoneIdByAssignmentId.TryGetValue(submission.AssignmentId, out var milestoneId))
            {
                continue;
            }

            submission.ResearchMilestoneId = milestoneId;
            submission.UpdatedAt = _seedNow;
            submission.UpdatedBy = Guid.Empty;
            await _unitOfWork.Submissions.Update(submission);
            updated++;
        }

        if (updated > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Backfilled ResearchMilestoneId on {Count} research submission(s).",
            updated);
    }

    private async Task SeedGradedCapstoneWithFileAsync()
    {
        if (await SubmissionCodeExistsAsync(GradedCapstoneSubmissionCode))
        {
            await EnsureSubmissionHasSeedFileAsync(
                GradedCapstoneSubmissionCode,
                GradedCapstoneSeedFileName,
                "OboxSTEAM Seed Capstone Deliverable");
            return;
        }

        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == GradedCapstoneStudentCode);
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-002");
        var milestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == GradedCapstoneMilestoneCode && !rm.IsDeleted);
        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-03" && !m.IsDeleted);

        if (student == null || mentor == null || milestone == null || moduleRobotics3 == null)
        {
            _loggerService.LogWarning(
                "Missing prerequisites for graded Capstone seed (STD-009 / MNT-002 / milestone / module). Skipping.");
            return;
        }

        var assignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == GradedCapstoneAssignmentCode && !a.IsDeleted)
            ?? await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);

        if (assignment == null || assignment.IsDeleted)
        {
            _loggerService.LogWarning(
                "Assignment {Code} not found. Skipping graded Capstone seed.",
                GradedCapstoneAssignmentCode);
            return;
        }

        var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == moduleRobotics3.Id
                  && !me.IsDeleted);
        if (enrollment == null)
        {
            _loggerService.LogWarning(
                "STD-009 has no MOD-ROBOTICS-03 enrollment. Skipping graded Capstone seed.");
            return;
        }

        // Dashboard rich seed previously attached SUB-DASHR* to Capstone without milestone/file
        // and blocked this seed via the unique student+assignment row check.
        await SoftRemoveOrphanDashboardSubmissionsOnAssignmentAsync(assignment.Id);

        var fileUrl = await UploadSeedSubmissionPdfAsync(
            GradedCapstoneSeedFileName,
            "OboxSTEAM Seed Capstone Deliverable");
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            _loggerService.LogWarning(
                "Skipping graded Capstone seed — Seed/Submission PDF upload failed with no fallback.");
            return;
        }

        var seedTime = AtDays(-95);

        await _unitOfWork.Submissions.AddAsync(new Submission
        {
            Id = Guid.NewGuid(),
            Code = GradedCapstoneSubmissionCode,
            AssignmentId = assignment.Id,
            StudentId = student.Id,
            ModuleEnrollmentId = enrollment.Id,
            ResearchMilestoneId = milestone.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            ContentText = "Seeded Capstone deck for mentor UI file-preview testing.",
            FileUrl = fileUrl,
            AssignedGrade = 88m,
            MentorFeedback = "Solid structure and clear demo notes. Seeded as Graded for FE testing.",
            VerifiedBy = mentor.Id,
            SubmittedAt = seedTime.AddDays(-2),
            GradedAt = seedTime.AddDays(-1),
            CreatedAt = seedTime.AddDays(-3),
            CreatedBy = student.Id,
            UpdatedAt = seedTime.AddDays(-1),
            UpdatedBy = mentor.Id,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed graded Capstone submission {Code} with FileUrl.",
            GradedCapstoneSubmissionCode);
    }

    private async Task SoftRemoveOrphanSubmissionsOnAllResearchAssignmentsAsync()
    {
        var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(rm => !rm.IsDeleted);
        if (milestones.Count == 0)
        {
            return;
        }

        foreach (var assignmentId in milestones.Select(m => m.AssignmentId).Distinct())
        {
            await SoftRemoveOrphanDashboardSubmissionsOnAssignmentAsync(assignmentId);
        }
    }

    private async Task SoftRemoveOrphanDashboardSubmissionsOnAssignmentAsync(Guid assignmentId)
    {
        var orphans = await _unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId
                 && !s.IsDeleted
                 && s.ResearchMilestoneId == null);

        foreach (var orphan in orphans)
        {
            _loggerService.LogInformation(
                "Soft-removing orphan dashboard submission {Code} on research assignment {AssignmentId}.",
                orphan.Code,
                assignmentId);
            await _unitOfWork.Submissions.SoftRemove(orphan);
        }

        if (orphans.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Ensures Design Brief <c>SUB-RML0301B</c> exists as ReturnedForRevision for STD-002
    /// with an openable Seed/Submission PDF (after safety-net may have wiped unlinked rows).
    /// </summary>
    private async Task EnsureDesignBriefReturnedForRevisionAsync()
    {
        var milestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-ROBOTICS-03-01" && !rm.IsDeleted);
        if (milestone == null)
        {
            _loggerService.LogWarning("Design Brief milestone not found; skipping STD-002 fixture.");
            return;
        }

        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002" && !u.IsDeleted);
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001" && !u.IsDeleted);
        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-03" && !m.IsDeleted);
        if (student == null || mentor == null || moduleRobotics3 == null)
        {
            _loggerService.LogWarning("STD-002 / MNT-001 / MOD-ROBOTICS-03 missing; skipping Design Brief fixture.");
            return;
        }

        if (!await StudentClassHasStartedModuleAsync(student.Id, moduleRobotics3.Id))
        {
            _loggerService.LogInformation(
                "STD-002 has not started MOD-ROBOTICS-03 with their class. Skipping Design Brief fixture.");
            return;
        }

        var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == moduleRobotics3.Id
                  && !me.IsDeleted);
        if (enrollment == null)
        {
            _loggerService.LogWarning("STD-002 has no MOD-ROBOTICS-03 enrollment. Skipping Design Brief fixture.");
            return;
        }

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            _loggerService.LogWarning("Design Brief assignment missing. Skipping STD-002 fixture.");
            return;
        }

        var existingSubmission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.Code == DesignBriefSubmissionCode && !s.IsDeleted);
        var fileUrl = await UploadSeedSubmissionPdfAsync(
            DesignBriefSeedFileName,
            "OboxSTEAM Seed Design Brief",
            existingSubmission?.FileUrl);
        var seedTime = _seedNow;

        var submission = existingSubmission;
        if (submission == null)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                _loggerService.LogWarning(
                    "Skipping Design Brief fixture — Seed/Submission PDF upload failed with no fallback.");
                return;
            }

            // Soft-remove any other leftover on this student+assignment so the fixture is unique.
            var leftovers = await _unitOfWork.Submissions.GetAllAsync(
                s => s.StudentId == student.Id
                     && s.AssignmentId == assignment.Id
                     && !s.IsDeleted);
            foreach (var leftover in leftovers)
            {
                await _unitOfWork.Submissions.SoftRemove(leftover);
            }

            await _unitOfWork.Submissions.AddAsync(new Submission
            {
                Id = Guid.NewGuid(),
                Code = DesignBriefSubmissionCode,
                AssignmentId = assignment.Id,
                StudentId = student.Id,
                ModuleEnrollmentId = enrollment.Id,
                ResearchMilestoneId = milestone.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.ReturnedForRevision,
                ContentText = "Initial design draft with motor placement notes.",
                FileUrl = fileUrl,
                MentorFeedback = "Please add sensor placement diagrams and a parts list before resubmitting.",
                SubmittedAt = seedTime.AddDays(-3),
                ExpiresAt = seedTime.AddDays(14),
                CreatedAt = seedTime.AddDays(-5),
                CreatedBy = mentor.Id,
                UpdatedAt = seedTime.AddDays(-2),
                UpdatedBy = mentor.Id,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Recreated Design Brief submission {Code} for STD-002 as ReturnedForRevision.",
                DesignBriefSubmissionCode);
            return;
        }

        submission.ResearchMilestoneId ??= milestone.Id;
        submission.ModuleEnrollmentId ??= enrollment.Id;
        submission.Status = SubmissionStatus.ReturnedForRevision;
        submission.AssignedGrade = null;
        submission.MentorFeedback =
            "Please add sensor placement diagrams and a parts list before resubmitting.";
        submission.ContentText ??= "Initial design draft with motor placement notes.";
        submission.ExpiresAt = seedTime.AddDays(14);
        submission.UpdatedAt = seedTime;
        submission.UpdatedBy = mentor.Id;
        if (!string.IsNullOrWhiteSpace(fileUrl)
            && (string.IsNullOrWhiteSpace(submission.FileUrl)
                || !submission.FileUrl.Contains(
                    $"{SeedS3Folder}/Submission/{DesignBriefSeedFileName}",
                    StringComparison.OrdinalIgnoreCase)))
        {
            submission.FileUrl = fileUrl;
        }

        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Ensured Design Brief submission {Code} is ReturnedForRevision with ResearchMilestoneId.",
            DesignBriefSubmissionCode);
    }

    private async Task EnsureSubmissionHasSeedFileAsync(
        string submissionCode,
        string fileName,
        string pdfTitle)
    {
        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.Code == submissionCode && !s.IsDeleted);
        if (submission == null)
        {
            return;
        }

        var expectedKeyFragment = $"{SeedS3Folder}/Submission/{fileName}";
        if (!string.IsNullOrWhiteSpace(submission.FileUrl)
            && submission.FileUrl.Contains(expectedKeyFragment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousUrl = submission.FileUrl;
        var fileUrl = await UploadSeedSubmissionPdfAsync(fileName, pdfTitle, previousUrl);
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return;
        }

        submission.FileUrl = fileUrl;
        submission.UpdatedAt = _seedNow;
        submission.UpdatedBy = Guid.Empty;
        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Backfilled openable Seed/Submission file on {Code}.",
            submissionCode);
    }

    /// <summary>
    /// Uploads a seed PDF under <c>Seed/Submission/</c>. On failure, keeps
    /// <paramref name="fallbackFileUrl"/> when present so the UI does not get a 404 placeholder.
    /// </summary>
    private async Task<string?> UploadSeedSubmissionPdfAsync(
        string fileName,
        string pdfTitle,
        string? fallbackFileUrl = null)
    {
        var folder = $"{SeedS3Folder}/Submission";
        var s3Key = $"{folder}/{fileName}";
        try
        {
            await using (var pdfStream = new MemoryStream(BuildSeedSubmissionPdfBytes(pdfTitle)))
            {
                await _blobService.UploadFileAsync(fileName, pdfStream, folder);
            }

            return await _blobService.GetPreviewUrlAsync(s3Key);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(fallbackFileUrl))
            {
                _loggerService.LogWarning(
                    ex,
                    "Seed PDF upload failed for {FileName}; keeping existing FileUrl.",
                    fileName);
                return fallbackFileUrl;
            }

            _loggerService.LogWarning(
                ex,
                "Seed PDF upload failed for {FileName}; no fallback FileUrl available.",
                fileName);
            return null;
        }
    }

    /// <summary>Minimal one-page PDF so seed does not depend on QuestPDF in Application.</summary>
    private static byte[] BuildSeedSubmissionPdfBytes(string title)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "OboxSTEAM Seed Submission" : title.Trim();
        // PDF string literals cannot contain unbalanced parentheses without escaping.
        safeTitle = safeTitle.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var contentStream = $"BT /F1 18 Tf 72 720 Td ({safeTitle}) Tj ET";
        var objects = new[]
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n",
            "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n",
            "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            + "/Resources<< /Font<< /F1 5 0 R >> >> /Contents 4 0 R >>endobj\n",
            $"4 0 obj<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>stream\n{contentStream}\nendstream\nendobj\n",
            "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n",
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new int[objects.Length + 1];
        offsets[0] = 0;

        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i + 1] = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.Append(objects[i]);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");
        for (var i = 1; i <= objects.Length; i++)
        {
            builder.Append($"{offsets[i]:D10} 00000 n \n");
        }

        builder.Append($"trailer<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        builder.Append($"startxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
