using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// All non-SelfPaced Introduction to Robotics (PRG-ROBOTICS) activities as cohort sessions.
    /// SelfPaced items are completed individually and do not need a <see cref="Domain.Entities.ClassSession"/>.
    /// SessionKind here must match <see cref="OboxSteam.Application.Validation.ClassSessionValidator.ResolveSessionKind"/>
    /// (Offline → FieldTrip, otherwise Lesson); CreateSeedSessionFromCurriculum derives from ActivityType.
    /// </summary>
    private static readonly (string ModuleCode, string ActivityCode, SessionKind SessionKind, string Title, string Description)[]
        IntroductionToRoboticsClassSessionTemplates =
        [
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-02", SessionKind.Lesson,
                "Introduction to Robotics",
                "Live cohort session covering robotics fundamentals."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-03", SessionKind.Lesson,
                "Components Overview Workshop",
                "Live workshop reviewing core robot components."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-02", SessionKind.Lesson,
                "Actuator Design Lecture",
                "Live lecture on actuator selection and torque planning."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-03", SessionKind.Lesson,
                "Mechanical Structures Q&A",
                "Live Q&A on mechanical structures and actuator integration."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-03-02", SessionKind.Lesson,
                "Lab Safety Briefing",
                "Live briefing on lab rules and emergency procedures."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-03-03", SessionKind.Lesson,
                "Safety Case Study Review",
                "Live review of lab safety case studies and documentation practices."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-02", SessionKind.Lesson,
                "Field Trip Preparation Briefing",
                "Live mentor briefing before the sensor exploration field trip."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-03", SessionKind.FieldTrip,
                "Sensor Exploration Field Trip",
                "On-site field trip exploring ultrasonic and infrared sensors."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-02", SessionKind.Lesson,
                "Movement Trip Preparation",
                "Live mentor session before the motor control field challenge."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-03", SessionKind.FieldTrip,
                "Motor Control Field Challenge",
                "On-site challenge to tune motor speed and direction control."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-06-02", SessionKind.Lesson,
                "Calibration Trip Preparation",
                "Live mentor session before the calibration field lab."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-06-03", SessionKind.FieldTrip,
                "Calibration Techniques Field Lab",
                "On-site lab practicing sensor and actuator calibration."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-02", SessionKind.Lesson,
                "Prototype Build Preparation",
                "Live mentor session on team roles and build-day logistics."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-03", SessionKind.FieldTrip,
                "Team Prototype Build",
                "Full-day team build session for the capstone prototype."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-08-02", SessionKind.Lesson,
                "Iteration Planning Session",
                "Live planning session before the prototype iteration lab."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-08-03", SessionKind.FieldTrip,
                "Prototype Iteration Lab",
                "On-site lab to refine and improve the team prototype."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-09-02", SessionKind.Lesson,
                "Final Testing Preparation",
                "Live mentor session before the capstone showcase."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-09-03", SessionKind.FieldTrip,
                "Final Testing & Showcase",
                "On-site final testing and capstone showcase."),
        ];
}
