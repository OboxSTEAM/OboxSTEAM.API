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
    private const string GradedCapstoneStudentCode = "STD-002";
    private const string GradedCapstoneTargetClassCode = "CLS-ROBOTICS-2026A";
    private const string GradedCapstoneSeedFileName = "ASG-ROBOTICS-03-03-std002-capstone.pdf";

    /// <summary>
    /// Moves STD-002 onto CLS-ROBOTICS-2026A and seeds a Graded Capstone research
    /// submission with a real PDF under <c>Seed/Submission/</c> so mentor UI can list
    /// and open the file after a full reseed.
    /// </summary>
    private async Task SeedGradedCapstoneSubmissionForUiAsync()
    {
        _loggerService.LogInformation("Starting seed graded Capstone submission for UI testing");

        await EnsureStudentOnRoboticsClassAsync(GradedCapstoneStudentCode, GradedCapstoneTargetClassCode);

        if (await SubmissionCodeExistsAsync(GradedCapstoneSubmissionCode))
        {
            _loggerService.LogInformation(
                "Graded Capstone submission {Code} already exists; skipping.",
                GradedCapstoneSubmissionCode);
            return;
        }

        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == GradedCapstoneStudentCode);
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var milestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == GradedCapstoneMilestoneCode && !rm.IsDeleted);
        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-03" && !m.IsDeleted);

        if (student == null || mentor == null || milestone == null || moduleRobotics3 == null)
        {
            _loggerService.LogWarning(
                "Missing prerequisites for graded Capstone seed (STD-002 / MNT-001 / milestone / module). Skipping.");
            return;
        }

        var assignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == GradedCapstoneAssignmentCode && !a.IsDeleted);
        if (assignment == null)
        {
            assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        }

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
                "STD-002 has no MOD-ROBOTICS-03 enrollment. Skipping graded Capstone seed.");
            return;
        }

        var existingForStudent = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.AssignmentId == assignment.Id
                 && s.StudentId == student.Id
                 && !s.IsDeleted);
        if (existingForStudent != null)
        {
            _loggerService.LogInformation(
                "STD-002 already has a Capstone submission ({Code}); skipping graded Capstone seed.",
                existingForStudent.Code);
            return;
        }

        var folder = $"{SeedS3Folder}/Submission";
        var s3Key = $"{folder}/{GradedCapstoneSeedFileName}";
        await using (var pdfStream = new MemoryStream(BuildSeedCapstonePdfBytes()))
        {
            await _blobService.UploadFileAsync(GradedCapstoneSeedFileName, pdfStream, folder);
        }

        var fileUrl = await _blobService.GetPreviewUrlAsync(s3Key);
        var seedTime = DateTime.UtcNow;

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
            "Finished seed graded Capstone submission {Code} with S3 key {S3Key}.",
            GradedCapstoneSubmissionCode,
            s3Key);
    }

    private async Task EnsureStudentOnRoboticsClassAsync(string studentCode, string targetClassCode)
    {
        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
        var targetClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == targetClassCode && !c.IsDeleted);
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");

        if (student == null || targetClass == null || program == null)
        {
            _loggerService.LogWarning(
                "Cannot place {StudentCode} on {ClassCode}: student/class/program missing.",
                studentCode,
                targetClassCode);
            return;
        }

        var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student.Id
                  && pe.ProgramId == program.Id
                  && !pe.IsDeleted);
        if (programEnrollment == null)
        {
            _loggerService.LogWarning(
                "No PRG-ROBOTICS enrollment for {StudentCode}; cannot move to {ClassCode}.",
                studentCode,
                targetClassCode);
            return;
        }

        var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == programEnrollment.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (classEnrollment == null)
        {
            await _unitOfWork.ClassEnrollments.AddAsync(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                StudentId = student.Id,
                ProgramEnrollmentId = programEnrollment.Id,
                Status = ClassEnrollmentStatus.Active,
                EnrolledAt = DateTime.UtcNow.AddDays(-3),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Created Active class enrollment for {StudentCode} on {ClassCode}.",
                studentCode,
                targetClassCode);
            return;
        }

        if (classEnrollment.ClassId == targetClass.Id)
        {
            return;
        }

        var previousClass = await _unitOfWork.Classes.GetByIdAsync(classEnrollment.ClassId);
        classEnrollment.ClassId = targetClass.Id;
        classEnrollment.UpdatedAt = DateTime.UtcNow;
        classEnrollment.UpdatedBy = Guid.Empty;
        await _unitOfWork.ClassEnrollments.Update(classEnrollment);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Moved {StudentCode} Active class enrollment from {FromClass} to {ToClass}.",
            studentCode,
            previousClass?.Code ?? classEnrollment.ClassId.ToString(),
            targetClassCode);
    }

    /// <summary>Minimal one-page PDF so seed does not depend on QuestPDF in Application.</summary>
    private static byte[] BuildSeedCapstonePdfBytes()
    {
        const string contentStream = "BT /F1 18 Tf 72 720 Td (OboxSTEAM Seed Capstone Deliverable) Tj ET";
        var objects = new[]
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n",
            "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n",
            "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            + "/Resources<< /Font<< /F1 5 0 R >> >> /Contents 4 0 R >>endobj\n",
            $"4 0 obj<< /Length {contentStream.Length} >>stream\n{contentStream}\nendstream\nendobj\n",
            "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n",
        };

        var header = "%PDF-1.4\n";
        var builder = new StringBuilder();
        builder.Append(header);
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
