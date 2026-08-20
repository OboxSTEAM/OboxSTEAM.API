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
    /// </summary>
    private async Task SeedGradedCapstoneSubmissionForUiAsync()
    {
        _loggerService.LogInformation("Starting seed research FileUpload submissions with S3 files");

        await SeedGradedCapstoneWithFileAsync();
        await EnsureDesignBriefHasOpenableFileAsync();

        _loggerService.LogInformation("Finished seed research FileUpload submissions with S3 files");
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
    /// Upgrades existing Design Brief <c>SUB-RML0301B</c> fake URL to a real Seed/Submission PDF.
    /// </summary>
    private async Task EnsureDesignBriefHasOpenableFileAsync()
    {
        var milestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-ROBOTICS-03-01" && !rm.IsDeleted);
        if (milestone != null)
        {
            await SoftRemoveOrphanDashboardSubmissionsOnAssignmentAsync(milestone.AssignmentId);
        }

        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.Code == DesignBriefSubmissionCode && !s.IsDeleted);
        if (submission == null)
        {
            _loggerService.LogWarning(
                "Design Brief submission {Code} not found; skipping file backfill.",
                DesignBriefSubmissionCode);
            return;
        }

        await EnsureSubmissionHasSeedFileAsync(
            DesignBriefSubmissionCode,
            DesignBriefSeedFileName,
            "OboxSTEAM Seed Design Brief");
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

        submission.FileUrl = await UploadSeedSubmissionPdfAsync(fileName, pdfTitle);
        submission.UpdatedAt = _seedNow;
        submission.UpdatedBy = Guid.Empty;
        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Backfilled openable Seed/Submission file on {Code}.",
            submissionCode);
    }

    private async Task<string> UploadSeedSubmissionPdfAsync(string fileName, string pdfTitle)
    {
        var folder = $"{SeedS3Folder}/Submission";
        var s3Key = $"{folder}/{fileName}";
        await using (var pdfStream = new MemoryStream(BuildSeedSubmissionPdfBytes(pdfTitle)))
        {
            await _blobService.UploadFileAsync(fileName, pdfStream, folder);
        }

        return await _blobService.GetPreviewUrlAsync(s3Key);
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
