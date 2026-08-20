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
        new("ACT-IOT-01-01", "Microcontroller Basics", MaterialType.Video,
            "https://storage.oboxsteam.com/materials/video/microcontroller-basics.mp4", 52_300_000L),
        new("ACT-IOT-01-02", "Sensor Wiring Guide", MaterialType.Image,
            "https://storage.oboxsteam.com/materials/image/sensor-wiring-diagram.png", 2_400_000L),
        new("ACT-IOT-02-01", "MQTT Concepts Explained", MaterialType.Video,
            "https://storage.oboxsteam.com/materials/video/mqtt-concepts.mp4", 36_700_000L),
        new("ACT-IOT-02-02", "Cloud Dashboard Setup Guide", MaterialType.PDF,
            "https://storage.oboxsteam.com/materials/pdf/cloud-dashboard-setup.pdf", 945_000L),
        new("ACT-CERT-TEST-01-01", "Certificate Test Reading Guide", MaterialType.PDF,
            "https://storage.oboxsteam.com/materials/pdf/certificate-test-reading-guide.pdf", 128_000L),
    ];

    private async Task SeedMaterialsAsync()
    {
        _loggerService.LogInformation("Starting seed materials");

        var definitions = GetSeedMaterialDefinitions();
        var definitionByCode = definitions.ToDictionary(
            d => d.ActivityCode,
            d => d,
            StringComparer.OrdinalIgnoreCase);

        var existingMaterials = await _unitOfWork.Materials.GetAllAsync();
        var activityIdsWithMaterial = existingMaterials
            .Select(m => m.ActivityId)
            .ToHashSet();

        var selfPacedActivities = await _unitOfWork.Activities.GetAllAsync(
            a => !a.IsDeleted && a.ActivityType == ActivityType.SelfPaced);

        var seedTime = _seedNow;
        var materialsToAdd = new List<Material>();

        foreach (var activity in selfPacedActivities)
        {
            if (activityIdsWithMaterial.Contains(activity.Id))
            {
                continue;
            }

            if (!definitionByCode.TryGetValue(activity.Code, out var definition))
            {
                definition = new SeedMaterialDefinition(
                    activity.Code,
                    $"{activity.Name} reading",
                    MaterialType.PDF,
                    "https://storage.oboxsteam.com/materials/pdf/catalog-reading-guide.pdf",
                    180_000L);
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

        AddRoboticsSeedActivities(activities, courseByCode, seedTime);

        AddActivities("CRS-WEBDEV-01", new[]
        {
            NewActivity("ACT-WEBDEV-01-01", "HTML Structure Overview", ActivityType.SelfPaced, 1,
                "Video lessons on semantic HTML and document structure.", null, false, false),
            NewActivity("ACT-WEBDEV-01-02", "Live CSS Layout Session", ActivityType.LiveOnline, 2,
                "Live session on flexbox and grid layouts.", 120, false, false),
            NewActivity("ACT-WEBDEV-01-03", "Responsive Layout Exercises", ActivityType.SelfPaced, 3,
                "Self-paced responsive layout practice exercises.", null, false, false),
        });

        AddActivities("CRS-WEBDEV-02", new[]
        {
            NewActivity("ACT-WEBDEV-02-01", "JavaScript Variables & Types", ActivityType.SelfPaced, 1,
                "Self-paced module on JS fundamentals.", null, false, false),
            NewActivity("ACT-WEBDEV-02-02", "DOM Manipulation Lab", ActivityType.Offline, 2,
                "Hands-on lab for DOM manipulation exercises.", 120, true, false),
            NewActivity("ACT-WEBDEV-02-03", "Weekend Hackathon", ActivityType.Offline, 3,
                "Build a simple interactive page in teams.", 360, true, true),
            NewActivity("ACT-WEBDEV-02-04", "Code Review Checklist", ActivityType.SelfPaced, 4,
                "Self-paced code review checklist and mentor feedback guide.", null, false, false),
        });

        AddActivities("CRS-WEBDEV-03", new[]
        {
            NewActivity("ACT-WEBDEV-03-01", "Responsive Design Brief", ActivityType.SelfPaced, 1,
                "Plan a responsive page for multiple screen sizes.", null, false, false),
            NewActivity("ACT-WEBDEV-03-02", "Launch Review Call", ActivityType.LiveOnline, 2,
                "Live review before shipping the small web project.", 90, false, false),
            NewActivity("ACT-WEBDEV-03-03", "Capstone Demo Day", ActivityType.LiveOnline, 3,
                "Present deployed capstone sites to mentors.", 120, false, true),
        });

        AddActivities("CRS-IOT-01", new[]
        {
            NewActivity("ACT-IOT-01-01", "Microcontroller Basics", ActivityType.SelfPaced, 1,
                "Self-paced intro to Arduino and GPIO pins.", null, false, false),
            NewActivity("ACT-IOT-01-02", "Sensor Wiring Guide", ActivityType.SelfPaced, 2,
                "Self-paced guide for wiring temperature and humidity sensors.", null, false, false),
            NewActivity("ACT-IOT-01-03", "Live Q&A: Sensor Data", ActivityType.LiveOnline, 3,
                "Live Q&A on reading and interpreting sensor data.", 60, false, false),
        });

        AddActivities("CRS-IOT-02", new[]
        {
            NewActivity("ACT-IOT-02-01", "MQTT Concepts", ActivityType.SelfPaced, 1,
                "Learn MQTT publish/subscribe patterns.", null, false, false),
            NewActivity("ACT-IOT-02-02", "Cloud Dashboard Setup Guide", ActivityType.SelfPaced, 2,
                "Self-paced guide for setting up a cloud dashboard.", null, false, false),
            NewActivity("ACT-IOT-02-03", "Device Deployment Lab", ActivityType.Offline, 3,
                "Deploy a device and verify cloud connectivity.", 240, true, true),
        });

        AddActivities("CRS-IOT-03", new[]
        {
            NewActivity("ACT-IOT-03-01", "Showcase Planning", ActivityType.SelfPaced, 1,
                "Plan the IoT prototype demo and evidence pack.", null, false, false),
            NewActivity("ACT-IOT-03-02", "Prototype Showcase", ActivityType.LiveOnline, 2,
                "Present the device-to-cloud prototype to mentors.", 90, false, true),
        });

        AddThinCatalogActivities(AddActivities);

        AddActivities("CRS-CERT-TEST-01", new[]
        {
            NewActivity("ACT-CERT-TEST-01-01", "Certificate Test Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading activity for certificate generation testing.",
                null, false, false),
            // LiveOnline anchor so the cert test cohort has a schedulable item — an Open
            // class is only valid with a generated schedule covering the curriculum.
            NewActivity("ACT-CERT-TEST-01-02", "Certificate Orientation Call", ActivityType.LiveOnline, 2,
                "Short live kickoff call before the self-paced reading.",
                60, false, false),
        });

        return activities;
    }

    private static void AddThinCatalogActivities(Action<string, IEnumerable<Activity>> addActivities)
    {
        (string CourseCode, string SelfPacedCode, string LiveCode, string Name)[] catalog =
        [
            ("CRS-PYBASIC-01", "ACT-PYBASIC-01-01", "ACT-PYBASIC-01-02", "Python Syntax"),
            ("CRS-PYBASIC-02", "ACT-PYBASIC-02-01", "ACT-PYBASIC-02-02", "Functions and Loops"),
            ("CRS-PYBASIC-03", "ACT-PYBASIC-03-01", "ACT-PYBASIC-03-02", "Python Mini-Project"),
            ("CRS-MATHFUN-01", "ACT-MATHFUN-01-01", "ACT-MATHFUN-01-02", "Number Sense"),
            ("CRS-MATHFUN-02", "ACT-MATHFUN-02-01", "ACT-MATHFUN-02-02", "Puzzle Reasoning"),
            ("CRS-MATHFUN-03", "ACT-MATHFUN-03-01", "ACT-MATHFUN-03-02", "Math Challenge"),
            ("CRS-DIGART-01", "ACT-DIGART-01-01", "ACT-DIGART-01-02", "Digital Drawing"),
            ("CRS-DIGART-02", "ACT-DIGART-02-01", "ACT-DIGART-02-02", "Color and Character"),
            ("CRS-DIGART-03", "ACT-DIGART-03-01", "ACT-DIGART-03-02", "Illustration Showcase"),
            ("CRS-BIOTECH-01", "ACT-BIOTECH-01-01", "ACT-BIOTECH-01-02", "Cell Biology"),
            ("CRS-BIOTECH-02", "ACT-BIOTECH-02-01", "ACT-BIOTECH-02-02", "Genetics Simulation"),
            ("CRS-BIOTECH-03", "ACT-BIOTECH-03-01", "ACT-BIOTECH-03-02", "Biotech Case Study"),
            ("CRS-3DDESIGN-01", "ACT-3DDESIGN-01-01", "ACT-3DDESIGN-01-02", "CAD Foundations"),
            ("CRS-3DDESIGN-02", "ACT-3DDESIGN-02-01", "ACT-3DDESIGN-02-02", "Printable Prototype"),
            ("CRS-3DDESIGN-03", "ACT-3DDESIGN-03-01", "ACT-3DDESIGN-03-02", "Design Review"),
            ("CRS-AIBASIC-01", "ACT-AIBASIC-01-01", "ACT-AIBASIC-01-02", "AI Concepts"),
            ("CRS-AIBASIC-02", "ACT-AIBASIC-02-01", "ACT-AIBASIC-02-02", "Image Recognition"),
            ("CRS-AIBASIC-03", "ACT-AIBASIC-03-01", "ACT-AIBASIC-03-02", "Chatbot Mini-Project"),
            ("CRS-ENVSCI-01", "ACT-ENVSCI-01-01", "ACT-ENVSCI-01-02", "Ecology Studio"),
            ("CRS-ENVSCI-02", "ACT-ENVSCI-02-01", "ACT-ENVSCI-02-02", "Field Data"),
            ("CRS-ENVSCI-03", "ACT-ENVSCI-03-01", "ACT-ENVSCI-03-02", "Sustainability Showcase"),
            ("CRS-GAMEDEV-01", "ACT-GAMEDEV-01-01", "ACT-GAMEDEV-01-02", "Game Logic"),
            ("CRS-GAMEDEV-02", "ACT-GAMEDEV-02-01", "ACT-GAMEDEV-02-02", "Level Design"),
            ("CRS-GAMEDEV-03", "ACT-GAMEDEV-03-01", "ACT-GAMEDEV-03-02", "Sprite Animation"),
            ("CRS-GAMEDEV-04", "ACT-GAMEDEV-04-01", "ACT-GAMEDEV-04-02", "Playable Prototype"),
            ("CRS-MUSICTECH-01", "ACT-MUSICTECH-01-01", "ACT-MUSICTECH-01-02", "DAW Foundations"),
            ("CRS-MUSICTECH-02", "ACT-MUSICTECH-02-01", "ACT-MUSICTECH-02-02", "Sound Design"),
            ("CRS-MUSICTECH-03", "ACT-MUSICTECH-03-01", "ACT-MUSICTECH-03-02", "Track Mix"),
            ("CRS-DATAMATH-01", "ACT-DATAMATH-01-01", "ACT-DATAMATH-01-02", "Statistics Studio"),
            ("CRS-DATAMATH-02", "ACT-DATAMATH-02-01", "ACT-DATAMATH-02-02", "Probability Lab"),
            ("CRS-DATAMATH-03", "ACT-DATAMATH-03-01", "ACT-DATAMATH-03-02", "Data Story"),
        ];

        foreach (var item in catalog)
        {
            addActivities(item.CourseCode, new[]
            {
                NewActivity(item.SelfPacedCode, $"{item.Name} reading", ActivityType.SelfPaced, 1,
                    $"Self-paced introduction to {item.Name}.", null, false, false),
                NewActivity(item.LiveCode, $"{item.Name} live session", ActivityType.LiveOnline, 2,
                    $"Live cohort session for {item.Name}.", 90, false, false),
            });
        }
    }

    private static Activity NewActivity(
        string code,
        string name,
        ActivityType activityType,
        int activityOrder,
        string? description,
        int? durationMinutes,
        bool requireQrCheckin,
        bool requireMediaEvidence) => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ActivityType = activityType,
            Description = description,
            ActivityOrder = activityOrder,
            DurationMinutes = durationMinutes,
            RequireQrCheckin = requireQrCheckin,
            RequireMediaEvidence = requireMediaEvidence,
        };

    private const string RoboticsTheoryMaterialUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/GI%C3%81O+TR%C3%8CNH+CH%E1%BB%A6+NGH%C4%A8A+X%C3%83+H%E1%BB%98I+KHOA+H%E1%BB%8CC+(Quoc+gia).pdf";

    private const string RoboticsExperientialMaterialUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/Robotics+engineers+are+in+high+demand+%E2%80%94+but+what+is+the+job+really+like+-+CNBC+International+(720p%2C+h264).mp4";

    private const string RoboticsResearchMaterialUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/Gi%C3%A1o+tr%C3%ACnh+k%E1%BB%B9+thu%E1%BA%ADt+robot+-+%C4%90%C3%A0o+V%C4%83n+Hi%E1%BB%87p.pdf";

    private const string DemoShowcaseVideoMaterialUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/Robotics-video.mp4";

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

        var nextOrderByModule = new Dictionary<Guid, int>();

        void AddCourse(Module? module, string code, string name, string description)
        {
            if (module == null)
            {
                return;
            }

            var order = nextOrderByModule.GetValueOrDefault(module.Id, 0) + 1;
            nextOrderByModule[module.Id] = order;

            courses.Add(new Course
            {
                Id = Guid.NewGuid(),
                Code = code,
                ModuleId = module.Id,
                Name = name,
                Description = description,
                CourseOrder = order,
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
                "Self-paced reading from the robotics techniques textbook.", null, false, false),
            NewActivity("ACT-ROBOTICS-01-02", "Introduction to Robotics", ActivityType.LiveOnline, 2,
                "Live online introduction to robotics systems.", 120, false, false),
            NewActivity("ACT-ROBOTICS-01-03", "Components Overview Workshop", ActivityType.LiveOnline, 3,
                "Live workshop reviewing core robot components.", 120, false, false),
        });

        AddCourseActivities("CRS-ROBOTICS-02", new[]
        {
            NewActivity("ACT-ROBOTICS-02-01", "Mechanics Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading on mechanics and actuators.", null, false, false),
            NewActivity("ACT-ROBOTICS-02-02", "Actuator Design Lecture", ActivityType.LiveOnline, 2,
                "Live lecture on actuator selection and torque planning.", 120, false, false),
            NewActivity("ACT-ROBOTICS-02-03", "Mechanical Structures Q&A", ActivityType.LiveOnline, 3,
                "Live Q&A on mechanical structures and actuator integration.", 60, false, false),
        });

        AddCourseActivities("CRS-ROBOTICS-03", new[]
        {
            NewActivity("ACT-ROBOTICS-03-01", "Safety Guidelines Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading on robotics lab safety practices.", null, false, false),
            NewActivity("ACT-ROBOTICS-03-02", "Lab Safety Briefing", ActivityType.LiveOnline, 2,
                "Live briefing on lab rules and emergency procedures.", 60, false, false),
            NewActivity("ACT-ROBOTICS-03-03", "Safety Case Study Review", ActivityType.LiveOnline, 3,
                "Live review of lab safety case studies and documentation practices.", 60, false, false),
        });

        AddCourseActivities("CRS-ROBOTICS-04", new[]
        {
            NewActivity("ACT-ROBOTICS-04-01", "Careers in Robotics Video", ActivityType.SelfPaced, 1,
                "Watch the CNBC feature on robotics engineering careers.", null, false, false),
            NewActivity("ACT-ROBOTICS-04-02", "Field Trip Preparation Briefing", ActivityType.LiveOnline, 2,
                "Live briefing with your mentor on what to bring and safety rules for the sensor field trip.", 60, false, false),
            NewActivity("ACT-ROBOTICS-04-03", "Sensor Exploration Field Trip", ActivityType.Offline, 3,
                "On-site field trip exploring ultrasonic and infrared sensors in a real lab environment.", 180, true, true),
        });

        AddCourseActivities("CRS-ROBOTICS-05", new[]
        {
            NewActivity("ACT-ROBOTICS-05-01", "Industry Insights Video", ActivityType.SelfPaced, 1,
                "Self-paced CNBC video on real-world robotics engineering work.", null, false, false),
            NewActivity("ACT-ROBOTICS-05-02", "Movement Trip Preparation", ActivityType.LiveOnline, 2,
                "Live mentor session on preparing equipment and goals for the motor control field challenge.", 60, false, false),
            NewActivity("ACT-ROBOTICS-05-03", "Motor Control Field Challenge", ActivityType.Offline, 3,
                "On-site challenge to tune motor speed and direction control.", 240, true, true),
        });

        AddCourseActivities("CRS-ROBOTICS-06", new[]
        {
            NewActivity("ACT-ROBOTICS-06-01", "Field Insights Video", ActivityType.SelfPaced, 1,
                "Self-paced video on robotics careers and industry demand.", null, false, false),
            NewActivity("ACT-ROBOTICS-06-02", "Calibration Trip Preparation", ActivityType.LiveOnline, 2,
                "Live mentor briefing on calibration tools, clothing, and checklist before the field lab.", 60, false, false),
            NewActivity("ACT-ROBOTICS-06-03", "Calibration Techniques Field Lab", ActivityType.Offline, 3,
                "On-site calibration techniques for line-following robots.", 180, true, true),
        });

        AddCourseActivities("CRS-ROBOTICS-07", new[]
        {
            NewActivity("ACT-ROBOTICS-07-01", "Research Design Brief Reading", ActivityType.SelfPaced, 1,
                "Read the research design brief and project requirements.", null, false, false),
            NewActivity("ACT-ROBOTICS-07-02", "Prototype Build Preparation", ActivityType.LiveOnline, 2,
                "Live mentor session on team roles, materials, and build-day logistics.", 60, false, false),
            NewActivity("ACT-ROBOTICS-07-03", "Team Prototype Build", ActivityType.Offline, 3,
                "Full-day team session to assemble and test prototypes.", 480, true, true),
        });

        AddCourseActivities("CRS-ROBOTICS-08", new[]
        {
            NewActivity("ACT-ROBOTICS-08-01", "Research Methods Reading", ActivityType.SelfPaced, 1,
                "Self-paced reading on scientific research methodology.", null, false, false),
            NewActivity("ACT-ROBOTICS-08-02", "Iteration Planning Session", ActivityType.LiveOnline, 2,
                "Live session to plan prototype iterations based on mentor feedback.", 60, false, false),
            NewActivity("ACT-ROBOTICS-08-03", "Prototype Iteration Lab", ActivityType.Offline, 3,
                "Iterate on prototype design and run bench tests.", 360, true, true),
        });

        AddCourseActivities("CRS-ROBOTICS-09", new[]
        {
            NewActivity("ACT-ROBOTICS-09-01", "Capstone Documentation Reading", ActivityType.SelfPaced, 1,
                "Self-paced guide for documenting capstone research outcomes.", null, false, false),
            NewActivity("ACT-ROBOTICS-09-02", "Final Testing Preparation", ActivityType.LiveOnline, 2,
                "Live briefing on final test procedures, safety checks, and demo setup.", 60, false, false),
            NewActivity("ACT-ROBOTICS-09-03", "Final Testing & Showcase", ActivityType.Offline, 3,
                "On-site final testing, performance validation, and capstone showcase.", 360, true, true),
        });
    }
}

