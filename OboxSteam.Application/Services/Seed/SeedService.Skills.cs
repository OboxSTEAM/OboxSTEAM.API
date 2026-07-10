using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedSkillsAsync()
    {
        _loggerService.LogInformation("Starting seed skills catalog");

        var existing = await _unitOfWork.Skills.GetAllAsync(s => !s.IsDeleted);
        var existingCodes = existing.Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var catalog = BuildSkillCatalog();
        var toAdd = catalog
            .Where(s => !existingCodes.Contains(s.Code))
            .ToList();

        if (toAdd.Count == 0)
        {
            _loggerService.LogInformation("Skills catalog already complete ({Count} skill(s)). Skipping.", existing.Count);
            return;
        }

        await _unitOfWork.Skills.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed skills — added {Added} skill(s); catalog total target {Total}.",
            toAdd.Count,
            catalog.Count);
    }

    private static List<Skill> BuildSkillCatalog()
    {
        var now = DateTime.UtcNow;

        Skill Create(
            string code,
            string name,
            SkillCategory category,
            string? subcategory = null,
            string? description = null) =>
            new()
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Category = category,
                Subcategory = subcategory,
                Description = description,
                CreatedAt = now,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

        return
        [
            // Science
            Create("SKL-SCI-OBSERVE", "Observation and recording", SkillCategory.Science, "Scientific method",
                "Observe phenomena and record findings systematically."),
            Create("SKL-SCI-HYPOTHESIS", "Hypothesis and experiment design", SkillCategory.Science, "Scientific method",
                "Form hypotheses and design experiments to test them."),
            Create("SKL-SCI-DATA", "Data collection and analysis", SkillCategory.Science, "Data",
                "Collect, organize, and analyze experimental data."),
            Create("SKL-SCI-REASONING", "Scientific reasoning", SkillCategory.Science, null,
                "Apply scientific reasoning to explain results."),
            Create("SKL-SCI-LAB-TOOLS", "Lab equipment use", SkillCategory.Science, "Lab",
                "Use laboratory tools and equipment safely."),
            Create("SKL-SCI-REPORT", "Scientific report writing", SkillCategory.Science, "Communication",
                "Write clear scientific reports and documentation."),

            // Technology
            Create("SKL-TECH-PROG-SCRATCH", "Programming — Scratch", SkillCategory.Technology, "Programming",
                "Block-based programming with Scratch."),
            Create("SKL-TECH-PROG-PYTHON", "Programming — Python", SkillCategory.Technology, "Programming",
                "Write Python programs using core language features."),
            Create("SKL-TECH-PROG-JS", "Programming — JavaScript", SkillCategory.Technology, "Programming",
                "Write JavaScript for interactivity and web logic."),
            Create("SKL-TECH-COMP-THINK", "Computational thinking", SkillCategory.Technology, null,
                "Decompose problems and design algorithmic solutions."),
            Create("SKL-TECH-SOFTWARE", "Specialized software tools", SkillCategory.Technology, "Tools",
                "Use CAD, design, or data tools for STEAM work."),
            Create("SKL-TECH-DIGITAL-LIT", "Digital literacy", SkillCategory.Technology, null,
                "Search, evaluate information, and stay safe online."),
            Create("SKL-TECH-ROBOTICS-IOT", "Robotics / IoT basics", SkillCategory.Technology, "Embedded",
                "Work with robots, sensors, microcontrollers, and IoT flows."),
            Create("SKL-TECH-DATA-DB", "Data handling and databases", SkillCategory.Technology, "Data",
                "Store, query, and process structured data."),

            // Engineering
            Create("SKL-ENG-DESIGN", "Engineering design process", SkillCategory.Engineering, "Design",
                "Follow define–ideate–prototype–test cycles."),
            Create("SKL-ENG-PROBLEM", "Problem solving", SkillCategory.Engineering, null,
                "Define constraints and solve engineering problems."),
            Create("SKL-ENG-PROTOTYPE", "Prototyping and model making", SkillCategory.Engineering, "Build",
                "Build physical or digital prototypes."),
            Create("SKL-ENG-DRAWING", "Technical drawing literacy", SkillCategory.Engineering, "Design",
                "Read and interpret technical drawings and dimensions."),
            Create("SKL-ENG-SYSTEMS", "Systems thinking", SkillCategory.Engineering, null,
                "Reason about interacting parts of a system."),
            Create("SKL-ENG-TEST-ITERATE", "Testing and iteration", SkillCategory.Engineering, "Build",
                "Test products and improve based on evidence."),

            // Arts
            Create("SKL-ART-VISUAL", "Visual design and drawing", SkillCategory.Arts, "Visual",
                "Create visual compositions and digital artwork."),
            Create("SKL-ART-MUSIC", "Music", SkillCategory.Arts, "Music",
                "Perform, compose, or produce music."),
            Create("SKL-ART-STORY", "Creative writing and storytelling", SkillCategory.Arts, "Narrative",
                "Write and tell creative stories."),
            Create("SKL-ART-UXUI", "UX/UI design basics", SkillCategory.Arts, "Design",
                "Design usable interfaces and layouts."),
            Create("SKL-ART-PERFORM", "Performance arts", SkillCategory.Arts, "Performance",
                "Express ideas through performance."),
            Create("SKL-ART-AESTHETIC", "Aesthetic thinking", SkillCategory.Arts, null,
                "Apply aesthetic judgment to creative work."),

            // Math
            Create("SKL-MATH-LOGIC", "Logical thinking", SkillCategory.Math, null,
                "Apply formal logic and structured reasoning."),
            Create("SKL-MATH-PROBLEM", "Mathematical problem solving", SkillCategory.Math, null,
                "Solve mathematical problems with clear strategies."),
            Create("SKL-MATH-STATS", "Statistics and probability", SkillCategory.Math, "Data",
                "Use statistics and probability concepts."),
            Create("SKL-MATH-MEASURE", "Measurement and spatial geometry", SkillCategory.Math, "Geometry",
                "Measure, estimate, and reason about space."),
            Create("SKL-MATH-MODEL", "Mathematical modeling", SkillCategory.Math, null,
                "Model real situations with mathematics."),

            // Soft / 21st century
            Create("SKL-SOFT-CRITICAL", "Critical thinking", SkillCategory.SoftSkill, "4C",
                "Evaluate claims and reason carefully."),
            Create("SKL-SOFT-COMM", "Communication", SkillCategory.SoftSkill, "4C",
                "Present and explain ideas clearly."),
            Create("SKL-SOFT-COLLAB", "Collaboration", SkillCategory.SoftSkill, "4C",
                "Work effectively in a team."),
            Create("SKL-SOFT-CREATIVE", "Creativity", SkillCategory.SoftSkill, "4C",
                "Generate original ideas and approaches."),
            Create("SKL-SOFT-LEADER", "Leadership", SkillCategory.SoftSkill, null,
                "Guide peers and take ownership."),
            Create("SKL-SOFT-TIME", "Time management", SkillCategory.SoftSkill, null,
                "Plan and manage time for learning tasks."),
            Create("SKL-SOFT-SELFLEARN", "Self-directed learning", SkillCategory.SoftSkill, null,
                "Learn independently and seek resources."),
            Create("SKL-SOFT-ADAPT", "Adaptability", SkillCategory.SoftSkill, null,
                "Adjust to new tools, feedback, and constraints."),
            Create("SKL-SOFT-DIGCIT", "Digital citizenship", SkillCategory.SoftSkill, null,
                "Act responsibly and ethically online.")
        ];
    }
}
