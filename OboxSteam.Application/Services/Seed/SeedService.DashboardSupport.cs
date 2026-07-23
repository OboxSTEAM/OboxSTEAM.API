using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Extra seed rows that make Manager dashboard aggregates non-trivial
/// (grading backlog, fail grades, mentor requests, attendance).
/// </summary>
public partial class SeedService
{
    private async Task SeedDashboardSupportDataAsync()
    {
        await SeedDashboardAssessmentExtrasAsync();
        await SeedClassMentorRequestsForDashboardAsync();
        await SeedSessionAttendanceForDashboardAsync();
    }

    private async Task SeedDashboardAssessmentExtrasAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard assessment extras");

        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
        var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
        var assignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(a => !a.IsDeleted);
        if (mentor == null || student1 == null || assignment == null)
        {
            _loggerService.LogWarning("Missing users/assignment for dashboard assessment seed.");
            return;
        }

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student1.Id && !me.IsDeleted);
        var now = DateTime.UtcNow;
        var created = 0;

        if (!await SubmissionCodeExistsAsync("SUB-DASH-BACKLOG-01"))
        {
            await _unitOfWork.Submissions.AddAsync(new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-DASH-BACKLOG-01",
                AssignmentId = assignment.Id,
                StudentId = student1.Id,
                ModuleEnrollmentId = moduleEnrollment?.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.TurnedIn,
                ContentText = "Dashboard seed — waiting for mentor grade (backlog > 48h).",
                SubmittedAt = now.AddDays(-5),
                CreatedAt = now.AddDays(-6),
                CreatedBy = student1.Id,
                IsDeleted = false
            });
            created++;
        }

        if (!await SubmissionCodeExistsAsync("SUB-DASH-BACKLOG-02"))
        {
            await _unitOfWork.Submissions.AddAsync(new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-DASH-BACKLOG-02",
                AssignmentId = assignment.Id,
                StudentId = student2?.Id ?? student1.Id,
                ModuleEnrollmentId = moduleEnrollment?.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.Pending,
                ContentText = "Dashboard seed — pending submission older than 48h.",
                SubmittedAt = now.AddDays(-4),
                CreatedAt = now.AddDays(-4),
                CreatedBy = student2?.Id ?? student1.Id,
                IsDeleted = false
            });
            created++;
        }

        if (!await SubmissionCodeExistsAsync("SUB-DASH-FAIL-01"))
        {
            await _unitOfWork.Submissions.AddAsync(new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-DASH-FAIL-01",
                AssignmentId = assignment.Id,
                StudentId = student2?.Id ?? student1.Id,
                ModuleEnrollmentId = moduleEnrollment?.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.Graded,
                ContentText = "Dashboard seed — graded below PassScore.",
                AssignedGrade = Math.Max(0, assignment.PassScore - 15),
                MentorFeedback = "Needs revision before passing.",
                VerifiedBy = mentor.Id,
                SubmittedAt = now.AddDays(-10),
                GradedAt = now.AddDays(-8),
                CreatedAt = now.AddDays(-11),
                CreatedBy = student2?.Id ?? student1.Id,
                IsDeleted = false
            });
            created++;
        }

        if (!await SubmissionCodeExistsAsync("SUB-DASH-PASS-01"))
        {
            await _unitOfWork.Submissions.AddAsync(new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-DASH-PASS-01",
                AssignmentId = assignment.Id,
                StudentId = student1.Id,
                ModuleEnrollmentId = moduleEnrollment?.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.Graded,
                ContentText = "Dashboard seed — graded above PassScore.",
                AssignedGrade = assignment.PassScore + 20,
                MentorFeedback = "Solid work.",
                VerifiedBy = mentor.Id,
                SubmittedAt = now.AddDays(-12),
                GradedAt = now.AddDays(-11),
                CreatedAt = now.AddDays(-13),
                CreatedBy = student1.Id,
                IsDeleted = false
            });
            created++;
        }

        if (created > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation("Finished seed dashboard assessment extras — {Count} submission(s).", created);
    }

    private async Task SeedClassMentorRequestsForDashboardAsync()
    {
        _loggerService.LogInformation("Starting seed class mentor requests for dashboard");

        var existing = await _unitOfWork.ClassMentorRequests.FirstOrDefaultAsync(r => !r.IsDeleted);
        if (existing != null)
        {
            _loggerService.LogInformation("Class mentor requests already exist, skipping");
            return;
        }

        var openUnassigned = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => !c.IsDeleted && c.Status == ClassStatus.Open && c.MentorId == null);
        var draftClass = openUnassigned
            ?? await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => !c.IsDeleted && c.Status == ClassStatus.Draft);
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-003");
        var mentor2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-004");

        // Prefer an open class that may already have a mentor — still seed Pending requests
        // against Draft robotics class when needed.
        var targetClass = draftClass
            ?? await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == "CLS-ROBOTICS-2026D");

        if (targetClass == null || mentor == null)
        {
            _loggerService.LogWarning("No suitable class/mentor for mentor-request seed.");
            return;
        }

        var now = DateTime.UtcNow;
        var requests = new List<ClassMentorRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                MentorId = mentor.Id,
                Status = ClassMentorRequestStatus.Pending,
                Message = "Available for this cohort — dashboard seed request.",
                CreatedAt = now.AddDays(-2),
                CreatedBy = mentor.Id,
                IsDeleted = false
            }
        };

        if (mentor2 != null)
        {
            requests.Add(new ClassMentorRequest
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                MentorId = mentor2.Id,
                Status = ClassMentorRequestStatus.Pending,
                Message = "Second pending request for utilization metrics.",
                CreatedAt = now.AddDays(-1),
                CreatedBy = mentor2.Id,
                IsDeleted = false
            });
        }

        await _unitOfWork.ClassMentorRequests.AddRangeAsync(requests);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed class mentor requests — {Count} request(s).", requests.Count);
    }

    private async Task SeedSessionAttendanceForDashboardAsync()
    {
        _loggerService.LogInformation("Starting seed session attendance for dashboard");

        var existing = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(a => !a.IsDeleted);
        if (existing != null)
        {
            _loggerService.LogInformation("Session attendance already exists, skipping");
            return;
        }

        var sessions = (await _unitOfWork.ClassSessions.GetAllAsync(
                cs => !cs.IsDeleted && cs.RequiresAttendance))
            .OrderByDescending(cs => cs.StartTime)
            .Take(8)
            .ToList();

        if (sessions.Count == 0)
        {
            _loggerService.LogWarning("No class sessions found for attendance seed.");
            return;
        }

        var students = (await _unitOfWork.Users.GetAllAsync(
                u => !u.IsDeleted && u.Role == RoleType.Student))
            .Take(5)
            .ToList();

        if (students.Count == 0)
        {
            _loggerService.LogWarning("No students found for attendance seed.");
            return;
        }

        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(me => !me.IsDeleted))
            .ToList();
        var statuses = new[]
        {
            AttendanceStatus.Present,
            AttendanceStatus.Present,
            AttendanceStatus.Late,
            AttendanceStatus.Absent,
            AttendanceStatus.Excused
        };

        var now = DateTime.UtcNow;
        var rows = new List<SessionAttendance>();
        var statusIndex = 0;

        foreach (var session in sessions)
        {
            // Spread session start times across the last ~60 days for trend charts.
            if (session.StartTime > now || session.StartTime < now.AddDays(-90))
            {
                session.StartTime = now.AddDays(-(rows.Count % 45) - 1).AddHours(9);
                session.EndTime = session.StartTime.AddHours(2);
                await _unitOfWork.ClassSessions.Update(session);
            }

            foreach (var student in students)
            {
                var moduleEnrollment = moduleEnrollments.FirstOrDefault(me => me.StudentId == student.Id)
                                       ?? moduleEnrollments.FirstOrDefault();
                if (moduleEnrollment == null)
                {
                    continue;
                }

                var status = statuses[statusIndex % statuses.Length];
                statusIndex++;

                rows.Add(new SessionAttendance
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = session.Id,
                    StudentId = student.Id,
                    ModuleEnrollmentId = moduleEnrollment.Id,
                    Status = status,
                    CheckedInAt = status is AttendanceStatus.Present or AttendanceStatus.Late
                        ? session.StartTime.AddMinutes(5)
                        : null,
                    CreatedAt = session.StartTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }
        }

        if (rows.Count == 0)
        {
            _loggerService.LogWarning("No attendance rows generated.");
            return;
        }

        await _unitOfWork.SessionAttendances.AddRangeAsync(rows);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed session attendance — {Count} row(s).", rows.Count);
    }
}
