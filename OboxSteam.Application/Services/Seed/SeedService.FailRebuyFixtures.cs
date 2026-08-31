using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    internal const string FailRebuyTheoryModuleCode = "MOD-FAILREBUY-TH";
    internal const string FailRebuyExperientialModuleCode = "MOD-FAILREBUY-01";
    internal const string FailRebuyResearchModuleCode = "MOD-FAILREBUY-02";
    internal const string FailRebuyTheoryQuizCode = "ASG-FAILREBUY-TH-QUIZ";
    internal const string FailRebuyQuizCode = "ASG-FAILREBUY-QUIZ";
    internal const string FailRebuyUploadCode = "ASG-FAILREBUY-UPLOAD";
    internal const string FailRebuyResearchAssignmentCode = "ASG-FAILREBUY-RS01";
    internal const string FailRebuyResearchMilestoneCode = "RML-FAILREBUY-01";

    /// <summary>
    /// Isolated three-module track (theory → lab → research) plus four classes so
    /// fail/drop close, rebuy window, credit copy, class eligibility, and manager
    /// reopen can be exercised without touching Robotics/demo showcase data.
    /// Each class has a chronological ClassSession timetable: past slots are
    /// Completed, the live slot is InProgress, upcoming slots are Scheduled.
    /// Must run after demo submission clearing and before payment seed.
    /// </summary>
    private async Task SeedFailRebuyFixturesAsync()
    {
        _loggerService.LogInformation("Starting seed fail/rebuy test fixtures");

        var currentMentor = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == FailRebuyMentorCode && !u.IsDeleted);
        var rebuyMentor = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == FailRebuyRebuyMentorCode && !u.IsDeleted);
        if (currentMentor == null || rebuyMentor == null)
        {
            _loggerService.LogWarning(
                "Mentor {Current} or {Rebuy} missing. Skipping fail/rebuy fixtures.",
                FailRebuyMentorCode,
                FailRebuyRebuyMentorCode);
            return;
        }

        var students = (await _unitOfWork.Users.GetAllAsync(
                u => u.Role == RoleType.Student && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);

        foreach (var code in FailRebuyClosedStudentCodes
                     .Concat(FailRebuyActiveStudentCodes)
                     .Concat(FailRebuyRebuyActiveStudentCodes))
        {
            if (!students.ContainsKey(code))
            {
                _loggerService.LogWarning(
                    "Student {StudentCode} missing. Skipping fail/rebuy fixtures.",
                    code);
                return;
            }
        }

        var seedTime = _seedNow;
        var program = await EnsureFailRebuyProgramAsync(seedTime);

        var theoryModule = await EnsureFailRebuyModuleAsync(
            program.Id,
            FailRebuyTheoryModuleCode,
            "Foundations",
            ModuleType.Theory,
            moduleOrder: 1,
            seedTime,
            prerequisiteModuleId: null);
        var labModule = await EnsureFailRebuyModuleAsync(
            program.Id,
            FailRebuyExperientialModuleCode,
            "Studio Lab",
            ModuleType.Experiential,
            moduleOrder: 2,
            seedTime,
            theoryModule.Id);
        var researchModule = await EnsureFailRebuyModuleAsync(
            program.Id,
            FailRebuyResearchModuleCode,
            "Capstone Research",
            ModuleType.Research,
            moduleOrder: 3,
            seedTime,
            labModule.Id);

        var theoryCourse = await EnsureFailRebuyCourseAsync(
            theoryModule.Id, "CRS-FAILREBUY-TH", "Foundations Studio", 1, seedTime);
        var labCourse = await EnsureFailRebuyCourseAsync(
            labModule.Id, "CRS-FAILREBUY-01", "Lab Sessions", 1, seedTime);
        var researchCourse = await EnsureFailRebuyCourseAsync(
            researchModule.Id, "CRS-FAILREBUY-02", "Research Studio", 1, seedTime);

        var kickoff = await EnsureFailRebuyActivityAsync(
            theoryCourse.Id,
            "ACT-FAILREBUY-TH-KICK",
            "Orientation Live",
            1,
            seedTime,
            ActivityType.LiveOnline);
        var theoryReadings = new List<Activity>
        {
            await EnsureFailRebuyActivityAsync(
                theoryCourse.Id, "ACT-FAILREBUY-TH-01", "Reading: STEAM Mindset", 2, seedTime),
            await EnsureFailRebuyActivityAsync(
                theoryCourse.Id, "ACT-FAILREBUY-TH-02", "Reading: Safety & Tools", 3, seedTime),
        };
        var theoryActivities = new List<Activity> { kickoff };
        theoryActivities.AddRange(theoryReadings);

        var labActivities = new List<Activity>(5);
        for (var i = 1; i <= 5; i++)
        {
            labActivities.Add(await EnsureFailRebuyActivityAsync(
                labCourse.Id,
                $"ACT-FAILREBUY-01-0{i}",
                $"Lab Session {i}",
                i,
                seedTime,
                ActivityType.LiveOnline));
        }

        var researchReading = await EnsureFailRebuyActivityAsync(
            researchCourse.Id,
            "ACT-FAILREBUY-RS-01",
            "Research Brief",
            1,
            seedTime);

        var theoryBank = await EnsureFailRebuyQuestionBankAsync(
            theoryCourse.Id, "Fail Rebuy Theory Bank", "What is STEAM?", seedTime);
        var labBank = await EnsureFailRebuyQuestionBankAsync(
            labCourse.Id, "Fail Rebuy Quiz Bank", "What is 1 + 1?", seedTime);

        var theoryQuiz = await EnsureFailRebuyQuizAsync(
            FailRebuyTheoryQuizCode,
            theoryModule.Id,
            theoryCourse.Id,
            theoryBank.Id,
            "Foundations Quiz",
            "Required quiz to complete the Foundations module.",
            maxAttempts: 3,
            seedTime);
        var labQuiz = await EnsureFailRebuyQuizAsync(
            FailRebuyQuizCode,
            labModule.Id,
            labCourse.Id,
            labBank.Id,
            "Studio Lab Quiz",
            "One attempt. Two decided recoveries are pre-seeded on trigger students.",
            maxAttempts: 1,
            seedTime);
        var upload = await EnsureFailRebuyUploadAsync(labModule.Id, labCourse.Id, seedTime);
        var (researchAssignment, researchMilestone) = await EnsureFailRebuyResearchAsync(
            researchModule.Id,
            seedTime);

        // Class dates follow the first/last live session. ELIGIBLE / BLOCKED / FRESH
        // stay Open so first-purchase and Completed pickers still list them
        // (open-classes is Open-only). Rebuy eligibility uses ClassSession.Status.
        var currentClass = await EnsureFailRebuyClassAsync(
            FailRebuyClassCode,
            "STEAM Foundations — Current Cohort",
            program.Id,
            currentMentor.Id,
            ClassStatus.InProgress,
            seedTime.AddDays(-14),
            seedTime.AddDays(42),
            "Foundations done; lab 5 of 5 in progress",
            seedTime);
        var eligibleClass = await EnsureFailRebuyClassAsync(
            FailRebuyEligibleClassCode,
            "STEAM Foundations — Next Cohort",
            program.Id,
            rebuyMentor.Id,
            ClassStatus.Open,
            seedTime.AddDays(-7),
            seedTime.AddDays(63),
            "Foundations complete; lab starts next week",
            seedTime);
        var blockedClass = await EnsureFailRebuyClassAsync(
            FailRebuyBlockedClassCode,
            "STEAM Foundations — Mid-lab Cohort",
            program.Id,
            rebuyMentor.Id,
            ClassStatus.Open,
            seedTime.AddDays(-16),
            seedTime.AddDays(61),
            "Foundations done; lab 1 of 5 complete, rest upcoming",
            seedTime);
        var freshClass = await EnsureFailRebuyClassAsync(
            FailRebuyFreshClassCode,
            "STEAM Foundations — Upcoming Cohort",
            program.Id,
            rebuyMentor.Id,
            ClassStatus.Open,
            seedTime.AddDays(21),
            seedTime.AddDays(77),
            "Not yet started; first session in three weeks",
            seedTime);
        var graduatedClass = await EnsureFailRebuyClassAsync(
            FailRebuyGraduatedClassCode,
            "STEAM Foundations — Graduated Chuyen-ca Cohort",
            program.Id,
            rebuyMentor.Id,
            ClassStatus.Completed,
            seedTime.AddDays(-90),
            seedTime.AddDays(-7),
            "Finished last week; all modules taught",
            seedTime);

        await SeedFailRebuyCohortTimetableAsync(
            currentClass.Id,
            eligibleClass.Id,
            blockedClass.Id,
            freshClass.Id,
            graduatedClass.Id,
            theoryModule.Id,
            labModule.Id,
            researchModule.Id,
            kickoff,
            labActivities,
            researchReading,
            seedTime);

        await EnsureFailRebuyEnrollmentsAsync(
            students,
            program,
            currentClass,
            eligibleClass,
            graduatedClass,
            theoryModule,
            labModule,
            researchModule,
            theoryActivities,
            labActivities,
            researchReading,
            theoryQuiz,
            labQuiz,
            upload,
            researchAssignment,
            researchMilestone,
            seedTime);

        _loggerService.LogInformation("Finished seed fail/rebuy test fixtures");
    }

    private async Task<Program> EnsureFailRebuyProgramAsync(DateTime seedTime)
    {
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == FailRebuyProgramCode && !p.IsDeleted);
        if (existing != null)
        {
            existing.RetakeFee ??= CatalogRetakeFee(existing.Price);
            existing.Name = "STEAM Foundations";
            existing.SeriesName = "Core Track";
            await _unitOfWork.Programs.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return existing;
        }

        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyProgramCode,
            Name = "STEAM Foundations",
            SeriesName = "Core Track",
            Description =
                "Three-module foundations track: theory, studio lab, then capstone research. " +
                "Also the fail/rebuy QA fixture (PRG-FAILREBUY).",
            Level = DifficultyLevel.Beginner,
            Category = ProgramCategory.Technology,
            EstimatedDuration = "6 weeks",
            SkillsGained = "STEAM foundations, studio practice, research documentation",
            Status = ProgramStatus.Active,
            Price = 1_000_000m,
            RetakeFee = CatalogRetakeFee(1_000_000m),
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Programs.AddAsync(program);
        await _unitOfWork.SaveChangesAsync();
        return program;
    }

    private async Task<Module> EnsureFailRebuyModuleAsync(
        Guid programId,
        string code,
        string name,
        ModuleType moduleType,
        int moduleOrder,
        DateTime seedTime,
        Guid? prerequisiteModuleId)
    {
        var existing = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == code && !m.IsDeleted);
        if (existing != null)
        {
            existing.Name = name;
            existing.ModuleOrder = moduleOrder;
            existing.PrerequisiteModuleId = prerequisiteModuleId;
            existing.ModuleType = moduleType;
            await _unitOfWork.Modules.Update(existing);
            await _unitOfWork.SaveChangesAsync();
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
            LearningOutcomes =
            [
                $"Complete {name} before moving on.",
                "Apply studio safety and documentation habits.",
            ],
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Modules.AddAsync(module);
        await _unitOfWork.SaveChangesAsync();
        return module;
    }

    private async Task<Course> EnsureFailRebuyCourseAsync(
        Guid moduleId,
        string code,
        string name,
        int courseOrder,
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
            Description = name,
            CourseOrder = courseOrder,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();
        return course;
    }

    private async Task<Activity> EnsureFailRebuyActivityAsync(
        Guid courseId,
        string code,
        string name,
        int order,
        DateTime seedTime,
        ActivityType activityType = ActivityType.SelfPaced)
    {
        var existing = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            existing.ActivityType = activityType;
            existing.ActivityOrder = order;
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
            Description = name,
            ActivityType = activityType,
            ActivityOrder = order,
            DurationMinutes = activityType == ActivityType.LiveOnline ? 90 : 30,
            RequireQrCheckin = false,
            RequireMediaEvidence = false,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Activities.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();
        return activity;
    }

    private async Task<QuestionBank> EnsureFailRebuyQuestionBankAsync(
        Guid courseId,
        string name,
        string questionText,
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
            Description = "Single easy question so pass/fail is deterministic in QA.",
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.QuestionBanks.AddAsync(bank);
        await _unitOfWork.SaveChangesAsync();

        var question = new BankQuestion
        {
            Id = Guid.NewGuid(),
            QuestionBankId = bank.Id,
            QuestionText = questionText,
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 100,
            DifficultyLevel = 1,
            OrderIndex = 1,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.BankQuestions.AddAsync(question);
        await _unitOfWork.SaveChangesAsync();

        var texts = questionText.Contains("1 + 1", StringComparison.Ordinal)
            ? new[] { "2", "3", "4", "5" }
            : new[] { "Science, Technology, Engineering, Arts, and Math", "Only coding", "Only painting", "A brand name" };
        var options = texts.Select((text, index) => new BankQuestionOption
        {
            Id = Guid.NewGuid(),
            BankQuestionId = question.Id,
            OptionText = text,
            IsCorrect = index == 0,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        }).ToList();
        await _unitOfWork.BankQuestionOptions.AddRangeAsync(options);
        await _unitOfWork.SaveChangesAsync();
        return bank;
    }

    private async Task<Assignment> EnsureFailRebuyQuizAsync(
        string code,
        Guid moduleId,
        Guid courseId,
        Guid bankId,
        string title,
        string description,
        int maxAttempts,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var quiz = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            CourseId = courseId,
            Title = title,
            Description = description,
            AssignmentType = AssignmentType.Quiz,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            AllowShuffle = false,
            ShuffleOptions = false,
            QuestionBankId = bankId,
            QuestionCount = 1,
            EasyPercent = 100,
            MediumPercent = 0,
            HardPercent = 0,
            TimeLimitMinutes = 15,
            MaxAttempts = maxAttempts,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Assignments.AddAsync(quiz);
        await _unitOfWork.SaveChangesAsync();
        return quiz;
    }

    private async Task<Assignment> EnsureFailRebuyUploadAsync(Guid moduleId, Guid courseId, DateTime seedTime)
    {
        var existing = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == FailRebuyUploadCode && !a.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyUploadCode,
            ModuleId = moduleId,
            CourseId = courseId,
            Title = "Studio Lab File Upload",
            Description = "Build photo / short note. Used for failing-grade and pass-copy fixtures.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            MaxAttempts = 1,
            TimeLimitMinutes = 60,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Assignments.AddAsync(assignment);
        await _unitOfWork.SaveChangesAsync();
        return assignment;
    }

    private async Task<(Assignment Assignment, ResearchMilestone Milestone)> EnsureFailRebuyResearchAsync(
        Guid researchModuleId,
        DateTime seedTime)
    {
        var existingMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == FailRebuyResearchMilestoneCode && !rm.IsDeleted);
        if (existingMilestone != null)
        {
            var existingAssignment = await _unitOfWork.Assignments.GetByIdAsync(existingMilestone.AssignmentId);
            return (existingAssignment!, existingMilestone);
        }

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyResearchAssignmentCode,
            ModuleId = researchModuleId,
            Title = "Capstone Research Upload",
            Description = "Research deliverable. Waiting-grade and completed-copy fixtures share this assignment.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            MaxAttempts = 1,
            TimeLimitMinutes = 60,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var milestone = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyResearchMilestoneCode,
            ModuleId = researchModuleId,
            Title = "Capstone Milestone",
            Description = "Single research milestone for close-path and completed-copy tests.",
            MilestoneOrder = 1,
            IsCapstone = true,
            AssignmentId = assignment.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Assignments.AddAsync(assignment);
        await _unitOfWork.ResearchMilestones.AddAsync(milestone);
        await _unitOfWork.SaveChangesAsync();
        return (assignment, milestone);
    }

    private async Task<Class> EnsureFailRebuyClassAsync(
        string code,
        string name,
        Guid programId,
        Guid mentorId,
        ClassStatus status,
        DateTime startDate,
        DateTime endDate,
        string scheduleSummary,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == code && !c.IsDeleted);
        if (existing != null)
        {
            existing.Name = name;
            existing.Status = status;
            existing.MentorId = mentorId;
            existing.StartDate = startDate;
            existing.EndDate = endDate;
            existing.ScheduleSummary = scheduleSummary;
            await _unitOfWork.Classes.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return existing;
        }

        var classEntity = new Class
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ProgramId = programId,
            MentorId = mentorId,
            StartDate = startDate,
            EndDate = endDate,
            MaxCapacity = 20,
            Kind = ClassKind.Standard,
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

    private async Task EnsureFailRebuySessionAsync(
        Guid classId,
        Guid moduleId,
        Activity activity,
        DateTime start,
        ClassSessionStatus status,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
            cs => cs.ClassId == classId
                  && cs.ActivityId == activity.Id
                  && !cs.IsDeleted);
        if (existing != null)
        {
            existing.StartTime = start;
            existing.EndTime = start.AddMinutes(90);
            existing.Status = status;
            existing.ModuleId = moduleId;
            await _unitOfWork.ClassSessions.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        await _unitOfWork.ClassSessions.AddAsync(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            ActivityId = activity.Id,
            SessionKind = SessionKind.LiveOnline,
            Title = activity.Name,
            StartTime = start,
            EndTime = start.AddMinutes(90),
            MeetingUrl = "https://meet.example.com/fail-rebuy",
            RequiresAttendance = true,
            Status = status,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Builds one live timetable per fail/rebuy cohort. Status matches wall-clock
    /// so catalog progress and credit copy agree on the happy path.
    /// </summary>
    private async Task SeedFailRebuyCohortTimetableAsync(
        Guid currentClassId,
        Guid eligibleClassId,
        Guid blockedClassId,
        Guid freshClassId,
        Guid graduatedClassId,
        Guid theoryModuleId,
        Guid labModuleId,
        Guid researchModuleId,
        Activity kickoff,
        IReadOnlyList<Activity> labActivities,
        Activity researchReading,
        DateTime seedTime)
    {
        DateTime AtNine(int daysFromSeed) => seedTime.AddDays(daysFromSeed).Date.AddHours(9);

        // CURRENT: Foundations done, labs 1-4 held, lab 5 live, research later.
        await EnsureFailRebuySessionAsync(
            currentClassId, theoryModuleId, kickoff, AtNine(-12), ClassSessionStatus.Completed, seedTime);
        for (var i = 0; i < labActivities.Count; i++)
        {
            var isLive = i == labActivities.Count - 1;
            await EnsureFailRebuySessionAsync(
                currentClassId,
                labModuleId,
                labActivities[i],
                isLive ? seedTime.AddHours(-1) : AtNine(-10 + (i * 2)),
                isLive ? ClassSessionStatus.InProgress : ClassSessionStatus.Completed,
                seedTime);
        }

        await EnsureFailRebuySessionAsync(
            currentClassId, researchModuleId, researchReading, AtNine(21), ClassSessionStatus.Scheduled, seedTime);

        // ELIGIBLE: Foundations done last week; every Lab/Research slot is upcoming.
        await EnsureFailRebuySessionAsync(
            eligibleClassId, theoryModuleId, kickoff, AtNine(-5), ClassSessionStatus.Completed, seedTime);
        for (var i = 0; i < labActivities.Count; i++)
        {
            await EnsureFailRebuySessionAsync(
                eligibleClassId,
                labModuleId,
                labActivities[i],
                AtNine(7 + (i * 2)),
                ClassSessionStatus.Scheduled,
                seedTime);
        }

        await EnsureFailRebuySessionAsync(
            eligibleClassId, researchModuleId, researchReading, AtNine(42), ClassSessionStatus.Scheduled, seedTime);

        // BLOCKED: Foundations done, lab 1 held, labs 2-5 and research still ahead.
        await EnsureFailRebuySessionAsync(
            blockedClassId, theoryModuleId, kickoff, AtNine(-14), ClassSessionStatus.Completed, seedTime);
        for (var i = 0; i < labActivities.Count; i++)
        {
            var labHeld = i == 0;
            await EnsureFailRebuySessionAsync(
                blockedClassId,
                labModuleId,
                labActivities[i],
                labHeld ? AtNine(-2) : AtNine(3 + ((i - 1) * 2)),
                labHeld ? ClassSessionStatus.Completed : ClassSessionStatus.Scheduled,
                seedTime);
        }

        await EnsureFailRebuySessionAsync(
            blockedClassId, researchModuleId, researchReading, AtNine(28), ClassSessionStatus.Scheduled, seedTime);

        // FRESH: nothing taught; first session in three weeks.
        await EnsureFailRebuySessionAsync(
            freshClassId, theoryModuleId, kickoff, AtNine(21), ClassSessionStatus.Scheduled, seedTime);
        for (var i = 0; i < labActivities.Count; i++)
        {
            await EnsureFailRebuySessionAsync(
                freshClassId,
                labModuleId,
                labActivities[i],
                AtNine(28 + (i * 2)),
                ClassSessionStatus.Scheduled,
                seedTime);
        }

        await EnsureFailRebuySessionAsync(
            freshClassId, researchModuleId, researchReading, AtNine(56), ClassSessionStatus.Scheduled, seedTime);

        // GRADUATED: a finished chuyen-ca destination. Every teaching slot is in the past
        // so copied Foundations + redone lab/research can complete with this class.
        await EnsureFailRebuySessionAsync(
            graduatedClassId, theoryModuleId, kickoff, AtNine(-80), ClassSessionStatus.Completed, seedTime);
        for (var i = 0; i < labActivities.Count; i++)
        {
            await EnsureFailRebuySessionAsync(
                graduatedClassId,
                labModuleId,
                labActivities[i],
                AtNine(-70 + (i * 7)),
                ClassSessionStatus.Completed,
                seedTime);
        }

        await EnsureFailRebuySessionAsync(
            graduatedClassId, researchModuleId, researchReading, AtNine(-21), ClassSessionStatus.Completed, seedTime);
    }

    private async Task EnsureFailRebuyEnrollmentsAsync(
        Dictionary<string, User> students,
        Program program,
        Class currentClass,
        Class eligibleClass,
        Class graduatedClass,
        Module theoryModule,
        Module labModule,
        Module researchModule,
        IReadOnlyList<Activity> theoryActivities,
        IReadOnlyList<Activity> labActivities,
        Activity researchReading,
        Assignment theoryQuiz,
        Assignment labQuiz,
        Assignment upload,
        Assignment researchAssignment,
        ResearchMilestone researchMilestone,
        DateTime seedTime)
    {
        async Task<ProgramEnrollment> EnsurePe(
            string studentCode,
            EnrollmentStatus status,
            ProgramPurchaseEndReason? endReason,
            Guid? endedModuleId,
            DateTime? endedAt,
            DateTime? completedAt = null,
            Guid? sourceProgramEnrollmentId = null,
            DateTime? enrolledAtOverride = null)
        {
            var student = students[studentCode];
            var existing = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student.Id
                      && pe.ProgramId == program.Id
                      && !pe.IsDeleted
                      && pe.SourceProgramEnrollmentId == sourceProgramEnrollmentId);
            if (existing != null)
            {
                return existing;
            }

            var enrolledAt = enrolledAtOverride ?? seedTime.AddDays(-24);
            var enrollment = new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = program.Id,
                Status = status,
                ProgressPercent = 0m,
                EnrolledAt = enrolledAt,
                StartedAt = enrolledAt.AddDays(1),
                CompletedAt = completedAt,
                EndReason = endReason,
                EndedModuleId = endedModuleId,
                EndedAt = endedAt,
                SourceProgramEnrollmentId = sourceProgramEnrollmentId,
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramEnrollments.AddAsync(enrollment);
            await _unitOfWork.SaveChangesAsync();
            return enrollment;
        }

        async Task<ModuleEnrollment> EnsureMe(
            ProgramEnrollment pe,
            Module module,
            EnrollmentStatus status,
            decimal progressPercent,
            DateTime? completedAt = null,
            decimal? finalGrade = null,
            int attemptNumber = 1)
        {
            var existing = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.ProgramEnrollmentId == pe.Id
                      && me.ModuleId == module.Id
                      && !me.IsDeleted);
            if (existing != null)
            {
                return existing;
            }

            var enrollment = new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = pe.StudentId,
                ModuleId = module.Id,
                ProgramEnrollmentId = pe.Id,
                Status = status,
                ProgressPercent = progressPercent,
                FinalGrade = finalGrade,
                AttemptNumber = attemptNumber,
                EnrolledAt = pe.EnrolledAt,
                StartedAt = pe.StartedAt,
                CompletedAt = completedAt,
                CreatedAt = pe.EnrolledAt ?? seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ModuleEnrollments.AddAsync(enrollment);
            await _unitOfWork.SaveChangesAsync();
            return enrollment;
        }

        async Task EnsureSeat(ProgramEnrollment pe, ClassEnrollmentStatus status, Class classEntity)
        {
            var existing = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.ProgramEnrollmentId == pe.Id
                      && ce.ClassId == classEntity.Id
                      && !ce.IsDeleted);
            if (existing != null)
            {
                return;
            }

            await _unitOfWork.ClassEnrollments.AddAsync(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = classEntity.Id,
                StudentId = pe.StudentId,
                ProgramEnrollmentId = pe.Id,
                Kind = ClassEnrollmentKind.Primary,
                Status = status,
                EnrolledAt = pe.EnrolledAt,
                CreatedAt = pe.EnrolledAt ?? seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }

        async Task EnsureProgressDone(ModuleEnrollment me, IEnumerable<Activity> activities)
        {
            foreach (var activity in activities)
            {
                var existing = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                    ap => ap.ModuleEnrollmentId == me.Id
                          && ap.ActivityId == activity.Id
                          && !ap.IsDeleted);
                if (existing != null)
                {
                    continue;
                }

                await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = me.StudentId,
                    ActivityId = activity.Id,
                    ModuleEnrollmentId = me.Id,
                    ActivityStatus = ActivityStatus.Done,
                    IsCompleted = true,
                    CompletionSource = CompletionSource.Manual,
                    CompletedAt = seedTime.AddDays(-10),
                    LastAccessedAt = seedTime.AddDays(-10),
                    CreatedAt = seedTime.AddDays(-10),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            await _unitOfWork.SaveChangesAsync();
        }

        async Task EnsureSubmission(
            User student,
            ModuleEnrollment me,
            Assignment assignment,
            SubmissionStatus status,
            decimal? grade,
            Guid? researchMilestoneId)
        {
            var existing = await _unitOfWork.Submissions.FirstOrDefaultAsync(
                s => s.StudentId == student.Id
                     && s.AssignmentId == assignment.Id
                     && s.ModuleEnrollmentId == me.Id
                     && !s.IsDeleted);
            if (existing != null)
            {
                return;
            }

            await _unitOfWork.Submissions.AddAsync(new Submission
            {
                Id = Guid.NewGuid(),
                Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                AssignmentId = assignment.Id,
                StudentId = student.Id,
                ModuleEnrollmentId = me.Id,
                ResearchMilestoneId = researchMilestoneId,
                AttemptNumber = 1,
                Status = status,
                AssignedGrade = grade,
                ContentText = status == SubmissionStatus.Graded
                    ? "Seeded graded work."
                    : "Seeded work waiting for a grade.",
                FileUrl = assignment.AssignmentType == AssignmentType.FileUpload
                    ? "https://cdn.example.com/fail-rebuy/work.pdf"
                    : null,
                SubmittedAt = seedTime.AddDays(-4),
                GradedAt = status == SubmissionStatus.Graded ? seedTime.AddDays(-3) : null,
                CreatedAt = seedTime.AddDays(-4),
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }

        async Task EnsureRecoveries(
            User student,
            ModuleEnrollment me,
            Assignment assignment,
            params AssessmentRecoveryRequestStatus[] statuses)
        {
            var existing = await _unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
                r => r.StudentId == student.Id
                     && r.AssignmentId == assignment.Id
                     && r.ModuleEnrollmentId == me.Id
                     && !r.IsDeleted);
            if (existing.Count > 0)
            {
                return;
            }

            foreach (var status in statuses)
            {
                await _unitOfWork.AssessmentRecoveryRequests.AddAsync(new AssessmentRecoveryRequest
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ModuleEnrollmentId = me.Id,
                    AssignmentId = assignment.Id,
                    ClassId = currentClass.Id,
                    Status = status,
                    StudentMessage = "Seeded recovery for fail/rebuy close tests.",
                    ExtraAttemptsGranted = 0,
                    DecidedAt = status == AssessmentRecoveryRequestStatus.Pending
                        ? null
                        : seedTime.AddDays(-2),
                    DecidedBy = status == AssessmentRecoveryRequestStatus.Pending ? null : currentClass.MentorId,
                    CreatedAt = seedTime.AddDays(-3),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            await _unitOfWork.SaveChangesAsync();
        }

        async Task CompleteTheoryAsync(ProgramEnrollment pe, User student, int attemptNumber = 1)
        {
            var me = await EnsureMe(
                pe, theoryModule, EnrollmentStatus.Completed, 100m, seedTime.AddDays(-12), 90m, attemptNumber);
            await EnsureProgressDone(me, theoryActivities);
            await EnsureSubmission(student, me, theoryQuiz, SubmissionStatus.Graded, 90m, null);
            await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, me);
        }

        async Task CompleteLabAsync(ProgramEnrollment pe, User student, int attemptNumber = 1)
        {
            var me = await EnsureMe(
                pe, labModule, EnrollmentStatus.Completed, 100m, seedTime.AddDays(-6), 88m, attemptNumber);
            await EnsureProgressDone(me, labActivities);
            await EnsureSubmission(student, me, labQuiz, SubmissionStatus.Graded, 90m, null);
            await EnsureSubmission(student, me, upload, SubmissionStatus.Graded, 85m, null);
            await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, me);
        }

        async Task CompleteResearchAsync(ProgramEnrollment pe, User student, int attemptNumber = 1)
        {
            var me = await EnsureMe(
                pe, researchModule, EnrollmentStatus.Completed, 100m, seedTime.AddDays(-2), 80m, attemptNumber);
            await EnsureProgressDone(me, [researchReading]);
            await EnsureSubmission(
                student, me, researchAssignment, SubmissionStatus.Graded, 80m, researchMilestone.Id);
            await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, me);
        }

        async Task RecalcProgramAsync(ProgramEnrollment pe)
        {
            var anyMe = (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                    me => me.ProgramEnrollmentId == pe.Id && !me.IsDeleted))
                .FirstOrDefault();
            if (anyMe == null)
            {
                return;
            }

            await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork, pe.Id, anyMe);
        }

        async Task SeedLiveAbsenceAsync(ProgramEnrollment pe, ModuleEnrollment labMe)
        {
            var labSessions = (await _unitOfWork.ClassSessions.GetAllAsync(
                    cs => cs.ClassId == currentClass.Id
                          && cs.ModuleId == labModule.Id
                          && cs.ActivityId != null
                          && !cs.IsDeleted))
                .OrderBy(cs => cs.StartTime)
                .Take(3)
                .ToList();

            foreach (var liveSession in labSessions)
            {
                var existingAbsent = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(
                    sa => sa.ClassSessionId == liveSession.Id
                          && sa.StudentId == pe.StudentId
                          && !sa.IsDeleted);
                if (existingAbsent != null)
                {
                    continue;
                }

                await _unitOfWork.SessionAttendances.AddAsync(new SessionAttendance
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = liveSession.Id,
                    StudentId = pe.StudentId,
                    ModuleEnrollmentId = labMe.Id,
                    Status = AttendanceStatus.Absent,
                    RecordedBy = currentClass.MentorId,
                    CheckedInAt = seedTime.AddDays(-1),
                    CreatedAt = seedTime.AddDays(-1),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            await _unitOfWork.SaveChangesAsync();
        }

        var outsideWindow = seedTime.AddDays(-120);
        var recentClose = seedTime.AddDays(-1);

        // STD-026 — Failed/Attendance after passing Foundations. 3/5 session absences (>=50%).
        // Rebuy: ELIGIBLE copies Foundations (taught); Lab stays ahead on that class.
        {
            var pe = await EnsurePe(
                "STD-026",
                EnrollmentStatus.Failed,
                ProgramPurchaseEndReason.Attendance,
                labModule.Id,
                recentClose);
            await CompleteTheoryAsync(pe, students["STD-026"]);
            var labMe = await EnsureMe(pe, labModule, EnrollmentStatus.Failed, 15m);
            await EnsureSeat(pe, ClassEnrollmentStatus.Withdrawn, currentClass);
            await SeedLiveAbsenceAsync(pe, labMe);
            await RecalcProgramAsync(pe);
        }

        // STD-027 — Failed/AcademicFail on lab quiz after passing Foundations.
        // Rebuy: ELIGIBLE copies Foundations. FRESH has not taught anything yet.
        {
            var pe = await EnsurePe(
                "STD-027",
                EnrollmentStatus.Failed,
                ProgramPurchaseEndReason.AcademicFail,
                labModule.Id,
                recentClose);
            await CompleteTheoryAsync(pe, students["STD-027"]);
            var labMe = await EnsureMe(pe, labModule, EnrollmentStatus.Failed, 20m);
            await EnsureSeat(pe, ClassEnrollmentStatus.Withdrawn, currentClass);
            await EnsureSubmission(
                students["STD-027"], labMe, labQuiz, SubmissionStatus.Graded, 10m, null);
            await EnsureRecoveries(
                students["STD-027"],
                labMe,
                labQuiz,
                AssessmentRecoveryRequestStatus.Rejected,
                AssessmentRecoveryRequestStatus.Rejected);
            await RecalcProgramAsync(pe);
        }

        // STD-034 — same academic fail as 027, but EndedAt outside the 3-month window.
        {
            var pe = await EnsurePe(
                "STD-034",
                EnrollmentStatus.Failed,
                ProgramPurchaseEndReason.AcademicFail,
                labModule.Id,
                outsideWindow);
            await CompleteTheoryAsync(pe, students["STD-034"]);
            var labMe = await EnsureMe(pe, labModule, EnrollmentStatus.Failed, 20m);
            await EnsureSeat(pe, ClassEnrollmentStatus.Withdrawn, currentClass);
            await EnsureSubmission(
                students["STD-034"], labMe, labQuiz, SubmissionStatus.Graded, 10m, null);
            await RecalcProgramAsync(pe);
        }

        // STD-035 — Dropped/Withdraw after passing Foundations (stop module = lab).
        // Lab ME is Dropped (open modules close with the purchase).
        // After Lab is marked Completed, BLOCKED copies Foundations + lab 1 only.
        {
            var pe = await EnsurePe(
                "STD-035",
                EnrollmentStatus.Dropped,
                ProgramPurchaseEndReason.Withdraw,
                endedModuleId: null,
                recentClose);
            await CompleteTheoryAsync(pe, students["STD-035"]);
            await EnsureMe(pe, labModule, EnrollmentStatus.Dropped, 20m);
            await EnsureSeat(pe, ClassEnrollmentStatus.Withdrawn, currentClass);
            await RecalcProgramAsync(pe);
        }

        // STD-036 — Completed inside the window (retake price, no progress copy).
        {
            var pe = await EnsurePe(
                "STD-036",
                EnrollmentStatus.Completed,
                null,
                null,
                endedAt: null,
                completedAt: recentClose);
            await CompleteTheoryAsync(pe, students["STD-036"]);
            await CompleteLabAsync(pe, students["STD-036"]);
            await CompleteResearchAsync(pe, students["STD-036"]);
            await EnsureSeat(pe, ClassEnrollmentStatus.Completed, currentClass);
            await RecalcProgramAsync(pe);
            pe.Status = EnrollmentStatus.Completed;
            pe.CompletedAt = recentClose;
            pe.EndReason = null;
            pe.EndedAt = null;
            pe.EndedModuleId = null;
            await _unitOfWork.ProgramEnrollments.Update(pe);
            await _unitOfWork.SaveChangesAsync();
        }

        // STD-037 — Completed outside the window (full price, no progress copy).
        {
            var pe = await EnsurePe(
                "STD-037",
                EnrollmentStatus.Completed,
                null,
                null,
                endedAt: null,
                completedAt: outsideWindow);
            await CompleteTheoryAsync(pe, students["STD-037"]);
            await CompleteLabAsync(pe, students["STD-037"]);
            await CompleteResearchAsync(pe, students["STD-037"]);
            await EnsureSeat(pe, ClassEnrollmentStatus.Completed, currentClass);
            await RecalcProgramAsync(pe);
            pe.Status = EnrollmentStatus.Completed;
            pe.CompletedAt = outsideWindow;
            pe.EndReason = null;
            pe.EndedAt = null;
            pe.EndedModuleId = null;
            await _unitOfWork.ProgramEnrollments.Update(pe);
            await _unitOfWork.SaveChangesAsync();
        }

        // STD-038 — Failed first module (Foundations). Only FRESH class is eligible. Nothing to copy.
        {
            var pe = await EnsurePe(
                "STD-038",
                EnrollmentStatus.Failed,
                ProgramPurchaseEndReason.AcademicFail,
                theoryModule.Id,
                recentClose);
            var theoryMe = await EnsureMe(pe, theoryModule, EnrollmentStatus.Failed, 10m);
            await EnsureSeat(pe, ClassEnrollmentStatus.Withdrawn, currentClass);
            await EnsureSubmission(
                students["STD-038"], theoryMe, theoryQuiz, SubmissionStatus.Graded, 10m, null);
            await RecalcProgramAsync(pe);
        }

        foreach (var code in FailRebuyActiveStudentCodes)
        {
            var pe = await EnsurePe(code, EnrollmentStatus.Active, null, null, null);
            await CompleteTheoryAsync(pe, students[code]);
            await EnsureSeat(pe, ClassEnrollmentStatus.Active, currentClass);

            if (code == "STD-032")
            {
                await CompleteLabAsync(pe, students[code]);
                var researchMe = await EnsureMe(pe, researchModule, EnrollmentStatus.Active, 20m);
                await EnsureProgressDone(researchMe, [researchReading]);
                await EnsureRecoveries(
                    students[code],
                    researchMe,
                    researchAssignment,
                    AssessmentRecoveryRequestStatus.Rejected,
                    AssessmentRecoveryRequestStatus.Rejected);
                await EnsureSubmission(
                    students[code],
                    researchMe,
                    researchAssignment,
                    SubmissionStatus.TurnedIn,
                    grade: null,
                    researchMilestone.Id);
            }
            else
            {
                var labMe = await EnsureMe(pe, labModule, EnrollmentStatus.Active, 20m);

                if (code is "STD-030" or "STD-033")
                {
                    await EnsureRecoveries(
                        students[code],
                        labMe,
                        labQuiz,
                        code == "STD-033"
                            ? [AssessmentRecoveryRequestStatus.Rejected, AssessmentRecoveryRequestStatus.Pending]
                            : [AssessmentRecoveryRequestStatus.Rejected, AssessmentRecoveryRequestStatus.Rejected]);
                }

                if (code == "STD-031")
                {
                    await EnsureRecoveries(
                        students[code],
                        labMe,
                        upload,
                        AssessmentRecoveryRequestStatus.Rejected,
                        AssessmentRecoveryRequestStatus.Rejected);
                    await EnsureSubmission(
                        students[code], labMe, upload, SubmissionStatus.TurnedIn, null, null);
                }

                if (code == "STD-033")
                {
                    await EnsureSubmission(
                        students[code], labMe, labQuiz, SubmissionStatus.Graded, 10m, null);
                }
            }

            await RecalcProgramAsync(pe);
            pe.Status = EnrollmentStatus.Active;
            pe.EndReason = null;
            pe.EndedAt = null;
            pe.EndedModuleId = null;
            pe.CompletedAt = null;
            await _unitOfWork.ProgramEnrollments.Update(pe);
            await _unitOfWork.SaveChangesAsync();
        }

        async Task<ProgramEnrollment> SeedAcademicFailLabSourceAsync(string studentCode, DateTime closedAt)
        {
            var pe = await EnsurePe(
                studentCode,
                EnrollmentStatus.Failed,
                ProgramPurchaseEndReason.AcademicFail,
                labModule.Id,
                closedAt);
            await CompleteTheoryAsync(pe, students[studentCode]);
            var labMe = await EnsureMe(pe, labModule, EnrollmentStatus.Failed, 20m);
            await EnsureSeat(pe, ClassEnrollmentStatus.Withdrawn, currentClass);
            await EnsureSubmission(
                students[studentCode], labMe, labQuiz, SubmissionStatus.Graded, 10m, null);
            await RecalcProgramAsync(pe);
            return pe;
        }

        // STD-039 — AcademicFail on CURRENT, then finished chuyen-ca on GRADUATED.
        {
            var source = await SeedAcademicFailLabSourceAsync(
                FailRebuyRebuyCompletedStudentCode,
                seedTime.AddDays(-40));
            var student = students[FailRebuyRebuyCompletedStudentCode];
            var rebuy = await EnsurePe(
                FailRebuyRebuyCompletedStudentCode,
                EnrollmentStatus.Completed,
                null,
                null,
                endedAt: null,
                completedAt: recentClose,
                sourceProgramEnrollmentId: source.Id,
                enrolledAtOverride: seedTime.AddDays(-35));
            await CompleteTheoryAsync(rebuy, student, attemptNumber: 2);
            await CompleteLabAsync(rebuy, student, attemptNumber: 2);
            await CompleteResearchAsync(rebuy, student);
            await EnsureSeat(rebuy, ClassEnrollmentStatus.Completed, graduatedClass);
            await RecalcProgramAsync(rebuy);
            rebuy.Status = EnrollmentStatus.Completed;
            rebuy.CompletedAt = recentClose;
            rebuy.EndReason = null;
            rebuy.EndedAt = null;
            rebuy.EndedModuleId = null;
            rebuy.SourceProgramEnrollmentId = source.Id;
            await _unitOfWork.ProgramEnrollments.Update(rebuy);
            await _unitOfWork.SaveChangesAsync();
        }

        // STD-040 — AcademicFail on CURRENT, then Active chuyen-ca on ELIGIBLE (Foundations copied).
        {
            var source = await SeedAcademicFailLabSourceAsync(
                FailRebuyRebuyActiveStudentCode,
                seedTime.AddDays(-8));
            var student = students[FailRebuyRebuyActiveStudentCode];
            var rebuy = await EnsurePe(
                FailRebuyRebuyActiveStudentCode,
                EnrollmentStatus.Active,
                null,
                null,
                endedAt: null,
                sourceProgramEnrollmentId: source.Id,
                enrolledAtOverride: seedTime.AddDays(-5));
            await CompleteTheoryAsync(rebuy, student, attemptNumber: 2);
            await EnsureMe(rebuy, labModule, EnrollmentStatus.Active, 0m, attemptNumber: 2);
            await EnsureSeat(rebuy, ClassEnrollmentStatus.Active, eligibleClass);
            await RecalcProgramAsync(rebuy);
            rebuy.Status = EnrollmentStatus.Active;
            rebuy.EndReason = null;
            rebuy.EndedAt = null;
            rebuy.EndedModuleId = null;
            rebuy.CompletedAt = null;
            rebuy.SourceProgramEnrollmentId = source.Id;
            await _unitOfWork.ProgramEnrollments.Update(rebuy);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
