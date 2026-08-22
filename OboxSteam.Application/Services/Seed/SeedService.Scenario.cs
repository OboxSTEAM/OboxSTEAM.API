using OboxSteam.Application.Validation;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private const string RoboticsCurrentClassCode = "CLS-ROBOTICS-CURRENT";
    private const string RoboticsPastClassCode = "CLS-ROBOTICS-PAST";
    private const string RoboticsOpenClassCode = "CLS-ROBOTICS-OPEN";
    private const string IotCurrentClassCode = "CLS-IOT-CURRENT";
    private const string WebDevPastClassCode = "CLS-WEBDEV-PAST";
    private const string GameDevOpenClassCode = "CLS-GAMEDEV-OPEN";
    private const string WebDevUnassignedClassCode = "CLS-WEBDEV-OPEN";
    private const string IotUnassignedClassCode = "CLS-IOT-OPEN";
    private const string PythonUnassignedClassCode = "CLS-PYBASIC-OPEN";

    private static readonly string[] RoboticsCurrentStudentCodes =
    [
        "STD-001", "STD-002", "STD-005", "STD-008",
        "STD-022", "STD-023", "STD-024", "STD-025",
    ];

    private static readonly string[] RoboticsPastStudentCodes =
    [
        "STD-009", "STD-010", "STD-011", "STD-012",
    ];

    private static readonly string[] RoboticsOpenStudentCodes =
    [
        "STD-019", "STD-020",
    ];

    private static readonly string[] IotCurrentStudentCodes =
    [
        "STD-004", "STD-013", "STD-014", "STD-015",
    ];

    private static readonly string[] WebDevPastStudentCodes =
    [
        "STD-001", "STD-003", "STD-016", "STD-017",
    ];

    private static readonly string[] GameDevPendingStudentCodes =
    [
        "STD-006", "STD-018",
    ];

    private static readonly string[] GameDevJustEnrolledStudentCodes =
    [
        "STD-021",
    ];

    private static readonly string[] AiDroppedStudentCodes =
    [
        "STD-007",
    ];

    private static readonly SeedTimeline.WeekdaySlot[] RoboticsTueThuMorning =
    [
        new(DayOfWeek.Tuesday, 9, 0, 150),
        new(DayOfWeek.Thursday, 9, 0, 150),
    ];

    /// <summary>Two weekly slots — mirrors typical Generate DaysOfWeek count (2 buổi/tuần).</summary>
    private static readonly SeedTimeline.WeekdaySlot[] RoboticsMonWedAfternoon =
    [
        new(DayOfWeek.Monday, 14, 0, 150),
        new(DayOfWeek.Wednesday, 14, 0, 150),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] IotWedFriEvening =
    [
        new(DayOfWeek.Wednesday, 18, 0, 150),
        new(DayOfWeek.Friday, 18, 0, 150),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] WebDevSatSunMorning =
    [
        new(DayOfWeek.Saturday, 9, 0, 150),
        new(DayOfWeek.Sunday, 9, 0, 150),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] GameDevFriSatAfternoon =
    [
        new(DayOfWeek.Friday, 15, 0, 180),
        new(DayOfWeek.Saturday, 15, 0, 180),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] WebDevTueThuEvening =
    [
        new(DayOfWeek.Tuesday, 18, 0, 150),
        new(DayOfWeek.Thursday, 18, 0, 150),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] IotMonWedEvening =
    [
        new(DayOfWeek.Monday, 18, 0, 150),
        new(DayOfWeek.Wednesday, 18, 0, 150),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] PythonTueThuEvening =
    [
        new(DayOfWeek.Tuesday, 18, 0, 150),
        new(DayOfWeek.Thursday, 18, 0, 150),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] DemoSatSunMorning =
    [
        new(DayOfWeek.Saturday, 9, 0, 120),
        new(DayOfWeek.Sunday, 9, 0, 120),
    ];

    private sealed record AcademicYearClassDefinition(
        string Code,
        string Name,
        string ProgramCode,
        string? MentorCode,
        ClassStatus Status,
        int StartDaysOffset,
        int EndDaysOffset,
        int MaxCapacity,
        string ScheduleSummary,
        SeedTimeline.WeekdaySlot[] WeeklySlots,
        string[] SkillCodes);

    private static AcademicYearClassDefinition[] GetAcademicYearClassDefinitions() =>
    [
        new(
            RoboticsCurrentClassCode,
            "Introduction to Robotics — Current Cohort",
            "PRG-ROBOTICS",
            "MNT-001",
            ClassStatus.InProgress,
            -42,
            42,
            12,
            "Tuesday & Thursday 09:00-11:30",
            RoboticsTueThuMorning,
            ["SKL-TECH-ROBOTICS-IOT", "SKL-TECH-PROG-PYTHON"]),
        new(
            RoboticsPastClassCode,
            "Introduction to Robotics — Completed Cohort",
            "PRG-ROBOTICS",
            "MNT-002",
            ClassStatus.Completed,
            -240,
            -90,
            8,
            "Tuesday & Thursday 09:00-11:30",
            RoboticsTueThuMorning,
            ["SKL-TECH-ROBOTICS-IOT", "SKL-TECH-PROG-PYTHON"]),
        new(
            RoboticsOpenClassCode,
            "Introduction to Robotics — Upcoming Cohort",
            "PRG-ROBOTICS",
            "MNT-003",
            ClassStatus.Open,
            14,
            98,
            10,
            "Monday & Wednesday 14:00-16:30",
            RoboticsMonWedAfternoon,
            ["SKL-TECH-ROBOTICS-IOT", "SKL-ENG-PROTOTYPE"]),
        new(
            IotCurrentClassCode,
            "IoT Fundamentals — Current Cohort",
            "PRG-IOT",
            "MNT-002",
            ClassStatus.InProgress,
            -28,
            56,
            8,
            "Wednesday & Friday 18:00-20:30",
            IotWedFriEvening,
            ["SKL-TECH-ROBOTICS-IOT", "SKL-ENG-SYSTEMS"]),
        new(
            WebDevPastClassCode,
            "Web Development — Completed Cohort",
            "PRG-WEBDEV",
            "MNT-003",
            ClassStatus.Completed,
            -150,
            -30,
            8,
            "Saturday & Sunday 09:00-11:30",
            WebDevSatSunMorning,
            ["SKL-TECH-PROG-JS", "SKL-ART-UXUI"]),
        new(
            GameDevOpenClassCode,
            "Game Design — Upcoming Cohort",
            "PRG-GAMEDEV",
            "MNT-004",
            ClassStatus.Open,
            21,
            105,
            16,
            "Friday & Saturday 15:00-18:00",
            GameDevFriSatAfternoon,
            ["SKL-TECH-SOFTWARE", "SKL-ART-VISUAL"]),
        new(
            WebDevUnassignedClassCode,
            "Web Development — Awaiting Mentor",
            "PRG-WEBDEV",
            null,
            ClassStatus.ReadyForMentor,
            21,
            105,
            12,
            "Tuesday & Thursday 18:00-20:30",
            WebDevTueThuEvening,
            ["SKL-TECH-PROG-JS", "SKL-ART-UXUI"]),
        new(
            IotUnassignedClassCode,
            "IoT Fundamentals — Awaiting Mentor",
            "PRG-IOT",
            null,
            ClassStatus.ReadyForMentor,
            21,
            105,
            10,
            "Monday & Wednesday 18:00-20:30",
            IotMonWedEvening,
            ["SKL-TECH-ROBOTICS-IOT", "SKL-ENG-SYSTEMS"]),
        new(
            PythonUnassignedClassCode,
            "Python Basics — Awaiting Mentor",
            "PRG-PYBASIC",
            null,
            ClassStatus.ReadyForMentor,
            28,
            112,
            12,
            "Tuesday & Thursday 18:00-20:30",
            PythonTueThuEvening,
            ["SKL-TECH-PROG-PYTHON", "SKL-TECH-COMP-THINK"]),
    ];

    private static readonly (string ClassCode, string[] StudentCodes, ClassEnrollmentStatus Status)[]
        AcademicYearClassEnrollmentPlan =
        [
            (RoboticsCurrentClassCode, RoboticsCurrentStudentCodes, ClassEnrollmentStatus.Active),
            (RoboticsPastClassCode, RoboticsPastStudentCodes, ClassEnrollmentStatus.Completed),
            (RoboticsOpenClassCode, RoboticsOpenStudentCodes, ClassEnrollmentStatus.Active),
            (IotCurrentClassCode, IotCurrentStudentCodes, ClassEnrollmentStatus.Active),
            (WebDevPastClassCode, WebDevPastStudentCodes, ClassEnrollmentStatus.Completed),
            // Unassigned ReadyForMentor (no mentor): CLS-WEBDEV-OPEN, CLS-IOT-OPEN, CLS-PYBASIC-OPEN.
            // Open with mentor, no students yet: CLS-GAMEDEV-OPEN.
        ];

    /// <summary>
    /// Concurrent usage matching <see cref="OboxSteam.Application.Validation.ClassMentorRequestValidator"/>:
    /// assigned classes that are not Completed/Cancelled, plus Pending board requests.
    /// </summary>
    internal static Dictionary<string, int> CountSeedConcurrentMentorUsage()
    {
        var usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Add(string mentorCode)
        {
            usage[mentorCode] = usage.GetValueOrDefault(mentorCode) + 1;
        }

        foreach (var definition in GetAcademicYearClassDefinitions())
        {
            if (definition.Status is ClassStatus.Completed or ClassStatus.Cancelled
                || string.IsNullOrWhiteSpace(definition.MentorCode))
            {
                continue;
            }

            Add(definition.MentorCode);
        }

        foreach (var definition in GetDemoProgramDefinitions())
        {
            Add(definition.InProgressMentorCode);
            Add(definition.OpenMentorCode);
        }

        foreach (var request in MentorBoardRequestPlan)
        {
            if (request.Status == ClassMentorRequestStatus.Pending)
            {
                Add(request.MentorCode);
            }
        }

        return usage;
    }

    internal static IReadOnlyList<string> GetUnassignedAcademicYearClassCodes()
        => GetAcademicYearClassDefinitions()
            .Where(definition => string.IsNullOrWhiteSpace(definition.MentorCode))
            .Select(definition => definition.Code)
            .ToList();

    /// <summary>
    /// Soft cap for seed + product: Active + PendingPayment program enrollments per student.
    /// Completed/Dropped do not count. PendingPayment is forbidden once the student already
    /// holds two in-progress programs.
    /// </summary>
    internal const int MaxInProgressProgramsPerStudent =
        ProgramEnrollmentValidator.MaxInProgressProgramsPerStudent;

    /// <summary>Soft cap for seed + product: Active class enrollments per student.</summary>
    internal const int MaxActiveClassesPerStudent =
        ClassEnrollmentValidator.MaxActiveClassesPerStudent;

    /// <summary>
    /// Counts planned Active + PendingPayment program enrollments (academic + demo).
    /// </summary>
    internal static Dictionary<string, int> CountSeedInProgressProgramEnrollments()
    {
        var usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Add(string studentCode)
        {
            usage[studentCode] = usage.GetValueOrDefault(studentCode) + 1;
        }

        foreach (var code in RoboticsCurrentStudentCodes.Concat(RoboticsOpenStudentCodes))
        {
            Add(code);
        }

        foreach (var code in IotCurrentStudentCodes)
        {
            Add(code);
        }

        foreach (var code in GameDevPendingStudentCodes)
        {
            Add(code);
        }

        foreach (var code in GameDevJustEnrolledStudentCodes)
        {
            Add(code);
        }

        // CERT-TEST Active for STD-025 (paired with Robotics Active → exactly 2).
        Add("STD-025");

        foreach (var pair in GetDemoStudentCodesByProgram())
        {
            foreach (var code in pair.Value)
            {
                Add(code);
            }
        }

        return usage;
    }

    /// <summary>
    /// Counts planned Active class enrollments (academic + demo InProgress cohorts).
    /// </summary>
    internal static Dictionary<string, int> CountSeedActiveClassEnrollments()
    {
        var usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Add(string studentCode)
        {
            usage[studentCode] = usage.GetValueOrDefault(studentCode) + 1;
        }

        foreach (var plan in AcademicYearClassEnrollmentPlan)
        {
            if (plan.Status != ClassEnrollmentStatus.Active)
            {
                continue;
            }

            foreach (var code in plan.StudentCodes)
            {
                Add(code);
            }
        }

        foreach (var pair in GetDemoStudentCodesByProgram())
        {
            foreach (var code in pair.Value)
            {
                Add(code);
            }
        }

        return usage;
    }
}
