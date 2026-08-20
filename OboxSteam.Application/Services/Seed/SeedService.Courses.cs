using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedCoursesAsync()
    {
        _loggerService.LogInformation("Starting seed courses");
        var existingCourses = await _unitOfWork.Courses.GetAllAsync();
        if (existingCourses.Any())
        {
            _loggerService.LogInformation("Courses already exist, skipping course seeding");
            return;
        }

        var modules = await _unitOfWork.Modules.GetAllAsync(m => !m.IsDeleted);
        var moduleByCode = modules.ToDictionary(m => m.Code, m => m, StringComparer.OrdinalIgnoreCase);
        var courses = new List<Course>();
        var createdAt = AtMonths(-11);

        AddRoboticsCourses(
            courses,
            GetModule(moduleByCode, "MOD-ROBOTICS-01"),
            GetModule(moduleByCode, "MOD-ROBOTICS-02"),
            GetModule(moduleByCode, "MOD-ROBOTICS-03"),
            createdAt);

        AddCourseIfPresent(courses, moduleByCode, "MOD-WEBDEV-01", "CRS-WEBDEV-01",
            "HTML & CSS Foundations", "Semantic markup and responsive CSS layouts.", createdAt);
        AddCourseIfPresent(courses, moduleByCode, "MOD-WEBDEV-02", "CRS-WEBDEV-02",
            "JavaScript Basics", "Variables, DOM manipulation, and simple interactive pages.", createdAt);
        AddCourseIfPresent(courses, moduleByCode, "MOD-WEBDEV-03", "CRS-WEBDEV-03",
            "Responsive Project Studio", "Ship a small responsive site end-to-end.", createdAt);

        AddCourseIfPresent(courses, moduleByCode, "MOD-IOT-01", "CRS-IOT-01",
            "Sensors 101", "Introduction to sensors, Arduino basics, and environmental data.", createdAt);
        AddCourseIfPresent(courses, moduleByCode, "MOD-IOT-02", "CRS-IOT-02",
            "Cloud Lab", "Connect devices with MQTT and visualize live sensor data.", createdAt);
        AddCourseIfPresent(courses, moduleByCode, "MOD-IOT-03", "CRS-IOT-03",
            "IoT Showcase Studio", "Build and present an end-to-end device-to-cloud prototype.", createdAt);

        AddThinCatalogCourses(courses, moduleByCode, createdAt);

        if (courses.Count == 0)
        {
            _loggerService.LogWarning("No courses seeded because required modules were not found.");
            return;
        }

        await _unitOfWork.Courses.AddRangeAsync(courses);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed courses — {Count} course(s) created.", courses.Count);
    }

    private static Module? GetModule(IReadOnlyDictionary<string, Module> moduleByCode, string code)
        => moduleByCode.TryGetValue(code, out var module) ? module : null;

    private static void AddCourseIfPresent(
        List<Course> courses,
        IReadOnlyDictionary<string, Module> moduleByCode,
        string moduleCode,
        string courseCode,
        string name,
        string description,
        DateTime createdAt,
        int courseOrder = 1)
    {
        if (!moduleByCode.TryGetValue(moduleCode, out var module))
        {
            return;
        }

        courses.Add(new Course
        {
            Id = Guid.NewGuid(),
            Code = courseCode,
            ModuleId = module.Id,
            Name = name,
            Description = description,
            CourseOrder = courseOrder,
            CreatedAt = createdAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private static void AddThinCatalogCourses(
        List<Course> courses,
        IReadOnlyDictionary<string, Module> moduleByCode,
        DateTime createdAt)
    {
        (string ModuleCode, string CourseCode, string Name)[] catalog =
        [
            ("MOD-PYBASIC-01", "CRS-PYBASIC-01", "Python Syntax Studio"),
            ("MOD-PYBASIC-02", "CRS-PYBASIC-02", "Functions and Loops Lab"),
            ("MOD-PYBASIC-03", "CRS-PYBASIC-03", "Python Mini-Project"),
            ("MOD-MATHFUN-01", "CRS-MATHFUN-01", "Number Sense Games"),
            ("MOD-MATHFUN-02", "CRS-MATHFUN-02", "Puzzle Reasoning Lab"),
            ("MOD-MATHFUN-03", "CRS-MATHFUN-03", "Math Challenge Studio"),
            ("MOD-DIGART-01", "CRS-DIGART-01", "Digital Drawing Studio"),
            ("MOD-DIGART-02", "CRS-DIGART-02", "Color and Character Lab"),
            ("MOD-DIGART-03", "CRS-DIGART-03", "Illustration Showcase"),
            ("MOD-BIOTECH-01", "CRS-BIOTECH-01", "Cell Biology Studio"),
            ("MOD-BIOTECH-02", "CRS-BIOTECH-02", "Genetics Simulation Lab"),
            ("MOD-BIOTECH-03", "CRS-BIOTECH-03", "Biotech Case Study"),
            ("MOD-3DDESIGN-01", "CRS-3DDESIGN-01", "CAD Foundations"),
            ("MOD-3DDESIGN-02", "CRS-3DDESIGN-02", "Printable Prototype Lab"),
            ("MOD-3DDESIGN-03", "CRS-3DDESIGN-03", "Design Review Studio"),
            ("MOD-AIBASIC-01", "CRS-AIBASIC-01", "AI Concepts Studio"),
            ("MOD-AIBASIC-02", "CRS-AIBASIC-02", "Image Recognition Lab"),
            ("MOD-AIBASIC-03", "CRS-AIBASIC-03", "Chatbot Mini-Project"),
            ("MOD-ENVSCI-01", "CRS-ENVSCI-01", "Ecology Studio"),
            ("MOD-ENVSCI-02", "CRS-ENVSCI-02", "Field Data Lab"),
            ("MOD-ENVSCI-03", "CRS-ENVSCI-03", "Sustainability Showcase"),
            ("MOD-GAMEDEV-01", "CRS-GAMEDEV-01", "Game Logic Studio"),
            ("MOD-GAMEDEV-02", "CRS-GAMEDEV-02", "Level Design Lab"),
            ("MOD-GAMEDEV-03", "CRS-GAMEDEV-03", "Sprite Animation Workshop"),
            ("MOD-GAMEDEV-04", "CRS-GAMEDEV-04", "Playable Prototype Studio"),
            ("MOD-MUSICTECH-01", "CRS-MUSICTECH-01", "DAW Foundations"),
            ("MOD-MUSICTECH-02", "CRS-MUSICTECH-02", "Sound Design Lab"),
            ("MOD-MUSICTECH-03", "CRS-MUSICTECH-03", "Track Mix Showcase"),
            ("MOD-DATAMATH-01", "CRS-DATAMATH-01", "Statistics Studio"),
            ("MOD-DATAMATH-02", "CRS-DATAMATH-02", "Probability Lab"),
            ("MOD-DATAMATH-03", "CRS-DATAMATH-03", "Data Story Showcase"),
            ("MOD-CERT-TEST-01", "CRS-CERT-TEST-01", "Workshop Reading Studio"),
        ];

        foreach (var item in catalog)
        {
            AddCourseIfPresent(
                courses,
                moduleByCode,
                item.ModuleCode,
                item.CourseCode,
                item.Name,
                $"Catalog course for {item.Name}.",
                createdAt);
        }
    }
}
