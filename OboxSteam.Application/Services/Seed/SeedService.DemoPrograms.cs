using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Idempotent demo showcase programs for mentor grading walkthroughs.
/// Does not modify existing programs; safe to run on an already-seeded database.
/// </summary>
public partial class SeedService
{
    /// <summary>
    /// One Active demo program (+ matching class) per student. Students chosen from
    /// Completed/Failed/Dropped-only roster so academic Active/Pending slots stay ≤ 2.
    /// STD-001/002 already hold Robotics Active and must not receive demo enrollments.
    /// </summary>
    private static readonly Dictionary<string, string[]> DemoStudentCodesByProgram =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PRG-DEMO-SCRATCH"] = ["STD-003", "STD-016"],
            ["PRG-DEMO-CLIMATE"] = ["STD-007", "STD-017"],
            ["PRG-DEMO-MAKER"] = ["STD-009", "STD-010"],
        };

    internal static IReadOnlyDictionary<string, string[]> GetDemoStudentCodesByProgram()
        => DemoStudentCodesByProgram;

    private async Task SeedDemoShowcaseProgramsAsync()
    {
        _loggerService.LogInformation("Starting seed demo showcase programs");

        var seedTime = _seedNow;
        var mentors = (await _unitOfWork.Users.GetAllAsync(u => u.Role == RoleType.Mentor && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in GetDemoProgramDefinitions())
        {
            if (!mentors.TryGetValue(definition.InProgressMentorCode, out var inProgressMentor)
                || !mentors.TryGetValue(definition.OpenMentorCode, out var openMentor))
            {
                _loggerService.LogWarning(
                    "Skipping demo program {ProgramCode}: mentor {InProgressMentor} or {OpenMentor} not found.",
                    definition.ProgramCode,
                    definition.InProgressMentorCode,
                    definition.OpenMentorCode);
                continue;
            }

            await SeedOneDemoProgramAsync(
                definition,
                inProgressMentor.Id,
                openMentor.Id,
                seedTime);
        }

        // Demo programs stay submission-free (quiz / retrospective / research file uploads).
        await ClearDemoProgramSubmissionsAsync();

        _loggerService.LogInformation("Finished seed demo showcase programs");
    }

    private static HashSet<string> GetDemoProgramCodeSet()
        => GetDemoProgramDefinitions()
            .Select(d => d.ProgramCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Program ids for demo showcase tracks. Used to keep dashboard/global seeds off live-demo data.
    /// </summary>
    private async Task<HashSet<Guid>> GetDemoProgramIdsAsync()
    {
        var demoProgramCodes = GetDemoProgramCodeSet();
        var demoPrograms = await _unitOfWork.Programs.GetAllAsync(
            p => demoProgramCodes.Contains(p.Code) && !p.IsDeleted);
        return demoPrograms.Select(p => p.Id).ToHashSet();
    }

    /// <summary>
    /// Removes any submissions on demo showcase assignments so the track stays clean for live demos.
    /// Demo seed never creates submissions; this clears leftovers from prior manual testing
    /// and from global seeds that previously targeted every assignment.
    /// </summary>
    private async Task ClearDemoProgramSubmissionsAsync()
    {
        var demoProgramIds = await GetDemoProgramIdsAsync();
        if (demoProgramIds.Count == 0)
        {
            return;
        }

        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => demoProgramIds.Contains(m.ProgramId) && !m.IsDeleted);
        if (modules.Count == 0)
        {
            return;
        }

        var moduleIds = modules.Select(m => m.Id).ToHashSet();
        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted);
        if (assignments.Count == 0)
        {
            return;
        }

        var assignmentIds = assignments.Select(a => a.Id).ToHashSet();
        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => assignmentIds.Contains(s.AssignmentId) && !s.IsDeleted);

        if (submissions.Count == 0)
        {
            _loggerService.LogInformation("Demo showcase programs have no submissions to clear.");
            return;
        }

        await _unitOfWork.Submissions.SoftRemoveRange(submissions);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Cleared {Count} submission(s) from demo showcase program assignments.",
            submissions.Count);
    }

    private sealed record DemoProgramDefinition(
        string ProgramCode,
        string Name,
        string SeriesName,
        string Description,
        DifficultyLevel Level,
        ProgramCategory Category,
        string EstimatedDuration,
        string SkillsGained,
        decimal Price,
        string ThumbnailUrl,
        string ClassCode,
        string ClassName,
        string ScheduleSummary,
        string InProgressMentorCode,
        string OpenMentorCode,
        string TheoryModuleName,
        string ExperientialModuleName,
        string ResearchModuleName,
        string TheoryCourseName,
        string ExperientialCourseName,
        string ResearchCourseName,
        string QuizBankName,
        string QuizTitle,
        string RetrospectiveTitle,
        string RetrospectiveDescription,
        string Milestone1Title,
        string Milestone1Description,
        string Milestone1AssignmentTitle,
        string Milestone2Title,
        string Milestone2Description,
        string Milestone2AssignmentTitle,
        (string Text, int Difficulty, string[] Options)[] BankQuestions)
    {
        public string ResolveOpenClassCode()
            => ClassCode.EndsWith("2026A", StringComparison.OrdinalIgnoreCase)
                ? ClassCode[..^5] + "2026B"
                : $"{ClassCode}-OPEN";
    }

    private static IReadOnlyList<DemoProgramDefinition> GetDemoProgramDefinitions() =>
    [
        new(
            ProgramCode: "PRG-DEMO-SCRATCH",
            Name: "Creative Coding with Scratch",
            SeriesName: "Demo Showcase",
            Description: "A short demo track for block-based coding: learn sprites, build a mini-game, then document a creative project.",
            Level: DifficultyLevel.Beginner,
            Category: ProgramCategory.Technology,
            EstimatedDuration: "3 weeks at 2 hours a week",
            SkillsGained: "Block coding, sprites, loops, creative storytelling",
            Price: 900_000m,
            ThumbnailUrl:
                "https://images.unsplash.com/photo-1587620962725-abab7fe55159?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            ClassCode: "CLS-DEMO-SCRATCH-2026A",
            ClassName: "Scratch Demo Cohort A",
            ScheduleSummary: "Saturday & Sunday 09:00-11:00",
            InProgressMentorCode: "MNT-001",
            OpenMentorCode: "MNT-003",
            TheoryModuleName: "Scratch Basics",
            ExperientialModuleName: "Build a Mini-Game",
            ResearchModuleName: "Creative Project Showcase",
            TheoryCourseName: "Sprites & Scripts",
            ExperientialCourseName: "Game Lab",
            ResearchCourseName: "Project Studio",
            QuizBankName: "Scratch Basics Question Bank",
            QuizTitle: "Scratch Basics Quiz",
            RetrospectiveTitle: "Mini-Game Lab Retrospective",
            RetrospectiveDescription: "Write a short reflection on what you built, what was hard, and what you would try next.",
            Milestone1Title: "Project Plan Upload",
            Milestone1Description: "Plan your Scratch story or game and upload a short design note.",
            Milestone1AssignmentTitle: "Upload Project Plan",
            Milestone2Title: "Final Project File",
            Milestone2Description: "Upload the finished Scratch project export and a short demo note.",
            Milestone2AssignmentTitle: "Upload Final Scratch Project",
            BankQuestions: ScratchDemoBankQuestions),
        new(
            ProgramCode: "PRG-DEMO-CLIMATE",
            Name: "Climate Detectives",
            SeriesName: "Demo Showcase",
            Description: "A short demo science track: learn climate basics, measure local clues, then share evidence as research milestones.",
            Level: DifficultyLevel.Beginner,
            Category: ProgramCategory.Science,
            EstimatedDuration: "3 weeks at 2 hours a week",
            SkillsGained: "Climate literacy, observation, data notes, evidence sharing",
            Price: 850_000m,
            ThumbnailUrl:
                "https://images.unsplash.com/photo-1569163139394-de460e9b8570?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            ClassCode: "CLS-DEMO-CLIMATE-2026A",
            ClassName: "Climate Detectives Cohort A",
            ScheduleSummary: "Saturday & Sunday 09:00-11:00",
            InProgressMentorCode: "MNT-002",
            OpenMentorCode: "MNT-005",
            TheoryModuleName: "Climate Foundations",
            ExperientialModuleName: "Field Clues Lab",
            ResearchModuleName: "Evidence Report",
            TheoryCourseName: "Weather & Climate",
            ExperientialCourseName: "Observation Lab",
            ResearchCourseName: "Detective Report Studio",
            QuizBankName: "Climate Foundations Question Bank",
            QuizTitle: "Climate Foundations Quiz",
            RetrospectiveTitle: "Field Clues Retrospective",
            RetrospectiveDescription: "Reflect on the clues you observed outdoors and what they might mean for local climate.",
            Milestone1Title: "Observation Log Upload",
            Milestone1Description: "Upload your observation log with photos or notes from the field.",
            Milestone1AssignmentTitle: "Upload Observation Log",
            Milestone2Title: "Evidence Summary Upload",
            Milestone2Description: "Upload a short evidence summary connecting clues to a climate idea.",
            Milestone2AssignmentTitle: "Upload Evidence Summary",
            BankQuestions: ClimateDemoBankQuestions),
        new(
            ProgramCode: "PRG-DEMO-MAKER",
            Name: "Maker Lab Adventures",
            SeriesName: "Demo Showcase",
            Description: "A short demo engineering track: learn maker safety, build a simple prototype, then upload milestone deliverables.",
            Level: DifficultyLevel.Beginner,
            Category: ProgramCategory.Engineering,
            EstimatedDuration: "3 weeks at 2 hours a week",
            SkillsGained: "Maker safety, prototyping, iteration notes, demo delivery",
            Price: 950_000m,
            ThumbnailUrl:
                "https://images.unsplash.com/photo-1581092160562-40aa08e78837?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            ClassCode: "CLS-DEMO-MAKER-2026A",
            ClassName: "Maker Lab Cohort A",
            ScheduleSummary: "Saturday & Sunday 09:00-11:00",
            InProgressMentorCode: "MNT-004",
            OpenMentorCode: "MNT-006",
            TheoryModuleName: "Maker Mindset",
            ExperientialModuleName: "Prototype Sprint",
            ResearchModuleName: "Build Journal",
            TheoryCourseName: "Tools & Safety",
            ExperientialCourseName: "Build Lab",
            ResearchCourseName: "Prototype Journal Studio",
            QuizBankName: "Maker Mindset Question Bank",
            QuizTitle: "Maker Mindset Quiz",
            RetrospectiveTitle: "Prototype Sprint Retrospective",
            RetrospectiveDescription: "Reflect on your prototype: what worked, what broke, and what you would change.",
            Milestone1Title: "Prototype Photo Upload",
            Milestone1Description: "Upload photos of your first prototype and a short build note.",
            Milestone1AssignmentTitle: "Upload Prototype Photos",
            Milestone2Title: "Improved Build Upload",
            Milestone2Description: "Upload the improved prototype evidence and a short iteration note.",
            Milestone2AssignmentTitle: "Upload Improved Build",
            BankQuestions: MakerDemoBankQuestions),
    ];

    // Mostly easy (difficulty 1-2) with a few medium (3) for demo draws.
    private static readonly (string Text, int Difficulty, string[] Options)[] ScratchDemoBankQuestions =
    [
        ("In Scratch, a character on the stage is called a", 1, ["Sprite", "Sensor", "Battery", "Router"]),
        ("What do you click to start most Scratch projects?", 1, ["The green flag", "The red stop only", "The trash can", "The backpack"]),
        ("A stack of Scratch blocks that runs in order is a", 1, ["Script", "Costume", "Backdrop", "Paintbrush"]),
        ("Which block category usually moves a sprite?", 1, ["Motion", "Looks", "Sound", "Events"]),
        ("The red stop sign in Scratch is used to", 1, ["Stop all scripts", "Change costumes", "Add a sprite", "Save the project"]),
        ("A loop block is useful when you want to", 2, ["Repeat actions", "Delete the stage", "Turn off sound forever", "Hide the green flag"]),
        ("Costumes in Scratch let a sprite", 2, ["Change how it looks", "Connect to Wi-Fi", "Print paper", "Charge a battery"]),
        ("The stage in Scratch is where", 2, ["Sprites perform the project", "You edit your password", "Files are encrypted", "Servers are hosted"]),
        ("An event block such as 'when green flag clicked' is used to", 2, ["Start a script", "Draw a circle only", "Delete variables", "Export PDF"]),
        ("A variable in Scratch stores", 3, ["A value you can change", "Only images", "Only music files", "The Wi-Fi password"]),
        ("Broadcast blocks help sprites", 3, ["Send messages to each other", "Charge batteries", "Print documents", "Open email"]),
        ("If a sprite goes to x:0 y:0 it moves toward the", 3, ["Center of the stage", "Top-left only", "Trash folder", "Sound library"]),
    ];

    private static readonly (string Text, int Difficulty, string[] Options)[] ClimateDemoBankQuestions =
    [
        ("Weather is best described as", 1, ["Day-to-day conditions outside", "Only the ocean depth", "A type of rock", "A computer program"]),
        ("Climate is best described as", 1, ["Average weather over many years", "One rainy afternoon", "A single thunderstorm", "Tonight's temperature only"]),
        ("The gas people often discuss in climate change is", 1, ["Carbon dioxide (CO2)", "Helium only", "Neon only", "Argon perfume"]),
        ("A thermometer is used to measure", 1, ["Temperature", "Wind color", "Soil taste", "Cloud password"]),
        ("Rain, snow, and hail are forms of", 1, ["Precipitation", "Gravity", "Magnetism", "Electricity"]),
        ("Trees help the climate because they can", 2, ["Absorb carbon dioxide", "Create plastic", "Stop the moon", "Delete clouds"]),
        ("A simple way to observe local climate clues is to", 2, ["Record temperature and rainfall", "Ignore the outdoors", "Only watch cartoons", "Turn off all clocks"]),
        ("Fossil fuels are commonly linked to", 2, ["Extra greenhouse gases", "Making more oxygen only", "Cooling the sun", "Stopping tides"]),
        ("The water cycle includes", 2, ["Evaporation and condensation", "Only earthquakes", "Only volcanoes", "Only Wi-Fi"]),
        ("An anemometer helps measure", 3, ["Wind speed", "Soil color", "Fish population", "Screen brightness"]),
        ("Sea-level rise is often linked to", 3, ["Melting ice and warmer oceans", "More desk lamps", "Faster Wi-Fi", "Louder music"]),
        ("A fair science observation should be", 3, ["Recorded carefully and honestly", "Guessed without notes", "Hidden from the team", "Changed to look nicer"]),
    ];

    private static readonly (string Text, int Difficulty, string[] Options)[] MakerDemoBankQuestions =
    [
        ("Before using tools in a maker lab you should", 1, ["Follow safety rules", "Run with scissors", "Ignore mentors", "Eat at the saw"]),
        ("Safety goggles protect your", 1, ["Eyes", "Shoes only", "Phone case", "Backpack"]),
        ("A prototype is", 1, ["An early version of an idea", "The final factory product only", "A password", "A Wi-Fi router"]),
        ("Iteration means you", 1, ["Improve a design step by step", "Never change anything", "Delete all notes", "Skip testing"]),
        ("Sharp tools should be", 1, ["Used carefully and stored safely", "Left on the floor", "Thrown to friends", "Kept in a pocket open"]),
        ("Cardboard is popular in maker labs because it is", 2, ["Easy to cut and reshape", "Made of metal", "Always waterproof forever", "A type of battery"]),
        ("Hot glue guns can", 2, ["Burn skin if you are not careful", "Cool the room", "Charge phones", "Print essays"]),
        ("A quick sketch helps makers", 2, ["Plan before building", "Skip all ideas", "Hide mistakes forever", "Turn off lights"]),
        ("Measuring before cutting helps you", 2, ["Avoid wasting materials", "Make louder noise", "Break more parts", "Skip safety"]),
        ("Feedback from a mentor is useful because it", 3, ["Helps you improve the prototype", "Deletes your project", "Stops all learning", "Removes safety rules"]),
        ("A bill of materials lists", 3, ["Parts you need for the build", "Only your lunch order", "Wi-Fi passwords", "Movie tickets"]),
        ("Failure during a prototype test often means", 3, ["You learned what to fix next", "You must quit forever", "Science is broken", "Tools are illegal"]),
    ];

    private async Task SeedOneDemoProgramAsync(
        DemoProgramDefinition definition,
        Guid inProgressMentorId,
        Guid openMentorId,
        DateTime seedTime)
    {
        var slug = definition.ProgramCode.Replace("PRG-DEMO-", string.Empty, StringComparison.OrdinalIgnoreCase);

        var program = await EnsureDemoProgramAsync(definition, seedTime);
        var theoryModule = await EnsureDemoModuleAsync(
            program.Id,
            $"MOD-DEMO-{slug}-01",
            definition.TheoryModuleName,
            ModuleType.Theory,
            moduleOrder: 1,
            price: 300_000m,
            seedTime);
        var experientialModule = await EnsureDemoModuleAsync(
            program.Id,
            $"MOD-DEMO-{slug}-02",
            definition.ExperientialModuleName,
            ModuleType.Experiential,
            moduleOrder: 2,
            price: 320_000m,
            seedTime,
            prerequisiteModuleId: theoryModule.Id);
        var researchModule = await EnsureDemoModuleAsync(
            program.Id,
            $"MOD-DEMO-{slug}-03",
            definition.ResearchModuleName,
            ModuleType.Research,
            moduleOrder: 3,
            price: 280_000m,
            seedTime,
            prerequisiteModuleId: experientialModule.Id);

        var theoryCourse = await EnsureDemoCourseAsync(
            theoryModule.Id,
            $"CRS-DEMO-{slug}-01",
            definition.TheoryCourseName,
            "Short theory course for the demo track.",
            seedTime);
        var experientialCourse = await EnsureDemoCourseAsync(
            experientialModule.Id,
            $"CRS-DEMO-{slug}-02",
            definition.ExperientialCourseName,
            "Hands-on course for the demo track.",
            seedTime);
        var researchCourse = await EnsureDemoCourseAsync(
            researchModule.Id,
            $"CRS-DEMO-{slug}-03",
            definition.ResearchCourseName,
            "Research course with milestone deliverables.",
            seedTime);

        // Theory allows SelfPaced + LiveOnline only (no Offline).
        var theorySelfPaced = await EnsureDemoActivityAsync(
            theoryCourse.Id,
            $"ACT-DEMO-{slug}-01-01",
            $"{definition.TheoryCourseName} Reading",
            ActivityType.SelfPaced,
            1,
            "Self-paced intro reading for the demo theory course.",
            null,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var theoryLive = await EnsureDemoActivityAsync(
            theoryCourse.Id,
            $"ACT-DEMO-{slug}-01-02",
            $"{definition.TheoryCourseName} Live Session",
            ActivityType.LiveOnline,
            2,
            "Live online walkthrough of key ideas.",
            120,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);

        var experientialSelfPaced = await EnsureDemoActivityAsync(
            experientialCourse.Id,
            $"ACT-DEMO-{slug}-02-01",
            $"{definition.ExperientialCourseName} Prep",
            ActivityType.SelfPaced,
            1,
            "Self-paced prep before the hands-on lab.",
            null,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var experientialLive = await EnsureDemoActivityAsync(
            experientialCourse.Id,
            $"ACT-DEMO-{slug}-02-02",
            $"{definition.ExperientialCourseName} Live Coaching",
            ActivityType.LiveOnline,
            2,
            "Live coaching session for the hands-on build.",
            120,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var experientialOffline = await EnsureDemoActivityAsync(
            experientialCourse.Id,
            $"ACT-DEMO-{slug}-02-03",
            $"{definition.ExperientialCourseName} Offline Lab",
            ActivityType.Offline,
            3,
            "On-site lab to practice skills and gather evidence.",
            180,
            requireQrCheckin: true,
            requireMediaEvidence: true,
            seedTime);

        var researchSelfPaced = await EnsureDemoActivityAsync(
            researchCourse.Id,
            $"ACT-DEMO-{slug}-03-01",
            $"{definition.ResearchCourseName} Brief",
            ActivityType.SelfPaced,
            1,
            "Self-paced research brief before milestone uploads.",
            null,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var researchLive = await EnsureDemoActivityAsync(
            researchCourse.Id,
            $"ACT-DEMO-{slug}-03-02",
            $"{definition.ResearchCourseName} Check-in",
            ActivityType.LiveOnline,
            2,
            "Live check-in before the first milestone upload.",
            60,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var researchOffline = await EnsureDemoActivityAsync(
            researchCourse.Id,
            $"ACT-DEMO-{slug}-03-03",
            $"{definition.ResearchCourseName} Showcase Lab",
            ActivityType.Offline,
            3,
            "On-site showcase lab for the final milestone.",
            180,
            requireQrCheckin: true,
            requireMediaEvidence: true,
            seedTime);

        // Reuse the same Seed/Material PDFs as robotics; experiential SelfPaced uses the new demo video.
        await EnsureDemoMaterialAsync(
            theorySelfPaced.Id,
            $"{definition.TheoryCourseName} Reading Pack",
            MaterialType.PDF,
            RoboticsTheoryMaterialUrl,
            4_200_000L,
            seedTime);
        await EnsureDemoMaterialAsync(
            experientialSelfPaced.Id,
            $"{definition.ExperientialCourseName} Prep Video",
            MaterialType.Video,
            DemoShowcaseVideoMaterialUrl,
            85_000_000L,
            seedTime);
        await EnsureDemoMaterialAsync(
            researchSelfPaced.Id,
            $"{definition.ResearchCourseName} Brief Pack",
            MaterialType.PDF,
            RoboticsResearchMaterialUrl,
            3_800_000L,
            seedTime);

        var bank = await EnsureDemoQuestionBankAsync(
            theoryCourse.Id,
            definition.QuizBankName,
            $"Easy demo question bank for {definition.Name}.",
            definition.BankQuestions,
            seedTime);

        await EnsureDemoQuizAsync(
            theoryModule.Id,
            theoryCourse.Id,
            bank.Id,
            $"ASG-DEMO-{slug}-QUIZ",
            definition.QuizTitle,
            seedTime);

        await EnsureDemoRetrospectiveAsync(
            experientialModule.Id,
            experientialCourse.Id,
            $"ASG-DEMO-{slug}-RETRO",
            definition.RetrospectiveTitle,
            definition.RetrospectiveDescription,
            seedTime);

        await EnsureDemoResearchMilestonesAsync(
            researchModule.Id,
            slug,
            definition,
            researchSelfPaced.Id,
            researchLive.Id,
            researchOffline.Id,
            seedTime);

        var classEntity = await EnsureDemoClassAsync(
            program.Id,
            inProgressMentorId,
            definition.ClassCode,
            definition.ClassName,
            definition.ScheduleSummary,
            seedTime,
            ClassStatus.InProgress,
            startDate: seedTime.AddDays(-7),
            endDate: seedTime.AddDays(60));

        await EnsureDemoClassSessionsAsync(
            classEntity,
            theoryModule.Id,
            experientialModule.Id,
            researchModule.Id,
            theoryLive,
            experientialLive,
            experientialOffline,
            researchLive,
            researchOffline,
            seedTime);

        // Open / not-started cohort for newly registered students to join.
        // A different mentor than Cohort A so concurrent load and Saturday slots do not stack.
        var openClassName = definition.ClassName.Contains("Cohort A", StringComparison.Ordinal)
            ? definition.ClassName.Replace("Cohort A", "Cohort B", StringComparison.Ordinal)
            : $"{definition.ClassName} (Open)";
        await EnsureDemoClassAsync(
            program.Id,
            openMentorId,
            definition.ResolveOpenClassCode(),
            openClassName,
            $"{definition.ScheduleSummary} (upcoming cohort)",
            seedTime,
            ClassStatus.Open,
            startDate: seedTime.AddDays(14),
            endDate: seedTime.AddDays(90));

        await PruneDemoStudentEnrollmentsAsync(program, classEntity);
        await EnsureDemoStudentEnrollmentsAsync(
            program,
            theoryModule,
            experientialModule,
            researchModule,
            theoryCourse,
            experientialCourse,
            researchCourse,
            classEntity,
            seedTime);
    }

    private async Task<Program> EnsureDemoProgramAsync(DemoProgramDefinition definition, DateTime seedTime)
    {
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == definition.ProgramCode && !p.IsDeleted);
        if (existing != null)
        {
            if (existing.RetakeFee == null)
            {
                existing.RetakeFee = CatalogRetakeFee(existing.Price);
                if (existing.RetakeFee != null)
                {
                    await _unitOfWork.Programs.Update(existing);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            return existing;
        }

        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = definition.ProgramCode,
            Name = definition.Name,
            SeriesName = definition.SeriesName,
            Description = definition.Description,
            Level = definition.Level,
            Category = definition.Category,
            EstimatedDuration = definition.EstimatedDuration,
            SkillsGained = definition.SkillsGained,
            Rating = 4.8m,
            TotalReviews = 12,
            ThumbnailUrl = definition.ThumbnailUrl,
            Status = ProgramStatus.Active,
            Price = definition.Price,
            RetakeFee = CatalogRetakeFee(definition.Price),
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Programs.AddAsync(program);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Seeded demo program {Code}.", program.Code);
        return program;
    }

    private async Task<Module> EnsureDemoModuleAsync(
        Guid programId,
        string code,
        string name,
        ModuleType moduleType,
        int moduleOrder,
        decimal price,
        DateTime seedTime,
        Guid? prerequisiteModuleId = null)
    {
        _ = price;
        var existing = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == code && !m.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var module = new Module
        {
            Id = Guid.NewGuid(),
            Code = code,
            ProgramId = programId,
            Name = name,
            ModuleType = moduleType,
            ModuleOrder = moduleOrder,
            PrerequisiteModuleId = prerequisiteModuleId,
            IsMandatory = true,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Modules.AddAsync(module);
        await _unitOfWork.SaveChangesAsync();
        return module;
    }

    private async Task<Course> EnsureDemoCourseAsync(
        Guid moduleId,
        string code,
        string name,
        string description,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == code && !c.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            Name = name,
            Description = description,
            // Demo tracks create one course per module.
            CourseOrder = 1,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();
        return course;
    }

    private async Task<Activity> EnsureDemoActivityAsync(
        Guid courseId,
        string code,
        string name,
        ActivityType activityType,
        int activityOrder,
        string description,
        int? durationMinutes,
        bool requireQrCheckin,
        bool requireMediaEvidence,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            var needsUpdate =
                existing.DurationMinutes != durationMinutes
                || existing.RequireQrCheckin != requireQrCheckin
                || existing.RequireMediaEvidence != requireMediaEvidence;

            if (!needsUpdate)
            {
                return existing;
            }

            existing.DurationMinutes = durationMinutes;
            existing.RequireQrCheckin = requireQrCheckin;
            existing.RequireMediaEvidence = requireMediaEvidence;
            existing.UpdatedAt = seedTime;
            existing.UpdatedBy = Guid.Empty;
            await _unitOfWork.Activities.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return existing;
        }

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Code = code,
            CourseId = courseId,
            Name = name,
            ActivityType = activityType,
            Description = description,
            ActivityOrder = activityOrder,
            DurationMinutes = durationMinutes,
            RequireQrCheckin = requireQrCheckin,
            RequireMediaEvidence = requireMediaEvidence,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Activities.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();
        return activity;
    }

    private async Task EnsureDemoMaterialAsync(
        Guid activityId,
        string title,
        MaterialType materialType,
        string fileUrl,
        long fileSizeBytes,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == activityId);
        if (existing != null)
        {
            if (existing.Title == title
                && existing.MaterialType == materialType
                && existing.FileUrl == fileUrl
                && existing.FileSizeBytes == fileSizeBytes)
            {
                return;
            }

            existing.Title = title;
            existing.MaterialType = materialType;
            existing.FileUrl = fileUrl;
            existing.FileSizeBytes = fileSizeBytes;
            existing.UpdatedAt = seedTime;
            existing.UpdatedBy = Guid.Empty;
            await _unitOfWork.Materials.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        await _unitOfWork.Materials.AddAsync(new Material
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            Title = title,
            MaterialType = materialType,
            FileUrl = fileUrl,
            FileSizeBytes = fileSizeBytes,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<QuestionBank> EnsureDemoQuestionBankAsync(
        Guid courseId,
        string name,
        string description,
        (string Text, int Difficulty, string[] Options)[] questions,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.QuestionBanks.FirstOrDefaultAsync(
            qb => qb.CourseId == courseId && qb.Name == name && !qb.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var bank = new QuestionBank
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = name,
            Description = description,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.QuestionBanks.AddAsync(bank);
        await _unitOfWork.SaveChangesAsync();

        var bankQuestions = new List<BankQuestion>();
        var orderIndex = 1;
        foreach (var question in questions)
        {
            bankQuestions.Add(new BankQuestion
            {
                Id = Guid.NewGuid(),
                QuestionBankId = bank.Id,
                QuestionText = question.Text,
                QuestionType = QuestionTypeConstants.SingleChoice,
                Points = 1,
                DifficultyLevel = question.Difficulty,
                OrderIndex = orderIndex++,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        await _unitOfWork.BankQuestions.AddRangeAsync(bankQuestions);
        await _unitOfWork.SaveChangesAsync();

        var options = new List<BankQuestionOption>();
        for (var i = 0; i < questions.Length; i++)
        {
            var question = questions[i];
            var bankQuestion = bankQuestions[i];
            for (var j = 0; j < question.Options.Length; j++)
            {
                options.Add(new BankQuestionOption
                {
                    Id = Guid.NewGuid(),
                    BankQuestionId = bankQuestion.Id,
                    OptionText = question.Options[j],
                    IsCorrect = j == 0,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        await _unitOfWork.BankQuestionOptions.AddRangeAsync(options);
        await _unitOfWork.SaveChangesAsync();
        return bank;
    }

    private async Task EnsureDemoQuizAsync(
        Guid moduleId,
        Guid courseId,
        Guid questionBankId,
        string code,
        string title,
        DateTime seedTime)
    {
        if (await AssignmentCodeExistsAsync(code))
        {
            return;
        }

        await _unitOfWork.Assignments.AddAsync(new Assignment
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            CourseId = courseId,
            Title = title,
            Description = "Easy demo quiz drawn from the course question bank.",
            AssignmentType = AssignmentType.Quiz,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            AllowShuffle = true,
            ShuffleOptions = true,
            QuestionBankId = questionBankId,
            QuestionCount = 5,
            EasyPercent = 80,
            MediumPercent = 20,
            HardPercent = 0,
            TimeLimitMinutes = 15,
            MaxAttempts = 3,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureDemoRetrospectiveAsync(
        Guid moduleId,
        Guid courseId,
        string code,
        string title,
        string description,
        DateTime seedTime)
    {
        if (await AssignmentCodeExistsAsync(code))
        {
            return;
        }

        await _unitOfWork.Assignments.AddAsync(new Assignment
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            CourseId = courseId,
            Title = title,
            Description = description,
            AssignmentType = AssignmentType.Retrospective,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            AllowShuffle = false,
            MaxAttempts = 2,
            TimeLimitMinutes = 60,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureDemoResearchMilestonesAsync(
        Guid researchModuleId,
        string slug,
        DemoProgramDefinition definition,
        Guid researchSelfPacedId,
        Guid researchLiveId,
        Guid researchOfflineId,
        DateTime seedTime)
    {
        var milestone1Code = $"RML-DEMO-{slug}-01";
        var existingMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == milestone1Code && !rm.IsDeleted);
        if (existingMilestone != null)
        {
            return;
        }

        var assignment1Code = $"ASG-DEMO-{slug}-MS01";
        var assignment2Code = $"ASG-DEMO-{slug}-MS02";

        var assignment1 = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = assignment1Code,
            ModuleId = researchModuleId,
            Title = definition.Milestone1AssignmentTitle,
            Description = definition.Milestone1Description,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            MaxAttempts = 3,
            TimeLimitMinutes = 60,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        var assignment2 = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = assignment2Code,
            ModuleId = researchModuleId,
            Title = definition.Milestone2AssignmentTitle,
            Description = definition.Milestone2Description,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            MaxAttempts = 3,
            TimeLimitMinutes = 60,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        var milestone1 = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = milestone1Code,
            ModuleId = researchModuleId,
            Title = definition.Milestone1Title,
            Description = definition.Milestone1Description,
            MilestoneOrder = 1,
            IsCapstone = false,
            AssignmentId = assignment1.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        var milestone2 = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = $"RML-DEMO-{slug}-02",
            ModuleId = researchModuleId,
            Title = definition.Milestone2Title,
            Description = definition.Milestone2Description,
            MilestoneOrder = 2,
            IsCapstone = true,
            AssignmentId = assignment2.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Assignments.AddRangeAsync([assignment1, assignment2]);
        await _unitOfWork.ResearchMilestones.AddRangeAsync([milestone1, milestone2]);
        await _unitOfWork.SaveChangesAsync();

        var links = new List<ResearchMilestoneActivity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestone1.Id,
                ActivityId = researchSelfPacedId,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestone1.Id,
                ActivityId = researchLiveId,
                IsRequiredForSubmission = true,
                DisplayOrder = 2,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestone2.Id,
                ActivityId = researchOfflineId,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
        };

        await _unitOfWork.ResearchMilestoneActivities.AddRangeAsync(links);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Class> EnsureDemoClassAsync(
        Guid programId,
        Guid mentorId,
        string classCode,
        string className,
        string scheduleSummary,
        DateTime seedTime,
        ClassStatus status,
        DateTime startDate,
        DateTime endDate)
    {
        var existing = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == classCode && !c.IsDeleted);
        if (existing != null)
        {
            var needsUpdate =
                existing.MentorId != mentorId
                || existing.Status != status
                || existing.StartDate != startDate
                || existing.EndDate != endDate
                || existing.ScheduleSummary != scheduleSummary
                || existing.Name != className;

            if (!needsUpdate)
            {
                return existing;
            }

            existing.Name = className;
            existing.MentorId = mentorId;
            existing.Status = status;
            existing.StartDate = startDate;
            existing.EndDate = endDate;
            existing.ScheduleSummary = scheduleSummary;
            existing.UpdatedAt = seedTime;
            existing.UpdatedBy = Guid.Empty;
            await _unitOfWork.Classes.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return existing;
        }

        var classEntity = new Class
        {
            Id = Guid.NewGuid(),
            Code = classCode,
            Name = className,
            ProgramId = programId,
            MentorId = mentorId,
            StartDate = startDate,
            EndDate = endDate,
            MaxCapacity = 20,
            Status = status,
            MinHoursBeforeAssignmentJoin = 48,
            ScheduleSummary = scheduleSummary,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Classes.AddAsync(classEntity);
        await _unitOfWork.SaveChangesAsync();
        return classEntity;
    }

    private async Task EnsureDemoClassSessionsAsync(
        Class classEntity,
        Guid theoryModuleId,
        Guid experientialModuleId,
        Guid researchModuleId,
        Activity theoryLive,
        Activity experientialLive,
        Activity experientialOffline,
        Activity researchLive,
        Activity researchOffline,
        DateTime seedTime)
    {
        var sessionDefs = new (Guid ModuleId, Activity Activity, SessionKind Kind, string Title)[]
        {
            (theoryModuleId, theoryLive, SessionKind.LiveOnline, theoryLive.Name),
            (experientialModuleId, experientialLive, SessionKind.LiveOnline, experientialLive.Name),
            (experientialModuleId, experientialOffline, SessionKind.Offline, experientialOffline.Name),
            (researchModuleId, researchLive, SessionKind.LiveOnline, researchLive.Name),
            (researchModuleId, researchOffline, SessionKind.Offline, researchOffline.Name),
        };

        var sessionsToAdd = new List<ClassSession>();
        var sessionIndex = 0;

        foreach (var definition in sessionDefs)
        {
            var existing = await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                cs => cs.ClassId == classEntity.Id
                      && cs.ActivityId == definition.Activity.Id
                      && !cs.IsDeleted);

            var slot = SeedTimeline.TryResolveSlotSequence(
                classEntity.StartDate,
                classEntity.EndDate,
                DemoSatSunMorning,
                sessionIndex);
            sessionIndex++;
            if (slot == null)
            {
                continue;
            }

            var startTime = slot.Value.StartTime;
            var endTime = startTime.AddMinutes(definition.Activity.DurationMinutes ?? 120);
            var status = SeedTimeline.ResolveSessionStatus(startTime, endTime, seedTime);
            var (location, meetingUrl, latitude, longitude) = SeedTimeline.ResolveSeedVenue(
                definition.Kind,
                classEntity.Code,
                sessionIndex - 1);

            if (existing != null)
            {
                existing.Status = status;
                existing.StartTime = startTime;
                existing.EndTime = endTime;
                existing.Location = location;
                existing.MeetingUrl = meetingUrl;
                existing.Latitude = latitude;
                existing.Longitude = longitude;
                existing.UpdatedAt = seedTime;
                existing.UpdatedBy = Guid.Empty;
                await _unitOfWork.ClassSessions.Update(existing);
                continue;
            }

            sessionsToAdd.Add(new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = classEntity.Id,
                ModuleId = definition.ModuleId,
                ActivityId = definition.Activity.Id,
                SessionKind = definition.Kind,
                Title = definition.Title,
                Description = definition.Activity.Description,
                StartTime = startTime,
                EndTime = endTime,
                Location = location,
                MeetingUrl = meetingUrl,
                Latitude = latitude,
                Longitude = longitude,
                RequiresAttendance = true,
                RequiresMentorCheckIn = definition.Activity.ActivityType == ActivityType.Offline,
                Status = status,
                CreatedAt = classEntity.CreatedAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (sessionsToAdd.Count > 0)
        {
            await _unitOfWork.ClassSessions.AddRangeAsync(sessionsToAdd);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Maker Cohort A only: pin experiential LiveOnline + Offline to <see cref="_seedNow"/>
    /// so Slice 2 JaaS join and QR check-in are immediately testable after every reseed.
    /// Leaves Scratch/Climate and all academic-year classes on the calendar grid.
    /// Must run after <c>RealignSeedSessionWallClocksAsync</c>.
    /// </summary>
    private async Task ApplyMakerSlice2JoinableSessionsAsync()
    {
        const string makerClassCode = "CLS-DEMO-MAKER-2026A";
        const string liveActivityCode = "ACT-DEMO-MAKER-02-02";
        const string offlineActivityCode = "ACT-DEMO-MAKER-02-03";

        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == makerClassCode && !c.IsDeleted);
        if (classEntity == null)
        {
            _loggerService.LogWarning(
                "Maker Slice-2 joinable sessions skipped: class {ClassCode} not found.",
                makerClassCode);
            return;
        }

        var activities = await _unitOfWork.Activities.GetAllAsync(
            a => (a.Code == liveActivityCode || a.Code == offlineActivityCode) && !a.IsDeleted);
        var liveActivity = activities.FirstOrDefault(a =>
            string.Equals(a.Code, liveActivityCode, StringComparison.OrdinalIgnoreCase));
        var offlineActivity = activities.FirstOrDefault(a =>
            string.Equals(a.Code, offlineActivityCode, StringComparison.OrdinalIgnoreCase));
        if (liveActivity == null || offlineActivity == null)
        {
            _loggerService.LogWarning(
                "Maker Slice-2 joinable sessions skipped: activities {Live} / {Offline} not found.",
                liveActivityCode,
                offlineActivityCode);
            return;
        }

        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classEntity.Id
                  && !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && cs.ActivityId != null
                  && (cs.ActivityId == liveActivity.Id || cs.ActivityId == offlineActivity.Id));

        var liveSession = sessions.FirstOrDefault(cs => cs.ActivityId == liveActivity.Id);
        var offlineSession = sessions.FirstOrDefault(cs => cs.ActivityId == offlineActivity.Id);
        if (liveSession == null || offlineSession == null)
        {
            _loggerService.LogWarning(
                "Maker Slice-2 joinable sessions skipped: LiveOnline/Offline class sessions missing on {ClassCode}.",
                makerClassCode);
            return;
        }

        var seedTime = _seedNow;
        var updated = 0;

        // LiveOnline: start in 5 minutes → join window already open (opens 15 min before start).
        var liveDuration = liveActivity.DurationMinutes is > 0
            ? liveActivity.DurationMinutes.Value
            : 120;
        var liveStart = seedTime.AddMinutes(5);
        var liveEnd = liveStart.AddMinutes(liveDuration);
        updated += await ApplyMakerJoinableClockAsync(
            liveSession,
            SessionKind.LiveOnline,
            liveStart,
            liveEnd,
            classEntity.Code,
            ordinal: 1,
            seedTime);

        // Offline: already started → mentor can mint QR; student can check in.
        var offlineDuration = offlineActivity.DurationMinutes is > 0
            ? offlineActivity.DurationMinutes.Value
            : 180;
        var offlineStart = seedTime.AddMinutes(-5);
        var offlineEnd = offlineStart.AddMinutes(offlineDuration);
        updated += await ApplyMakerJoinableClockAsync(
            offlineSession,
            SessionKind.Offline,
            offlineStart,
            offlineEnd,
            classEntity.Code,
            ordinal: 2,
            seedTime);

        if (updated > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Maker Slice-2 joinable sessions: refreshed {Count} session(s) on {ClassCode} for FE join/check-in.",
            updated,
            makerClassCode);
    }

    private async Task<int> ApplyMakerJoinableClockAsync(
        ClassSession session,
        SessionKind kind,
        DateTime startTime,
        DateTime endTime,
        string classCode,
        int ordinal,
        DateTime seedTime)
    {
        var status = SeedTimeline.ResolveSessionStatus(startTime, endTime, seedTime);
        var (location, meetingUrl, latitude, longitude) = SeedTimeline.ResolveSeedVenue(
            kind,
            classCode,
            ordinal);

        if (session.StartTime == startTime
            && session.EndTime == endTime
            && session.Status == status
            && session.Location == location
            && session.MeetingUrl == meetingUrl
            && session.Latitude == latitude
            && session.Longitude == longitude)
        {
            return 0;
        }

        session.StartTime = startTime;
        session.EndTime = endTime;
        session.Status = status;
        session.Location = location;
        session.MeetingUrl = meetingUrl;
        session.Latitude = latitude;
        session.Longitude = longitude;
        session.UpdatedAt = seedTime;
        session.UpdatedBy = Guid.Empty;
        await _unitOfWork.ClassSessions.Update(session);
        return 1;
    }

    /// <summary>
    /// STD-010 on Maker only: complete Theory (module 1) and the Module 2 SelfPaced prep
    /// so FE can open the Slice-2 LiveOnline / Offline pair immediately after reseed.
    /// Runs after <c>ClearDemoProgramSubmissionsAsync</c> so the theory quiz grade survives.
    /// </summary>
    private async Task ApplyMakerStudent10Module1CompleteAsync()
    {
        const string studentCode = "STD-010";
        const string programCode = "PRG-DEMO-MAKER";
        const string theoryModuleCode = "MOD-DEMO-MAKER-01";
        const string experientialModuleCode = "MOD-DEMO-MAKER-02";
        const string theorySelfPacedCode = "ACT-DEMO-MAKER-01-01";
        const string theoryLiveCode = "ACT-DEMO-MAKER-01-02";
        const string experientialPrepCode = "ACT-DEMO-MAKER-02-01";
        const string theoryQuizCode = "ASG-DEMO-MAKER-QUIZ";

        var student = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == studentCode && !u.IsDeleted);
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == programCode && !p.IsDeleted);
        if (student == null || program == null)
        {
            _loggerService.LogWarning(
                "Maker STD-010 module-1 complete skipped: student or program missing.");
            return;
        }

        var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted);
        if (programEnrollment == null)
        {
            _loggerService.LogWarning(
                "Maker STD-010 module-1 complete skipped: program enrollment missing.");
            return;
        }

        var theoryModule = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == theoryModuleCode && !m.IsDeleted);
        var experientialModule = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == experientialModuleCode && !m.IsDeleted);
        if (theoryModule == null || experientialModule == null)
        {
            _loggerService.LogWarning(
                "Maker STD-010 module-1 complete skipped: theory/experiential module missing.");
            return;
        }

        var theoryMe = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == theoryModule.Id
                  && me.ProgramEnrollmentId == programEnrollment.Id
                  && !me.IsDeleted);
        var experientialMe = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == experientialModule.Id
                  && me.ProgramEnrollmentId == programEnrollment.Id
                  && !me.IsDeleted);
        if (theoryMe == null || experientialMe == null)
        {
            _loggerService.LogWarning(
                "Maker STD-010 module-1 complete skipped: module enrollments missing.");
            return;
        }

        var activityCodes = new[] { theorySelfPacedCode, theoryLiveCode, experientialPrepCode };
        var activities = await _unitOfWork.Activities.GetAllAsync(
            a => activityCodes.Contains(a.Code) && !a.IsDeleted);
        var byCode = activities.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);

        if (!byCode.TryGetValue(theorySelfPacedCode, out var theorySelfPaced)
            || !byCode.TryGetValue(theoryLiveCode, out var theoryLive)
            || !byCode.TryGetValue(experientialPrepCode, out var experientialPrep))
        {
            _loggerService.LogWarning(
                "Maker STD-010 module-1 complete skipped: expected activities missing.");
            return;
        }

        var quiz = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == theoryQuizCode && !a.IsDeleted);
        if (quiz == null)
        {
            _loggerService.LogWarning(
                "Maker STD-010 module-1 complete skipped: quiz {QuizCode} missing.",
                theoryQuizCode);
            return;
        }

        var seedTime = _seedNow;
        await EnsureMakerSeedActivityDoneAsync(theoryMe, theorySelfPaced, seedTime.AddDays(-3));
        await EnsureMakerSeedActivityDoneAsync(theoryMe, theoryLive, seedTime.AddDays(-2));
        await EnsureMakerSeedActivityDoneAsync(experientialMe, experientialPrep, seedTime.AddDays(-1));
        await EnsureMakerSeedQuizPassedAsync(student, theoryMe, quiz, seedTime);

        await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, theoryMe);
        await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, experientialMe);
        await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
            _unitOfWork,
            programEnrollment.Id,
            theoryMe);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Maker STD-010: Theory module completed ({Progress}%) and Module-2 prep marked Done for Slice-2 FE testing.",
            theoryMe.ProgressPercent);
    }

    private async Task EnsureMakerSeedActivityDoneAsync(
        ModuleEnrollment moduleEnrollment,
        Activity activity,
        DateTime completedAt)
    {
        var existing = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                  && ap.ActivityId == activity.Id
                  && !ap.IsDeleted);

        if (existing != null)
        {
            if (existing.ActivityStatus == ActivityStatus.Done && existing.IsCompleted)
            {
                return;
            }

            existing.ActivityStatus = ActivityStatus.Done;
            existing.IsCompleted = true;
            existing.CompletionSource = CompletionSource.Manual;
            existing.CompletedAt ??= completedAt;
            existing.LastAccessedAt = completedAt;
            existing.UpdatedAt = completedAt;
            existing.UpdatedBy = Guid.Empty;
            await _unitOfWork.ActivityProgresses.Update(existing);
            return;
        }

        await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = moduleEnrollment.StudentId,
            ActivityId = activity.Id,
            ModuleEnrollmentId = moduleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            CompletionSource = CompletionSource.Manual,
            CompletedAt = completedAt,
            LastAccessedAt = completedAt,
            CreatedAt = completedAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task EnsureMakerSeedQuizPassedAsync(
        User student,
        ModuleEnrollment moduleEnrollment,
        Assignment quiz,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.StudentId == student.Id
                 && s.AssignmentId == quiz.Id
                 && s.ModuleEnrollmentId == moduleEnrollment.Id
                 && !s.IsDeleted);

        if (existing != null)
        {
            var needsUpdate = existing.Status != SubmissionStatus.Graded
                              || existing.AssignedGrade is null
                              || existing.AssignedGrade < quiz.PassScore;
            if (!needsUpdate)
            {
                return;
            }

            existing.Status = SubmissionStatus.Graded;
            existing.AssignedGrade = Math.Max(quiz.PassScore, 90m);
            existing.SubmittedAt ??= seedTime.AddDays(-2);
            existing.GradedAt = seedTime.AddDays(-1);
            existing.UpdatedAt = seedTime;
            existing.UpdatedBy = Guid.Empty;
            await _unitOfWork.Submissions.Update(existing);
            return;
        }

        await _unitOfWork.Submissions.AddAsync(new Submission
        {
            Id = Guid.NewGuid(),
            Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
            AssignmentId = quiz.Id,
            StudentId = student.Id,
            ModuleEnrollmentId = moduleEnrollment.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 90m,
            ContentText = "Seeded Maker theory quiz pass for Slice-2 FE testing (STD-010).",
            SubmittedAt = seedTime.AddDays(-2),
            GradedAt = seedTime.AddDays(-1),
            CreatedAt = seedTime.AddDays(-2),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task PruneDemoStudentEnrollmentsAsync(
        Program program,
        Class classEntity)
    {
        if (!DemoStudentCodesByProgram.TryGetValue(program.Code, out var allowedCodes)
            || allowedCodes.Length == 0)
        {
            return;
        }

        var allowed = allowedCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.ProgramId == program.Id && !pe.IsDeleted,
            pe => pe.Student);

        foreach (var enrollment in enrollments)
        {
            var code = enrollment.Student?.Code;
            if (!string.IsNullOrWhiteSpace(code) && allowed.Contains(code))
            {
                continue;
            }

            var classEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
                ce => ce.StudentId == enrollment.StudentId
                      && ce.ClassId == classEntity.Id
                      && !ce.IsDeleted);
            foreach (var classEnrollment in classEnrollments)
            {
                await _unitOfWork.ClassEnrollments.SoftRemove(classEnrollment);
            }

            var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.StudentId == enrollment.StudentId
                      && me.ProgramEnrollmentId == enrollment.Id
                      && !me.IsDeleted);
            foreach (var moduleEnrollment in moduleEnrollments)
            {
                await _unitOfWork.ModuleEnrollments.SoftRemove(moduleEnrollment);
            }

            await _unitOfWork.ProgramEnrollments.SoftRemove(enrollment);

            _loggerService.LogInformation(
                "Pruned demo enrollment for student {StudentId} on {ProgramCode} (over student load limit).",
                enrollment.StudentId,
                program.Code);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureDemoStudentEnrollmentsAsync(
        Program program,
        Module theoryModule,
        Module experientialModule,
        Module researchModule,
        Course theoryCourse,
        Course experientialCourse,
        Course researchCourse,
        Class classEntity,
        DateTime seedTime)
    {
        if (!DemoStudentCodesByProgram.TryGetValue(program.Code, out var studentCodes)
            || studentCodes.Length == 0)
        {
            _loggerService.LogWarning(
                "No demo student roster for {ProgramCode}. Skipping enrollments.",
                program.Code);
            return;
        }

        var modules = new[] { theoryModule, experientialModule, researchModule };
        var courses = new[] { theoryCourse, experientialCourse, researchCourse };

        foreach (var studentCode in studentCodes)
        {
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode && !u.IsDeleted);
            if (student == null)
            {
                _loggerService.LogWarning("Student {StudentCode} not found for demo enrollments.", studentCode);
                continue;
            }

            var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted);

            if (programEnrollment == null)
            {
                programEnrollment = new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ProgramId = program.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = seedTime.AddDays(-5),
                    StartedAt = seedTime.AddDays(-4),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                };
                await _unitOfWork.ProgramEnrollments.AddAsync(programEnrollment);
                await _unitOfWork.SaveChangesAsync();
            }

            foreach (var module in modules)
            {
                var moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student.Id && me.ModuleId == module.Id && !me.IsDeleted);
                if (moduleEnrollment != null)
                {
                    continue;
                }

                await _unitOfWork.ModuleEnrollments.AddAsync(new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ModuleId = module.Id,
                    ProgramEnrollmentId = programEnrollment.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = seedTime.AddDays(-4),
                    StartedAt = seedTime.AddDays(-3),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            await _unitOfWork.SaveChangesAsync();

            foreach (var course in courses)
            {
                var courseEnrollment = await _unitOfWork.CourseEnrollments.FirstOrDefaultAsync(
                    ce => ce.StudentId == student.Id && ce.CourseId == course.Id && !ce.IsDeleted);
                if (courseEnrollment != null)
                {
                    continue;
                }

                await _unitOfWork.CourseEnrollments.AddAsync(new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    CourseId = course.Id,
                    Status = EnrollmentStatus.Active,
                    JoinedAt = seedTime.AddDays(-4),
                    StartedAt = seedTime.AddDays(-3),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            await _unitOfWork.SaveChangesAsync();

            var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.StudentId == student.Id && ce.ClassId == classEntity.Id && !ce.IsDeleted);
            if (classEnrollment != null)
            {
                continue;
            }

            await _unitOfWork.ClassEnrollments.AddAsync(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = classEntity.Id,
                StudentId = student.Id,
                ProgramEnrollmentId = programEnrollment.Id,
                Status = ClassEnrollmentStatus.Active,
                EnrolledAt = seedTime.AddDays(-3),
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
