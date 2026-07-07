using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// All non-SelfPaced Introduction to Robotics (PRG-ROBOTICS) activities as cohort sessions.
    /// SelfPaced items are completed individually and do not need a <see cref="Domain.Entities.ClassSession"/>.
    /// </summary>
    private static readonly (string ModuleCode, string ActivityCode, SessionKind SessionKind, string Title, string Description)[]
        IntroductionToRoboticsClassSessionTemplates =
        [
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-02", SessionKind.LiveOnline,
                "Introduction to Robotics",
                "Live cohort session covering robotics fundamentals."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-03", SessionKind.LiveOnline,
                "Components Overview Workshop",
                "Live workshop reviewing core robot components."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-02", SessionKind.LiveOnline,
                "Actuator Design Lecture",
                "Live lecture on actuator selection and torque planning."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-03", SessionKind.LiveOnline,
                "Mechanical Structures Q&A",
                "Live Q&A on mechanical structures and actuator integration."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-03-02", SessionKind.LiveOnline,
                "Lab Safety Briefing",
                "Live briefing on lab rules and emergency procedures."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-03-03", SessionKind.LiveOnline,
                "Safety Case Study Review",
                "Live review of lab safety case studies and documentation practices."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-02", SessionKind.LiveOnline,
                "Field Trip Preparation Briefing",
                "Live mentor briefing before the sensor exploration field trip."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-03", SessionKind.Lesson,
                "Sensor Exploration Field Trip",
                "On-site field trip exploring ultrasonic and infrared sensors."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-02", SessionKind.LiveOnline,
                "Movement Trip Preparation",
                "Live mentor session before the motor control field challenge."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-03", SessionKind.Lesson,
                "Motor Control Field Challenge",
                "On-site challenge to tune motor speed and direction control."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-06-02", SessionKind.LiveOnline,
                "Calibration Trip Preparation",
                "Live mentor session before the calibration field lab."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-06-03", SessionKind.Lesson,
                "Calibration Techniques Field Lab",
                "On-site lab practicing sensor and actuator calibration."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-02", SessionKind.LiveOnline,
                "Prototype Build Preparation",
                "Live mentor session on team roles and build-day logistics."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-03", SessionKind.Lesson,
                "Team Prototype Build",
                "Full-day team build session for the capstone prototype."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-08-02", SessionKind.LiveOnline,
                "Iteration Planning Session",
                "Live planning session before the prototype iteration lab."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-08-03", SessionKind.Lesson,
                "Prototype Iteration Lab",
                "On-site lab to refine and improve the team prototype."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-09-02", SessionKind.LiveOnline,
                "Final Testing Preparation",
                "Live mentor session before the capstone showcase."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-09-03", SessionKind.Lesson,
                "Final Testing & Showcase",
                "On-site final testing and capstone showcase."),
        ];

    private static (DateTime StartTime, DateTime EndTime) ResolveIntroductionToRoboticsSessionTimes(
        Class classEntity,
        int sessionIndex,
        int sessionCount)
    {
        var durationDays = Math.Max((classEntity.EndDate.Date - classEntity.StartDate.Date).TotalDays, 1);
        var fraction = (sessionIndex + 1) / (double)(sessionCount + 1);
        var sessionDate = classEntity.StartDate.Date.AddDays(durationDays * fraction);
        var startHour = sessionIndex % 2 == 0 ? 9 : 14;
        var startTime = sessionDate.AddHours(startHour);
        var endTime = startTime.AddHours(2).AddMinutes(30);
        return (startTime, endTime);
    }
}
