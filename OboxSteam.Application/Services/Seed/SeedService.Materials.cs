using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private sealed record SeedMaterialDefinition(
        string ActivityCode,
        string Title,
        MaterialType MaterialType,
        string FileUrl,
        long? FileSizeBytes = null);

    private static IReadOnlyList<SeedMaterialDefinition> GetSeedMaterialDefinitions() =>
    [
        ..GetRoboticsSeedMaterialDefinitions(),
        new("ACT-WEBDEV-01-01", "HTML Structure Overview", MaterialType.Video,
            "https://storage.oboxsteam.com/materials/video/html-structure-overview.mp4", 41_200_000L),
        new("ACT-WEBDEV-01-03", "Responsive Layout Exercise Workbook", MaterialType.PDF,
            "https://storage.oboxsteam.com/materials/pdf/responsive-layout-workbook.pdf", 760_000L),
        new("ACT-WEBDEV-02-01", "JavaScript Variables and Types", MaterialType.Video,
            "https://storage.oboxsteam.com/materials/video/javascript-variables-types.mp4", 38_400_000L),
        new("ACT-WEBDEV-02-04", "Code Review Checklist", MaterialType.DOC,
            "https://storage.oboxsteam.com/materials/doc/webdev-code-review-checklist.docx", 128_000L),
        new("ACT-WEBDEV-03-01", "Responsive Design Brief", MaterialType.PDF,
            "https://storage.oboxsteam.com/materials/pdf/responsive-design-brief.pdf", 512_000L),
        new("ACT-STEAM-01-02", "Science Experiment Kit Guide", MaterialType.PDF,
            "https://storage.oboxsteam.com/materials/pdf/steam-science-kit-guide.pdf", 680_000L),
        new("ACT-STEAM-02-01", "Prototyping Principles", MaterialType.Video,
            "https://storage.oboxsteam.com/materials/video/prototyping-principles.mp4", 44_600_000L),
        new("ACT-STEAM-02-03", "Design Critique Worksheet", MaterialType.PDF,
            "https://storage.oboxsteam.com/materials/pdf/design-critique-worksheet.pdf", 198_000L),
        new("ACT-STEAM-02-04", "Portfolio Documentation Template", MaterialType.DOC,
            "https://storage.oboxsteam.com/materials/doc/portfolio-documentation-template.docx", 156_000L),
        new("ACT-IOT-01-01", "Microcontroller Basics", MaterialType.Video,
            "https://storage.oboxsteam.com/materials/video/microcontroller-basics.mp4", 52_300_000L),
        new("ACT-IOT-01-02", "Sensor Wiring Guide", MaterialType.Image,
            "https://storage.oboxsteam.com/materials/image/sensor-wiring-diagram.png", 2_400_000L),
        new("ACT-IOT-02-01", "MQTT Concepts Explained", MaterialType.Video,
            "https://storage.oboxsteam.com/materials/video/mqtt-concepts.mp4", 36_700_000L),
        new("ACT-IOT-02-02", "Cloud Dashboard Setup Guide", MaterialType.PDF,
            "https://storage.oboxsteam.com/materials/pdf/cloud-dashboard-setup.pdf", 945_000L),
    ];

    private async Task SeedMaterialsAsync()
    {
        _loggerService.LogInformation("Starting seed materials");

        var definitions = GetSeedMaterialDefinitions();
        var definitionByCode = definitions.ToDictionary(
            d => d.ActivityCode,
            d => d,
            StringComparer.OrdinalIgnoreCase);

        var existingMaterials = await _unitOfWork.Materials.GetAllAsync(m => !m.IsDeleted);
        var activityIdsWithMaterial = existingMaterials
            .Select(m => m.ActivityId)
            .ToHashSet();

        var selfPacedActivities = await _unitOfWork.Activities.GetAllAsync(
            a => !a.IsDeleted && a.ActivityType == ActivityType.SelfPaced);

        var seedTime = DateTime.UtcNow;
        var materialsToAdd = new List<Material>();

        foreach (var activity in selfPacedActivities)
        {
            if (activityIdsWithMaterial.Contains(activity.Id))
            {
                continue;
            }

            if (!definitionByCode.TryGetValue(activity.Code, out var definition))
            {
                _loggerService.LogWarning(
                    "No seed material definition for SelfPaced activity '{ActivityCode}'. Skipping.",
                    activity.Code);
                continue;
            }

            materialsToAdd.Add(new Material
            {
                Id = Guid.NewGuid(),
                ActivityId = activity.Id,
                Title = definition.Title,
                MaterialType = definition.MaterialType,
                FileUrl = definition.FileUrl,
                FileSizeBytes = definition.FileSizeBytes,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
        }

        if (materialsToAdd.Count == 0)
        {
            _loggerService.LogInformation("No new materials to seed.");
            return;
        }

        await _unitOfWork.Materials.AddRangeAsync(materialsToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed materials — {Count} material(s) created.",
            materialsToAdd.Count);
    }

    private static List<Activity> CreateSeedActivities(
        Dictionary<string, Course> courseByCode,
        DateTime baseDate,
        DateTime seedTime)
    {
        var activities = new List<Activity>();

        void AddActivities(string courseCode, IEnumerable<Activity> courseActivities)
        {
            if (!courseByCode.TryGetValue(courseCode, out var course))
            {
                return;
            }

            foreach (var activity in courseActivities)
            {
                activity.CourseId = course.Id;
                activity.CreatedAt = seedTime;
                activity.CreatedBy = Guid.Empty;
                activity.IsDeleted = false;
                activities.Add(activity);
            }
        }

        AddRoboticsSeedActivities(activities, courseByCode, baseDate, seedTime);

        AddActivities("CRS-WEBDEV-01", new[]
        {
            NewActivity("ACT-WEBDEV-01-01", "HTML Structure Overview", ActivityType.SelfPaced, 1,
                "Video lessons on semantic HTML and document structure.", null, null, null, null, false, false),
            NewActivity("ACT-WEBDEV-01-02", "Live CSS Layout Session", ActivityType.LiveOnline, 2,
                "Live session on flexbox and grid layouts.",
                "https://meet.google.com/webdev-css",
                baseDate.AddDays(4).AddHours(18), baseDate.AddDays(4).AddHours(20), 35, false, false),
            NewActivity("ACT-WEBDEV-01-03", "Responsive Layout Exercises", ActivityType.SelfPaced, 3,
                "Self-paced responsive layout practice exercises.", null, null, null, null, false, false),
        });

        AddActivities("CRS-WEBDEV-02", new[]
        {
            NewActivity("ACT-WEBDEV-02-01", "JavaScript Variables & Types", ActivityType.SelfPaced, 1,
                "Self-paced module on JS fundamentals.", null, null, null, null, false, false),
            NewActivity("ACT-WEBDEV-02-02", "DOM Manipulation Lab", ActivityType.Offline, 2,
                "Hands-on lab for DOM manipulation exercises.",
                "Computer Lab 202",
                baseDate.AddDays(6).AddHours(10), baseDate.AddDays(6).AddHours(12), 30, true, false),
            NewActivity("ACT-WEBDEV-02-03", "Weekend Hackathon", ActivityType.Offline, 3,
                "Build a simple interactive page in teams.",
                "Computer Lab 202",
                baseDate.AddDays(12).AddHours(9), baseDate.AddDays(12).AddHours(15), 24, true, true),
            NewActivity("ACT-WEBDEV-02-04", "Code Review Checklist", ActivityType.SelfPaced, 4,
                "Self-paced code review checklist and mentor feedback guide.", null, null, null, null, false, false),
        });

        AddActivities("CRS-STEAM-01", new[]
        {
            NewActivity("ACT-STEAM-01-01", "STEAM Lab Orientation", ActivityType.LiveOnline, 1,
                "Orientation to interdisciplinary STEAM projects.",
                "https://meet.google.com/steam-kickoff",
                baseDate.AddDays(3).AddHours(9), baseDate.AddDays(3).AddHours(10), 40, false, false),
            NewActivity("ACT-STEAM-01-02", "Science Experiment Kit", ActivityType.SelfPaced, 2,
                "Complete the at-home science experiment kit.", null, null, null, null, false, true),
            NewActivity("ACT-STEAM-01-03", "Art & Engineering Discussion", ActivityType.LiveOnline, 3,
                "Live discussion on combining art and engineering in projects.",
                "https://meet.google.com/steam-art-engineering",
                baseDate.AddDays(9).AddHours(13), baseDate.AddDays(9).AddHours(16), 16, false, true),
        });

        AddActivities("CRS-STEAM-02", new[]
        {
            NewActivity("ACT-STEAM-02-01", "Prototyping Principles", ActivityType.SelfPaced, 1,
                "Introduction to rapid prototyping methods.", null, null, null, null, false, false),
            NewActivity("ACT-STEAM-02-02", "Material Exploration Lab", ActivityType.Offline, 2,
                "Explore recycled materials and simple circuits.",
                "STEAM Studio 2",
                baseDate.AddDays(11).AddHours(10), baseDate.AddDays(11).AddHours(13), 14, true, true),
            NewActivity("ACT-STEAM-02-03", "Design Critique Worksheet", ActivityType.SelfPaced, 3,
                "Complete the peer design critique worksheet.", null, null, null, null, false, false),
            NewActivity("ACT-STEAM-02-04", "Portfolio Documentation", ActivityType.SelfPaced, 4,
                "Document your prototype with photos and a short write-up.", null, null, null, null, false, true),
        });

        AddActivities("CRS-IOT-01", new[]
        {
            NewActivity("ACT-IOT-01-01", "Microcontroller Basics", ActivityType.SelfPaced, 1,
                "Self-paced intro to Arduino and GPIO pins.", null, null, null, null, false, false),
            NewActivity("ACT-IOT-01-02", "Sensor Wiring Guide", ActivityType.SelfPaced, 2,
                "Self-paced guide for wiring temperature and humidity sensors.", null, null, null, null, false, false),
            NewActivity("ACT-IOT-01-03", "Live Q&A: Sensor Data", ActivityType.LiveOnline, 3,
                "Live Q&A on reading and interpreting sensor data.",
                "https://meet.google.com/iot-sensors",
                baseDate.AddDays(8).AddHours(14), baseDate.AddDays(8).AddHours(15), 25, false, false),
        });

        AddActivities("CRS-IOT-02", new[]
        {
            NewActivity("ACT-IOT-02-01", "MQTT Concepts", ActivityType.SelfPaced, 1,
                "Learn MQTT publish/subscribe patterns.", null, null, null, null, false, false),
            NewActivity("ACT-IOT-02-02", "Cloud Dashboard Setup Guide", ActivityType.SelfPaced, 2,
                "Self-paced guide for setting up a cloud dashboard.", null, null, null, null, false, false),
            NewActivity("ACT-IOT-02-03", "Device Deployment Lab", ActivityType.Offline, 3,
                "Deploy a device and verify cloud connectivity.",
                "Electronics Lab 302",
                baseDate.AddDays(13).AddHours(9), baseDate.AddDays(13).AddHours(13), 12, true, true),
        });

        return activities;
    }

    private static Activity NewActivity(
        string code,
        string name,
        ActivityType activityType,
        int activityOrder,
        string? description,
        string? location,
        DateTime? startTime,
        DateTime? endTime,
        int? maxCapacity,
        bool requireQrCheckin,
        bool requireMediaEvidence) => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ActivityType = activityType,
            Description = description,
            ActivityOrder = activityOrder,
            Location = location,
            StartTime = startTime,
            EndTime = endTime,
            MaxCapacity = maxCapacity,
            RequireQrCheckin = requireQrCheckin,
            RequireMediaEvidence = requireMediaEvidence,
        };

    private const string RoboticsTheoryMaterialUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/GI%C3%81O+TR%C3%8CNH+CH%E1%BB%A6+NGH%C4%A8A+X%C3%83+H%E1%BB%98I+KHOA+H%E1%BB%8CC+(Quoc+gia).pdf";

    private const string RoboticsExperientialMaterialUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/Robotics+engineers+are+in+high+demand+%E2%80%94+but+what+is+the+job+really+like+-+CNBC+International+(720p%2C+h264).mp4";

    private const string RoboticsResearchMaterialUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/Gi%C3%A1o+tr%C3%ACnh+k%E1%BB%B9+thu%E1%BA%ADt+robot+-+%C4%90%C3%A0o+V%C4%83n+Hi%E1%BB%87p.pdf";

    private static readonly string[] RoboticsClassCodes =
    [
        "CLS-ROBOTICS-2026A",
        "CLS-ROBOTICS-2026B",
        "CLS-ROBOTICS-2026C",
        "CLS-ROBOTICS-2026D",
    ];

    private static readonly HashSet<string> Mentor1RoboticsClassCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CLS-ROBOTICS-2026A",
            "CLS-ROBOTICS-2026B",
        };

    private static readonly Dictionary<int, int> Mentor1SharedSessionDayOffsets = new()
    {
        [1] = 21,
        [3] = 49,
    };

    private static void AddRoboticsCourses(
        List<Course> courses,
        Module? moduleTheory,
        Module? moduleExperiential,
        Module? moduleResearch,
        DateTime seedTime)
    {
        if (moduleTheory == null && moduleExperiential == null && moduleResearch == null)
        {
            return;
        }

        void AddCourse(Module? module, string code, string name, string description)
        {
            if (module == null)
            {
                return;
            }

            courses.Add(new Course
            {
                Id = Guid.NewGuid(),
                Code = code,
                ModuleId = module.Id,
                Name = name,
                Description = description,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        AddCourse(moduleTheory, "CRS-ROBOTICS-01", "Robot Fundamentals",
            "Core theory on robotics components, systems, and engineering mindset.");
        AddCourse(moduleTheory, "CRS-ROBOTICS-02", "Mechanics & Actuators",
            "Theory course covering mechanical structures, motors, and actuators.");
        AddCourse(moduleTheory, "CRS-ROBOTICS-03", "Safety & Lab Practice",
            "Theory course on lab safety, documentation, and responsible prototyping.");

        AddCourse(moduleExperiential, "CRS-ROBOTICS-04", "Sensor Exploration",
            "Hands-on experiential course introducing common robot sensors.");
        AddCourse(moduleExperiential, "CRS-ROBOTICS-05", "Movement Programming",
            "Experiential course on programming movement patterns and motor control.");
        AddCourse(moduleExperiential, "CRS-ROBOTICS-06", "Hands-on Calibration",
            "Experiential lab course for sensor calibration and behavior tuning.");

        AddCourse(moduleResearch, "CRS-ROBOTICS-07", "Research Design Brief",
            "Research course for planning a team robotics capstone project.");
        AddCourse(moduleResearch, "CRS-ROBOTICS-08", "Prototype Build Project",
            "Research course focused on building and iterating a robot prototype.");
        AddCourse(moduleResearch, "CRS-ROBOTICS-09", "Capstone Presentation",
            "Research course for documenting outcomes and presenting results.");
    }

    private static IReadOnlyList<SeedMaterialDefinition> GetRoboticsSeedMaterialDefinitions()
    {
        var definitions = new List<SeedMaterialDefinition>();

        void AddTheory(string activityCode, string title) =>
            definitions.Add(new(activityCode, title, MaterialType.PDF, RoboticsTheoryMaterialUrl, 4_200_000L));

        void AddExperiential(string activityCode, string title) =>
            definitions.Add(new(activityCode, title, MaterialType.Video, RoboticsExperientialMaterialUrl, 85_000_000L));

        void AddResearch(string activityCode, string title) =>
            definitions.Add(new(activityCode, title, MaterialType.PDF, RoboticsResearchMaterialUrl, 3_800_000L));

        AddTheory("ACT-ROBOTICS-01-01", "Giáo trình kỹ thuật robot - Đào Văn Hiệp");
        AddTheory("ACT-ROBOTICS-02-01", "Giáo trình kỹ thuật robot - Đào Văn Hiệp");
        AddTheory("ACT-ROBOTICS-03-01", "Giáo trình kỹ thuật robot - Đào Văn Hiệp");

        AddExperiential("ACT-ROBOTICS-04-01", "Robotics Engineers — CNBC International");
        AddExperiential("ACT-ROBOTICS-05-01", "Robotics Engineers — CNBC International");
        AddExperiential("ACT-ROBOTICS-06-01", "Robotics Engineers — CNBC International");

        AddResearch("ACT-ROBOTICS-07-01", "Giáo trình Chủ nghĩa xã hội khoa học (Quốc gia)");
        AddResearch("ACT-ROBOTICS-08-01", "Giáo trình Chủ nghĩa xã hội khoa học (Quốc gia)");
        AddResearch("ACT-ROBOTICS-09-01", "Giáo trình Chủ nghĩa xã hội khoa học (Quốc gia)");

        return definitions;
    }

    private static void AddRoboticsSeedActivities(
        List<Activity> activities,
        Dictionary<string, Course> courseByCode,
        DateTime baseDate,
        DateTime seedTime)
    {
        void AddCourseActivities(string courseCode, IEnumerable<Activity> courseActivities)
        {
            if (!courseByCode.TryGetValue(courseCode, out var course))
            {
                return;
            }

            foreach (var activity in courseActivities)
            {
                activity.CourseId = course.Id;
                activity.CreatedAt = seedTime;
                activity.CreatedBy = Guid.Empty;
                activity.IsDeleted = false;
                activities.Add(activity);
            }
        }

        AddCourseActivities("CRS-ROBOTICS-01", new[]
        {
            NewActivity("ACT-ROBOTICS-01-01", "Robot Fundamentals Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading from the robotics techniques textbook.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-01-02", "Introduction to Robotics", ActivityType.LiveOnline, 2,
                "Live online introduction to robotics systems.",
                "https://meet.google.com/robotics-theory-intro",
                baseDate.AddDays(1).AddHours(9), baseDate.AddDays(1).AddHours(11), 30, false, false),
            NewActivity("ACT-ROBOTICS-01-03", "Components Overview Workshop", ActivityType.LiveOnline, 3,
                "Live workshop reviewing core robot components.",
                "https://meet.google.com/robotics-components",
                baseDate.AddDays(4).AddHours(14), baseDate.AddDays(4).AddHours(16), 30, false, false),
        });

        AddCourseActivities("CRS-ROBOTICS-02", new[]
        {
            NewActivity("ACT-ROBOTICS-02-01", "Mechanics Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading on mechanics and actuators.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-02-02", "Actuator Design Lecture", ActivityType.LiveOnline, 2,
                "Live lecture on actuator selection and torque planning.",
                "https://meet.google.com/robotics-actuators",
                baseDate.AddDays(2).AddHours(10), baseDate.AddDays(2).AddHours(12), 28, false, false),
            NewActivity("ACT-ROBOTICS-02-03", "Mechanical Structures Lab", ActivityType.Offline, 3,
                "Offline lab on assembling simple mechanical structures.",
                "Lab Room 101",
                baseDate.AddDays(6).AddHours(9), baseDate.AddDays(6).AddHours(12), 20, true, false),
        });

        AddCourseActivities("CRS-ROBOTICS-03", new[]
        {
            NewActivity("ACT-ROBOTICS-03-01", "Safety Guidelines Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading on robotics lab safety practices.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-03-02", "Lab Safety Briefing", ActivityType.LiveOnline, 2,
                "Live briefing on lab rules and emergency procedures.",
                "https://meet.google.com/robotics-safety",
                baseDate.AddDays(3).AddHours(9), baseDate.AddDays(3).AddHours(10), 35, false, false),
            NewActivity("ACT-ROBOTICS-03-03", "Sensor Calibration Lab", ActivityType.Offline, 3,
                "Hands-on sensor calibration and obstacle-avoidance testing.",
                "Lab Room 103",
                baseDate.AddDays(10).AddHours(14), baseDate.AddDays(10).AddHours(17), 15, true, true),
        });

        AddCourseActivities("CRS-ROBOTICS-04", new[]
        {
            NewActivity("ACT-ROBOTICS-04-01", "Careers in Robotics Video", ActivityType.SelfPaced, 1,
                "Watch the CNBC feature on robotics engineering careers.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-04-02", "Sensor Exploration Lab", ActivityType.Offline, 2,
                "Hands-on lab exploring ultrasonic and infrared sensors.",
                "Electronics Lab 201",
                baseDate.AddDays(8).AddHours(9), baseDate.AddDays(8).AddHours(12), 24, true, false),
            NewActivity("ACT-ROBOTICS-04-03", "Sensor Data Discussion", ActivityType.LiveOnline, 3,
                "Live discussion on interpreting sensor readings.",
                "https://meet.google.com/robotics-sensors",
                baseDate.AddDays(11).AddHours(15), baseDate.AddDays(11).AddHours(16), 30, false, false),
        });

        AddCourseActivities("CRS-ROBOTICS-05", new[]
        {
            NewActivity("ACT-ROBOTICS-05-01", "Industry Insights Video", ActivityType.SelfPaced, 1,
                "Self-paced CNBC video on real-world robotics engineering work.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-05-02", "Movement Patterns Workshop", ActivityType.LiveOnline, 2,
                "Live workshop on programming robot movement patterns.",
                "https://meet.google.com/robotics-movement",
                baseDate.AddDays(9).AddHours(10), baseDate.AddDays(9).AddHours(12), 28, false, false),
            NewActivity("ACT-ROBOTICS-05-03", "Motor Control Challenge", ActivityType.Offline, 3,
                "Offline challenge to tune motor speed and direction control.",
                "Maker Space B",
                baseDate.AddDays(13).AddHours(9), baseDate.AddDays(13).AddHours(13), 18, true, true),
        });

        AddCourseActivities("CRS-ROBOTICS-06", new[]
        {
            NewActivity("ACT-ROBOTICS-06-01", "Field Insights Video", ActivityType.SelfPaced, 1,
                "Self-paced video on robotics careers and industry demand.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-06-02", "Calibration Techniques Lab", ActivityType.Offline, 2,
                "Hands-on calibration techniques for line-following robots.",
                "Lab Room 104",
                baseDate.AddDays(12).AddHours(14), baseDate.AddDays(12).AddHours(17), 16, true, true),
            NewActivity("ACT-ROBOTICS-06-03", "Experiential Reflection", ActivityType.SelfPaced, 3,
                "Submit a reflection on experiential learning outcomes.", null, null, null, null, false, false),
        });

        AddCourseActivities("CRS-ROBOTICS-07", new[]
        {
            NewActivity("ACT-ROBOTICS-07-01", "Research Design Brief Reading", ActivityType.SelfPaced, 1,
                "Read the research design brief and project requirements.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-07-02", "Team Prototype Build", ActivityType.Offline, 2,
                "Full-day team session to assemble and test prototypes.",
                "Maker Space A",
                baseDate.AddDays(14).AddHours(9), baseDate.AddDays(14).AddHours(17), 12, true, true),
            NewActivity("ACT-ROBOTICS-07-03", "Capstone Presentation", ActivityType.LiveOnline, 3,
                "Teams present robot prototypes and research findings.",
                "https://meet.google.com/robotics-finals",
                baseDate.AddDays(21).AddHours(14), baseDate.AddDays(21).AddHours(16), 50, false, true),
        });

        AddCourseActivities("CRS-ROBOTICS-08", new[]
        {
            NewActivity("ACT-ROBOTICS-08-01", "Research Methods Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading on scientific research methodology.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-08-02", "Prototype Iteration Lab", ActivityType.Offline, 2,
                "Iterate on prototype design based on mentor feedback.",
                "Maker Space A",
                baseDate.AddDays(16).AddHours(9), baseDate.AddDays(16).AddHours(15), 12, true, true),
            NewActivity("ACT-ROBOTICS-08-03", "Peer Review Session", ActivityType.LiveOnline, 3,
                "Live peer review of prototype progress and test results.",
                "https://meet.google.com/robotics-peer-review",
                baseDate.AddDays(19).AddHours(10), baseDate.AddDays(19).AddHours(12), 24, false, false),
        });

        AddCourseActivities("CRS-ROBOTICS-09", new[]
        {
            NewActivity("ACT-ROBOTICS-09-01", "Capstone Documentation Reading", ActivityType.SelfPaced, 1,
                "Self-paced guide for documenting capstone research outcomes.", null, null, null, null, false, false),
            NewActivity("ACT-ROBOTICS-09-02", "Final Testing Lab", ActivityType.Offline, 2,
                "Final testing and performance validation for capstone robots.",
                "Maker Space A",
                baseDate.AddDays(20).AddHours(9), baseDate.AddDays(20).AddHours(13), 12, true, true),
            NewActivity("ACT-ROBOTICS-09-03", "Research Showcase", ActivityType.LiveOnline, 3,
                "Public showcase of capstone research projects.",
                "https://meet.google.com/robotics-showcase",
                baseDate.AddDays(24).AddHours(14), baseDate.AddDays(24).AddHours(17), 40, false, true),
        });
    }
}

