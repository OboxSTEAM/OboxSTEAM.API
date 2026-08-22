namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// All non-SelfPaced Introduction to Robotics (PRG-ROBOTICS) activities as cohort sessions.
    /// SelfPaced items are completed individually and do not need a <see cref="Domain.Entities.ClassSession"/>.
    /// SessionKind is derived at create time from ActivityType via
    /// <see cref="OboxSteam.Application.Validation.ClassSessionValidator.ResolveSessionKind"/>.
    /// </summary>
    private static readonly (string ModuleCode, string ActivityCode, string Title, string Description)[]
        IntroductionToRoboticsClassSessionTemplates =
        [
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-02",
                "Introduction to Robotics",
                "Live cohort session covering robotics fundamentals."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-03",
                "Components Overview Workshop",
                "Live workshop reviewing core robot components."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-02",
                "Actuator Design Lecture",
                "Live lecture on actuator selection and torque planning."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-03",
                "Mechanical Structures Q&A",
                "Live Q&A on mechanical structures and actuator integration."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-03-02",
                "Lab Safety Briefing",
                "Live briefing on lab rules and emergency procedures."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-03-03",
                "Safety Case Study Review",
                "Live review of lab safety case studies and documentation practices."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-02",
                "Field Trip Preparation Briefing",
                "Live mentor briefing before the sensor exploration field trip."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-03",
                "Sensor Exploration Field Trip",
                "On-site field trip exploring ultrasonic and infrared sensors."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-02",
                "Movement Trip Preparation",
                "Live mentor session before the motor control field challenge."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-03",
                "Motor Control Field Challenge",
                "On-site challenge to tune motor speed and direction control."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-06-02",
                "Calibration Trip Preparation",
                "Live mentor session before the calibration field lab."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-06-03",
                "Calibration Techniques Field Lab",
                "On-site lab practicing sensor and actuator calibration."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-02",
                "Prototype Build Preparation",
                "Live mentor session on team roles and build-day logistics."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-03",
                "Team Prototype Build",
                "Full-day team build session for the capstone prototype."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-08-02",
                "Iteration Planning Session",
                "Live planning session before the prototype iteration lab."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-08-03",
                "Prototype Iteration Lab",
                "On-site lab to refine and improve the team prototype."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-09-02",
                "Final Testing Preparation",
                "Live mentor session before the capstone showcase."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-09-03",
                "Final Testing & Showcase",
                "On-site final testing and capstone showcase."),
        ];
}
