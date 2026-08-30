using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.Helpers;

/// <summary>
/// Seeds a Standard class, active class enrollment, and/or an open AssignmentWindow.
/// </summary>
internal static class ClassAssignmentWindowSeed
{
    public static ClassSession Open(
        InMemoryUnitOfWork db,
        Guid classId,
        Guid moduleId,
        Guid assignmentId,
        DateTime? start = null,
        DateTime? end = null,
        string title = "Assignment window")
    {
        var now = DateTime.UtcNow;
        var existing = db.ClassSessions.Items.FirstOrDefault(s =>
            s.ClassId == classId
            && s.AssignmentId == assignmentId
            && s.SessionKind == SessionKind.AssignmentWindow
            && !s.IsDeleted);
        if (existing != null)
        {
            return existing;
        }
        var session = new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            AssignmentId = assignmentId,
            SessionKind = SessionKind.AssignmentWindow,
            Title = title,
            StartTime = start ?? now.AddDays(-7),
            EndTime = end ?? now.AddDays(60),
            RequiresAttendance = false,
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        };
        db.ClassSessions.Seed(session);
        return session;
    }

    public static void Close(ClassSession session)
    {
        var now = DateTime.UtcNow;
        session.StartTime = now.AddDays(-7);
        session.EndTime = now.AddMinutes(-1);
    }

    public static Class ClassWithActiveEnrollment(
        InMemoryUnitOfWork db,
        Guid classId,
        Guid programId,
        Guid studentId,
        Guid? programEnrollmentId = null,
        Guid? mentorId = null)
    {
        Class cls;
        if (db.Classes.Items.Any(c => c.Id == classId))
        {
            cls = db.Classes.Items.Single(c => c.Id == classId);
        }
        else
        {
            cls = new Class
            {
                Id = classId,
                Code = "CLS-WIN",
                Name = "Cohort",
                ProgramId = programId,
                MentorId = mentorId,
                Status = ClassStatus.InProgress,
                Kind = ClassKind.Standard,
                StartDate = DateTime.UtcNow.AddDays(-14),
                EndDate = DateTime.UtcNow.AddDays(90),
                MaxCapacity = 30,
                IsDeleted = false,
            };
            db.Classes.Seed(cls);
        }

        var peId = programEnrollmentId ?? Guid.NewGuid();
        if (db.ProgramEnrollments.Items.All(p => p.Id != peId))
        {
            db.ProgramEnrollments.Seed(new ProgramEnrollment
            {
                Id = peId,
                StudentId = studentId,
                ProgramId = programId,
                Status = EnrollmentStatus.Active,
                IsDeleted = false,
            });
        }

        if (db.ClassEnrollments.Items.All(ce => ce.ClassId != classId || ce.StudentId != studentId))
        {
            db.ClassEnrollments.Seed(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                StudentId = studentId,
                ProgramEnrollmentId = peId,
                Status = ClassEnrollmentStatus.Active,
                IsDeleted = false,
            });
        }

        return cls;
    }
}
