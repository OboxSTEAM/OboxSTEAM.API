using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Idempotent capstone demo programs: one lean curriculum shape (Theory, Experiential, Research;
/// SelfPaced, LiveOnline, Offline) shared by an Open class to buy (Flow 1) and an InProgress
/// class with a live LiveOnline + Offline pair (Flows 2 and 3).
/// </summary>
public partial class SeedService
{
    internal static IReadOnlyDictionary<string, string[]> GetDemoStudentCodesByProgram()
        => GetDemoProgramDefinitions().ToDictionary(
            d => d.ProgramCode,
            d => d.AllStudentCodes,
            StringComparer.OrdinalIgnoreCase);

    private async Task SeedDemoShowcaseProgramsAsync()
    {
        _loggerService.LogInformation("Starting seed capstone demo programs");

        var seedTime = _seedNow;
        var mentors = (await _unitOfWork.Users.GetAllAsync(u => u.Role == RoleType.Mentor && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in GetDemoProgramDefinitions())
        {
            if (!mentors.TryGetValue(definition.MentorCode, out var mentor))
            {
                _loggerService.LogWarning(
                    "Skipping demo program {ProgramCode}: mentor {MentorCode} not found.",
                    definition.ProgramCode,
                    definition.MentorCode);
                continue;
            }

            await SeedOneDemoProgramAsync(definition, mentor.Id, seedTime);
        }

        // Demo programs stay submission-free until the capstone journey tail seeds the theory quiz.
        await ClearDemoProgramSubmissionsAsync();
        await EnsureCapstoneProgramBoardsAsync();

        _loggerService.LogInformation("Finished seed capstone demo programs");
    }

    private static HashSet<string> GetDemoProgramCodeSet()
        => GetDemoProgramDefinitions()
            .Select(d => d.ProgramCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Program ids for demo tracks. Used to keep dashboard/global seeds off live-demo data
    /// and to clear demo submissions before the capstone journey fixtures.
    /// </summary>
    private async Task<HashSet<Guid>> GetDemoProgramIdsAsync()
    {
        var demoProgramCodes = GetDemoProgramCodeSet();
        var demoPrograms = await _unitOfWork.Programs.GetAllAsync(
            p => demoProgramCodes.Contains(p.Code) && !p.IsDeleted);
        return demoPrograms.Select(p => p.Id).ToHashSet();
    }

    /// <summary>
    /// Programs excluded from taught-module safety-net and elapsed-window passes:
    /// capstone demos, ADV advisory, review drafts, and fail/rebuy QA.
    /// </summary>
    private async Task<HashSet<Guid>> GetGlobalAssessmentExcludedProgramIdsAsync()
    {
        var codes = GetDemoProgramCodeSet();
        codes.Add(SeedReviewDraftIotProgramCode);
        codes.Add(SeedReviewDraftCodeProgramCode);
        codes.Add(FailRebuyProgramCode);
        codes.Add(SeedAdvDraftAdviceCode);
        codes.Add(SeedAdvDraftFixCode);
        codes.Add(SeedAdvPendingCode);
        codes.Add(SeedAdvResubmitCode);
        codes.Add(SeedAdvApprovedCode);
        codes.Add(SeedAdvActiveCode);
        codes.Add(SeedAdvPinV1Code);
        codes.Add(SeedAdvShareACode);
        codes.Add(SeedAdvShareBCode);

        var programs = await _unitOfWork.Programs.GetAllAsync(
            p => codes.Contains(p.Code) && !p.IsDeleted);
        return programs.Select(p => p.Id).ToHashSet();
    }

    /// <summary>
    /// Removes any submissions on demo assignments so the track stays clean for live demos.
    /// Clears leftovers from prior manual testing and from global seeds that target every assignment.
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
            _loggerService.LogInformation("Demo programs have no submissions to clear.");
            return;
        }

        await _unitOfWork.Submissions.SoftRemoveRange(submissions);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Cleared {Count} submission(s) from demo program assignments.",
            submissions.Count);
    }

    private sealed record DemoMilestoneDefinition(string Title, string Description, string AssignmentTitle);

    /// <summary>Extra Open cohort shown on the program page (tuyển sinh), partly filled with paid students.</summary>
    private sealed record DemoOpenClassDefinition(
        string ClassCode,
        string ClassName,
        int StartDaysOffset,
        int EndDaysOffset,
        string ScheduleSummary,
        string MentorCode,
        SeedTimeline.WeekdaySlot[] WeeklySlots,
        string[] StudentCodes);

    private sealed record DemoProgramDefinition(
        string ProgramCode,
        string Slug,
        string Name,
        string SeriesName,
        string Description,
        DifficultyLevel Level,
        ProgramCategory Category,
        string EstimatedDuration,
        decimal Price,
        string ThumbnailUrl,
        string ClassCode,
        string ClassName,
        ClassStatus ClassStatus,
        int ClassStartDaysOffset,
        int ClassEndDaysOffset,
        string ScheduleSummary,
        string MentorCode,
        string[] StudentCodes,
        string TheoryModuleName,
        string ExperientialModuleName,
        string ResearchModuleName,
        string TheoryCourseName,
        string ExperientialCourseName,
        string ResearchCourseName,
        string TheoryReading1Name,
        string TheoryReading1File,
        string TheoryReading2Name,
        string TheoryReading2File,
        string ExperientialLiveName,
        string ExperientialOfflineName,
        string ResearchBriefName,
        string ResearchBriefFile,
        string ResearchOfflineName,
        string QuizBankName,
        string QuizTitle,
        string RetrospectiveTitle,
        string RetrospectiveDescription,
        DemoMilestoneDefinition DesignMilestone,
        DemoMilestoneDefinition PrototypeMilestone,
        DemoMilestoneDefinition CapstoneMilestone,
        (string Text, int Difficulty, string[] Options)[] BankQuestions,
        DemoOpenClassDefinition[] AdditionalOpenClasses)
    {
        public string[] AllStudentCodes
            => StudentCodes
                .Concat(AdditionalOpenClasses.SelectMany(c => c.StudentCodes))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        public string ModuleCode(int order) => $"MOD-CAP-{Slug}-{order:D2}";

        public string ActivityCode(int moduleOrder, int activityOrder)
            => $"ACT-CAP-{Slug}-{moduleOrder:D2}-{activityOrder:D2}";

        public string AssignmentCode(string suffix) => $"ASG-CAP-{Slug}-{suffix}";
    }

    private const string CapstoneBuyProgramCode = "PRG-CAP-AIROBOT";
    private const string CapstoneBuyClassCode = "CLS-CAP-AIROBOT-2026B";
    private const string CapstoneLiveProgramCode = "PRG-CAP-SMARTCITY";
    private const string CapstoneLiveClassCode = "CLS-CAP-SMARTCITY-2026A";

    private static IReadOnlyList<DemoProgramDefinition> GetDemoProgramDefinitions() =>
    [
        new(
            ProgramCode: CapstoneBuyProgramCode,
            Slug: "AIROBOT",
            Name: "AI Robotics Explorer",
            SeriesName: "Capstone Showcase",
            Description: "Build a small AI robot: learn how robots sense, think and act, code an obstacle-avoiding robot in the lab, then research and showcase your own robot project.",
            Level: DifficultyLevel.Beginner,
            Category: ProgramCategory.Technology,
            EstimatedDuration: "4 weeks at 3 hours a week",
            Price: 1_200_000m,
            ThumbnailUrl:
                "https://images.unsplash.com/photo-1485827404703-89b55fcc595e?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            ClassCode: CapstoneBuyClassCode,
            ClassName: "AI Robotics Explorer Cohort B",
            ClassStatus: ClassStatus.Open,
            ClassStartDaysOffset: 14,
            ClassEndDaysOffset: 42,
            ScheduleSummary: "Saturday & Sunday 09:00-11:00",
            MentorCode: "MNT-005",
            StudentCodes: [CapstoneTruongStudentCode, CapstoneLongStudentCode, CapstoneHoaStudentCode],
            TheoryModuleName: "Robotics & AI Foundations",
            ExperientialModuleName: "Robot Build Lab",
            ResearchModuleName: "AI Robot Research Project",
            TheoryCourseName: "Robots and Intelligent Machines",
            ExperientialCourseName: "AI Robot Workshop",
            ResearchCourseName: "Robot Research Studio",
            TheoryReading1Name: "What Is a Robot?",
            TheoryReading1File: "robot-1.pdf",
            TheoryReading2Name: "Robots and Artificial Intelligence",
            TheoryReading2File: "robotics-1.pdf",
            ExperientialLiveName: "Robot Coding Live Coaching",
            ExperientialOfflineName: "Obstacle-Avoiding Robot Lab",
            ResearchBriefName: "Robot Sensors Research Brief",
            ResearchBriefFile: "sensor-1.pdf",
            ResearchOfflineName: "AI Robot Project Showcase",
            QuizBankName: "Robotics & AI Foundations Question Bank",
            QuizTitle: "Robotics & AI Foundations Quiz",
            RetrospectiveTitle: "Obstacle Robot Lab Retrospective",
            RetrospectiveDescription: "Reflect on the lab: what your robot did well, which sensor reading surprised you, and what you would change next time.",
            DesignMilestone: new(
                "Design: Robot Problem and Sketch",
                "Choose a task your robot should solve, write a research question, and upload a sketch with the sensors and motors you plan to use.",
                "Upload Robot Design Plan"),
            PrototypeMilestone: new(
                "Prototype: First Robot Build",
                "Upload photos or a short video of your first robot build and notes from the first test run.",
                "Upload Robot Prototype Evidence"),
            CapstoneMilestone: new(
                "Capstone: Robot Final Report",
                "Upload your final report and showcase slides explaining how your robot works and what you learned.",
                "Upload Robot Final Report"),
            BankQuestions: AiRoboticsBankQuestions,
            AdditionalOpenClasses:
            [
                new(
                    "CLS-CAP-AIROBOT-2026C",
                    "AI Robotics Explorer Cohort C",
                    StartDaysOffset: 21,
                    EndDaysOffset: 49,
                    "Monday & Wednesday 14:00-16:00",
                    "MNT-009",
                    CapstoneMonWedAfternoon,
                    ["STD-053", "STD-054", "STD-055", "STD-056", "STD-057", "STD-058"]),
            ]),
        new(
            ProgramCode: CapstoneLiveProgramCode,
            Slug: "SMARTCITY",
            Name: "Smart City IoT Lab",
            SeriesName: "Capstone Showcase",
            Description: "Explore how sensors and the Internet of Things make cities smarter: learn IoT basics, build a smart traffic light in the lab, then research a real city problem.",
            Level: DifficultyLevel.Beginner,
            Category: ProgramCategory.Engineering,
            EstimatedDuration: "4 weeks at 3 hours a week",
            Price: 1_500_000m,
            ThumbnailUrl:
                "https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            ClassCode: CapstoneLiveClassCode,
            ClassName: "Smart City IoT Lab Cohort A",
            ClassStatus: ClassStatus.InProgress,
            ClassStartDaysOffset: -14,
            ClassEndDaysOffset: 14,
            ScheduleSummary: "Saturday & Sunday 09:00-11:00",
            MentorCode: CapstoneDemoMentorCode,
            StudentCodes:
            [
                CapstoneDriverStudentCode,
                CapstoneTruongStudentCode,
                CapstoneLongStudentCode,
                CapstoneHoaStudentCode,
            ],
            TheoryModuleName: "Smart City Foundations",
            ExperientialModuleName: "Smart City Build Lab",
            ResearchModuleName: "Smart City Research Project",
            TheoryCourseName: "Sensors & Connected Cities",
            ExperientialCourseName: "Connected Prototype Lab",
            ResearchCourseName: "City Problem Research Studio",
            TheoryReading1Name: "Smart Cities and the Internet of Things",
            TheoryReading1File: "smart-city-1.pdf",
            TheoryReading2Name: "IoT Devices Around the City",
            TheoryReading2File: "smart-iot-1.pdf",
            ExperientialLiveName: "Smart Sensor Live Coaching",
            ExperientialOfflineName: "Smart Traffic Light Lab",
            ResearchBriefName: "Smart City Research Project Guide",
            ResearchBriefFile: "smart-iot-2.pdf",
            ResearchOfflineName: "Smart City Project Showcase",
            QuizBankName: "Smart City Foundations Question Bank",
            QuizTitle: "Smart City Foundations Quiz",
            RetrospectiveTitle: "Smart Traffic Light Lab Retrospective",
            RetrospectiveDescription: "Reflect on the lab: what your team built, which sensor worked best, and what you would improve for a real street.",
            DesignMilestone: new(
                "Design: City Problem and Solution Sketch",
                "Pick one city problem (traffic, flooding, air quality or energy), write a research question, and upload a solution sketch with the sensors you plan to use.",
                "Upload Smart City Design Plan"),
            PrototypeMilestone: new(
                "Prototype: Sensor Build and First Test",
                "Upload photos of your smart city prototype and the first test data you collected.",
                "Upload Smart City Prototype Evidence"),
            CapstoneMilestone: new(
                "Capstone: Smart City Final Report",
                "Upload your final report and showcase slides explaining how your prototype helps the city.",
                "Upload Smart City Final Report"),
            BankQuestions: SmartCityBankQuestions,
            AdditionalOpenClasses:
            [
                new(
                    "CLS-CAP-SMARTCITY-2026B",
                    "Smart City IoT Lab Cohort B",
                    StartDaysOffset: 21,
                    EndDaysOffset: 49,
                    "Tuesday & Thursday 18:00-20:00",
                    "MNT-002",
                    CapstoneTueThuEvening,
                    ["STD-041", "STD-042", "STD-043", "STD-044", "STD-045", "STD-046", "STD-047"]),
                new(
                    "CLS-CAP-SMARTCITY-2026C",
                    "Smart City IoT Lab Cohort C",
                    StartDaysOffset: 28,
                    EndDaysOffset: 56,
                    "Saturday & Sunday 14:00-16:00",
                    "MNT-008",
                    CapstoneSatSunAfternoon,
                    ["STD-048", "STD-049", "STD-050", "STD-051", "STD-052"]),
            ]),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] CapstoneTueThuEvening =
    [
        new(DayOfWeek.Tuesday, 18, 0, 120),
        new(DayOfWeek.Thursday, 18, 0, 120),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] CapstoneSatSunAfternoon =
    [
        new(DayOfWeek.Saturday, 14, 0, 120),
        new(DayOfWeek.Sunday, 14, 0, 120),
    ];

    private static readonly SeedTimeline.WeekdaySlot[] CapstoneMonWedAfternoon =
    [
        new(DayOfWeek.Monday, 14, 0, 120),
        new(DayOfWeek.Wednesday, 14, 0, 120),
    ];

    // First option is the correct answer; options are shuffled at draw time. Difficulty 1-2 only (easy pool).
    private static readonly (string Text, int Difficulty, string[] Options)[] SmartCityBankQuestions =
    [
        ("IoT là viết tắt của cụm từ nào?", 1, ["Internet of Things", "Internet of Tools", "Input of Technology", "Index of Tasks"]),
        ("Thành phố thông minh dùng công nghệ để làm gì?", 1, ["Giúp cuộc sống người dân tốt hơn", "Làm thành phố ồn ào hơn", "Tắt hết điện vào ban ngày", "Đóng cửa công viên"]),
        ("Đèn giao thông thông minh giúp thành phố điều gì?", 1, ["Giảm ùn tắc giao thông", "Tăng tiếng ồn", "Tắt hết đèn đường", "Đóng cửa trường học"]),
        ("Cảm biến nhiệt độ dùng để đo gì?", 1, ["Nhiệt độ", "Màu sắc", "Âm nhạc", "Mật khẩu Wi-Fi"]),
        ("Thiết bị IoT thường kết nối với nhau qua đâu?", 1, ["Mạng Internet", "Dây phơi quần áo", "Hộp bút", "Quyển vở"]),
        ("Thùng rác thông minh có thể báo cho người thu gom khi nào?", 1, ["Khi thùng rác đã đầy", "Khi trời mưa", "Khi có người hát", "Khi đến giờ ăn trưa"]),
        ("Đèn đường thông minh tự bật khi nào?", 2, ["Khi trời tối", "Khi trời nắng to", "Khi có mưa đá", "Khi điện thoại hết pin"]),
        ("Cảm biến chất lượng không khí giúp chúng ta biết điều gì?", 2, ["Không khí sạch hay ô nhiễm", "Giờ tan học", "Giá vé xe buýt", "Tên đường phố"]),
        ("Bãi đỗ xe thông minh giúp tài xế làm gì?", 2, ["Tìm chỗ đỗ xe trống nhanh hơn", "Lái xe nhanh hơn", "Đổ xăng miễn phí", "Rửa xe tự động"]),
        ("Để tiết kiệm điện, ngôi nhà thông minh có thể làm gì?", 2, ["Tự tắt đèn khi không có người", "Bật tất cả đèn cả ngày", "Mở tủ lạnh liên tục", "Tắt Internet mãi mãi"]),
        ("Bộ vi điều khiển như Arduino đóng vai trò gì trong thiết bị IoT?", 2, ["Bộ não xử lý dữ liệu", "Vỏ bảo vệ", "Dây điện", "Pin dự phòng"]),
        ("Vì sao cần bảo vệ dữ liệu trong thành phố thông minh?", 2, ["Để giữ an toàn thông tin cá nhân", "Để máy chạy chậm hơn", "Để xóa hết dữ liệu", "Để không ai dùng được Internet"]),
    ];

    private static readonly (string Text, int Difficulty, string[] Options)[] AiRoboticsBankQuestions =
    [
        ("Robot là gì?", 1, ["Máy có thể tự thực hiện công việc", "Một loại trái cây", "Một bài hát", "Một loại bút chì"]),
        ("Bộ phận nào giúp robot \"nhìn\" thấy vật cản?", 1, ["Cảm biến", "Bánh xe", "Pin", "Vỏ nhựa"]),
        ("AI là viết tắt của cụm từ nào?", 1, ["Artificial Intelligence", "Auto Internet", "Active Input", "Art Image"]),
        ("Động cơ giúp robot làm gì?", 1, ["Di chuyển", "Ngủ", "Đọc sách", "Nghe nhạc"]),
        ("Robot cần gì để hoạt động?", 1, ["Nguồn điện hoặc pin", "Nước ngọt", "Kẹo", "Giấy màu"]),
        ("Robot hút bụi giúp gia đình làm việc gì?", 1, ["Dọn sạch sàn nhà", "Nấu cơm", "Tưới cây", "Giặt quần áo"]),
        ("Chu trình hoạt động cơ bản của robot là gì?", 2, ["Cảm nhận, suy nghĩ, hành động", "Ăn, ngủ, chơi", "Đọc, viết, vẽ", "Chạy, nhảy, bơi"]),
        ("Muốn robot làm theo ý mình, chúng ta cần làm gì?", 2, ["Lập trình cho robot", "Hát cho robot nghe", "Sơn màu cho robot", "Để robot ngoài nắng"]),
        ("AI giúp robot làm được điều gì?", 2, ["Nhận biết hình ảnh và ra quyết định", "Tự mọc thêm bánh xe", "Hoạt động không cần điện", "Biến thành con người"]),
        ("Cảm biến siêu âm giúp robot đo được gì?", 2, ["Khoảng cách tới vật cản", "Nhiệt độ cơ thể", "Mùi thức ăn", "Màu của bầu trời"]),
        ("Khi làm việc với robot, điều quan trọng nhất là gì?", 2, ["Tuân thủ quy tắc an toàn", "Chạy nhảy trong phòng", "Tháo pin khi robot đang chạy", "Đổ nước vào robot"]),
        ("Robot trong nhà máy thường được dùng để làm gì?", 2, ["Lắp ráp sản phẩm", "Đi học thay học sinh", "Xem phim", "Chơi bóng đá"]),
    ];

    private async Task SeedOneDemoProgramAsync(
        DemoProgramDefinition definition,
        Guid mentorId,
        DateTime seedTime)
    {
        var slug = definition.Slug;
        var program = await EnsureDemoProgramAsync(definition, seedTime);

        // Theory allows SelfPaced + LiveOnline only (no Offline).
        var theoryModule = await EnsureDemoModuleAsync(
            program.Id,
            definition.ModuleCode(1),
            definition.TheoryModuleName,
            ModuleType.Theory,
            moduleOrder: 1,
            seedTime);
        var experientialModule = await EnsureDemoModuleAsync(
            program.Id,
            definition.ModuleCode(2),
            definition.ExperientialModuleName,
            ModuleType.Experiential,
            moduleOrder: 2,
            seedTime,
            prerequisiteModuleId: theoryModule.Id);
        var researchModule = await EnsureDemoModuleAsync(
            program.Id,
            definition.ModuleCode(3),
            definition.ResearchModuleName,
            ModuleType.Research,
            moduleOrder: 3,
            seedTime,
            prerequisiteModuleId: experientialModule.Id);

        var theoryCourse = await EnsureDemoCourseAsync(
            theoryModule.Id,
            $"CRS-CAP-{slug}-01",
            definition.TheoryCourseName,
            "Self-paced readings and a short quiz.",
            seedTime);
        var experientialCourse = await EnsureDemoCourseAsync(
            experientialModule.Id,
            $"CRS-CAP-{slug}-02",
            definition.ExperientialCourseName,
            "Live coaching followed by an on-site lab co-taught by board experts.",
            seedTime);
        var researchCourse = await EnsureDemoCourseAsync(
            researchModule.Id,
            $"CRS-CAP-{slug}-03",
            definition.ResearchCourseName,
            "Research brief, project showcase and Design / Prototype / Capstone milestones.",
            seedTime);

        var theoryReading1 = await EnsureDemoActivityAsync(
            theoryCourse.Id,
            definition.ActivityCode(1, 1),
            definition.TheoryReading1Name,
            ActivityType.SelfPaced,
            1,
            "Self-paced reading that introduces the core ideas of the program.",
            null,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var theoryReading2 = await EnsureDemoActivityAsync(
            theoryCourse.Id,
            definition.ActivityCode(1, 2),
            definition.TheoryReading2Name,
            ActivityType.SelfPaced,
            2,
            "Self-paced reading on the devices and building blocks used in the lab.",
            null,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);

        var experientialLive = await EnsureDemoActivityAsync(
            experientialCourse.Id,
            definition.ActivityCode(2, 1),
            definition.ExperientialLiveName,
            ActivityType.LiveOnline,
            1,
            "Live online coaching to plan the hands-on build.",
            90,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var experientialOffline = await EnsureDemoActivityAsync(
            experientialCourse.Id,
            definition.ActivityCode(2, 2),
            definition.ExperientialOfflineName,
            ActivityType.Offline,
            2,
            "On-site lab co-taught by board experts. Check in with the session QR code.",
            180,
            requireQrCheckin: true,
            requireMediaEvidence: true,
            seedTime);

        var researchBrief = await EnsureDemoActivityAsync(
            researchCourse.Id,
            definition.ActivityCode(3, 1),
            definition.ResearchBriefName,
            ActivityType.SelfPaced,
            1,
            "Self-paced guide to planning, building and documenting your research project.",
            null,
            requireQrCheckin: false,
            requireMediaEvidence: false,
            seedTime);
        var researchOffline = await EnsureDemoActivityAsync(
            researchCourse.Id,
            definition.ActivityCode(3, 2),
            definition.ResearchOfflineName,
            ActivityType.Offline,
            2,
            "On-site showcase where teams present their final projects.",
            180,
            requireQrCheckin: true,
            requireMediaEvidence: true,
            seedTime);

        await EnsureDemoMaterialAsync(
            theoryReading1.Id,
            definition.TheoryReading1Name,
            MaterialType.PDF,
            CapstoneMaterialBaseUrl + definition.TheoryReading1File,
            2_500_000L,
            seedTime);
        await EnsureDemoMaterialAsync(
            theoryReading2.Id,
            definition.TheoryReading2Name,
            MaterialType.PDF,
            CapstoneMaterialBaseUrl + definition.TheoryReading2File,
            2_500_000L,
            seedTime);
        await EnsureDemoMaterialAsync(
            researchBrief.Id,
            definition.ResearchBriefName,
            MaterialType.PDF,
            CapstoneMaterialBaseUrl + definition.ResearchBriefFile,
            2_500_000L,
            seedTime);

        var bank = await EnsureDemoQuestionBankAsync(
            theoryCourse.Id,
            definition.QuizBankName,
            $"Easy Vietnamese question bank for {definition.Name}.",
            definition.BankQuestions,
            seedTime);

        await EnsureDemoQuizAsync(
            theoryModule.Id,
            theoryCourse.Id,
            bank.Id,
            definition.AssignmentCode("QUIZ"),
            definition.QuizTitle,
            seedTime);

        await EnsureDemoRetrospectiveAsync(
            experientialModule.Id,
            experientialCourse.Id,
            definition.AssignmentCode("RETRO"),
            definition.RetrospectiveTitle,
            definition.RetrospectiveDescription,
            seedTime);

        await EnsureDemoResearchMilestonesAsync(
            researchModule.Id,
            definition,
            researchBrief.Id,
            researchOffline.Id,
            seedTime);

        var classEntity = await EnsureDemoClassAsync(
            program.Id,
            mentorId,
            definition.ClassCode,
            definition.ClassName,
            definition.ScheduleSummary,
            seedTime,
            definition.ClassStatus,
            startDate: seedTime.AddDays(definition.ClassStartDaysOffset),
            endDate: seedTime.AddDays(definition.ClassEndDaysOffset));

        IReadOnlyList<(Guid ModuleId, Activity Activity, SessionKind Kind)> sessionDefs =
        [
            (experientialModule.Id, experientialLive, SessionKind.LiveOnline),
            (experientialModule.Id, experientialOffline, SessionKind.Offline),
            (researchModule.Id, researchOffline, SessionKind.Offline),
        ];
        Module[] modules = [theoryModule, experientialModule, researchModule];
        Course[] courses = [theoryCourse, experientialCourse, researchCourse];

        await EnsureDemoClassSessionsAsync(classEntity, sessionDefs, DemoSatSunMorning, seedTime);

        await PruneDemoStudentEnrollmentsAsync(program, definition.AllStudentCodes);
        await EnsureDemoStudentEnrollmentsAsync(
            program,
            definition.StudentCodes,
            modules,
            courses,
            classEntity,
            seedTime);

        foreach (var openClass in definition.AdditionalOpenClasses)
        {
            var openClassMentor = await _unitOfWork.Users.FirstOrDefaultAsync(
                u => u.Code == openClass.MentorCode && u.Role == RoleType.Mentor && !u.IsDeleted);
            if (openClassMentor == null)
            {
                _loggerService.LogWarning(
                    "Skipping open class {ClassCode}: mentor {MentorCode} not found.",
                    openClass.ClassCode,
                    openClass.MentorCode);
                continue;
            }

            var openClassEntity = await EnsureDemoClassAsync(
                program.Id,
                openClassMentor.Id,
                openClass.ClassCode,
                openClass.ClassName,
                openClass.ScheduleSummary,
                seedTime,
                ClassStatus.Open,
                startDate: seedTime.AddDays(openClass.StartDaysOffset),
                endDate: seedTime.AddDays(openClass.EndDaysOffset));

            await EnsureDemoClassSessionsAsync(openClassEntity, sessionDefs, openClass.WeeklySlots, seedTime);
            await EnsureDemoStudentEnrollmentsAsync(
                program,
                openClass.StudentCodes,
                modules,
                courses,
                openClassEntity,
                seedTime);
        }
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
                await _unitOfWork.Programs.Update(existing);
                await _unitOfWork.SaveChangesAsync();
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
        DateTime seedTime,
        Guid? prerequisiteModuleId = null)
    {
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
            Description = "Easy quiz drawn from the course question bank.",
            AssignmentType = AssignmentType.Quiz,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            AllowShuffle = true,
            ShuffleOptions = true,
            QuestionBankId = questionBankId,
            QuestionCount = 5,
            EasyPercent = 100,
            MediumPercent = 0,
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
        DemoProgramDefinition definition,
        Guid researchBriefId,
        Guid researchOfflineId,
        DateTime seedTime)
    {
        var milestone1Code = $"RML-CAP-{definition.Slug}-01";
        var existingMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == milestone1Code && !rm.IsDeleted);
        if (existingMilestone != null)
        {
            return;
        }

        var plans = new (DemoMilestoneDefinition Milestone, bool IsCapstone, Guid? LinkedActivityId)[]
        {
            (definition.DesignMilestone, false, researchBriefId),
            (definition.PrototypeMilestone, false, null),
            (definition.CapstoneMilestone, true, researchOfflineId),
        };

        var assignments = new List<Assignment>();
        var milestones = new List<ResearchMilestone>();
        var links = new List<ResearchMilestoneActivity>();

        for (var index = 0; index < plans.Length; index++)
        {
            var order = index + 1;
            var plan = plans[index];

            var assignment = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = definition.AssignmentCode($"MS{order:D2}"),
                ModuleId = researchModuleId,
                Title = plan.Milestone.AssignmentTitle,
                Description = plan.Milestone.Description,
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
            assignments.Add(assignment);

            var milestone = new ResearchMilestone
            {
                Id = Guid.NewGuid(),
                Code = $"RML-CAP-{definition.Slug}-{order:D2}",
                ModuleId = researchModuleId,
                Title = plan.Milestone.Title,
                Description = plan.Milestone.Description,
                MilestoneOrder = order,
                IsCapstone = plan.IsCapstone,
                AssignmentId = assignment.Id,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            milestones.Add(milestone);

            if (plan.LinkedActivityId is { } activityId)
            {
                links.Add(new ResearchMilestoneActivity
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestone.Id,
                    ActivityId = activityId,
                    IsRequiredForSubmission = true,
                    DisplayOrder = 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        await _unitOfWork.Assignments.AddRangeAsync(assignments);
        await _unitOfWork.ResearchMilestones.AddRangeAsync(milestones);
        await _unitOfWork.SaveChangesAsync();

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

    /// <summary>
    /// One session per LiveOnline/Offline activity on the class's weekly grid.
    /// The InProgress capstone class is re-pinned to the seed clock by <c>ApplyCapstoneLiveSessionsAsync</c>.
    /// </summary>
    private async Task EnsureDemoClassSessionsAsync(
        Class classEntity,
        IReadOnlyList<(Guid ModuleId, Activity Activity, SessionKind Kind)> sessionDefs,
        SeedTimeline.WeekdaySlot[] weeklySlots,
        DateTime seedTime)
    {
        var sessionsToAdd = new List<ClassSession>();

        for (var sessionIndex = 0; sessionIndex < sessionDefs.Count; sessionIndex++)
        {
            var definition = sessionDefs[sessionIndex];
            var existing = await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                cs => cs.ClassId == classEntity.Id
                      && cs.ActivityId == definition.Activity.Id
                      && !cs.IsDeleted);

            var slot = SeedTimeline.TryResolveSlotSequence(
                classEntity.StartDate,
                classEntity.EndDate,
                weeklySlots,
                sessionIndex);
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
                sessionIndex);

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
                Title = definition.Activity.Name,
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

    private async Task PruneDemoStudentEnrollmentsAsync(Program program, IReadOnlyCollection<string> allowedCodes)
    {
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
                ce => ce.ProgramEnrollmentId == enrollment.Id && !ce.IsDeleted);
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
                "Pruned demo enrollment for student {StudentId} on {ProgramCode} (not on the demo roster).",
                enrollment.StudentId,
                program.Code);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureDemoStudentEnrollmentsAsync(
        Program program,
        IReadOnlyCollection<string> studentCodes,
        IReadOnlyList<Module> modules,
        IReadOnlyList<Course> courses,
        Class classEntity,
        DateTime seedTime)
    {
        if (studentCodes.Count == 0)
        {
            _loggerService.LogWarning(
                "No demo student roster for {ProgramCode}. Skipping enrollments.",
                program.Code);
            return;
        }

        // InProgress cohorts enrolled before class start; Open cohorts bought a few days ago.
        var enrolledAt = classEntity.StartDate < seedTime
            ? classEntity.StartDate.AddDays(-3)
            : seedTime.AddDays(-5);

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
                    EnrolledAt = enrolledAt,
                    StartedAt = enrolledAt.AddDays(1),
                    CreatedAt = enrolledAt,
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
                    EnrolledAt = enrolledAt,
                    StartedAt = enrolledAt.AddDays(1),
                    CreatedAt = enrolledAt,
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
                    JoinedAt = enrolledAt,
                    StartedAt = enrolledAt.AddDays(1),
                    CreatedAt = enrolledAt,
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
                EnrolledAt = enrolledAt,
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
