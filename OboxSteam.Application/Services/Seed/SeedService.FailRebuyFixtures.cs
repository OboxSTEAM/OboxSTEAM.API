using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    internal const string FailRebuyQuizCode = "ASG-FAILREBUY-QUIZ";
    internal const string FailRebuyUploadCode = "ASG-FAILREBUY-UPLOAD";
    internal const string FailRebuyResearchAssignmentCode = "ASG-FAILREBUY-RS01";
    internal const string FailRebuyResearchMilestoneCode = "RML-FAILREBUY-01";
    internal const string FailRebuyExperientialModuleCode = "MOD-FAILREBUY-01";
    internal const string FailRebuyResearchModuleCode = "MOD-FAILREBUY-02";

    /// <summary>
    /// Isolated program + class so attendance (5 session activities) and academic
    /// close wires can be exercised without touching Robotics/demo showcase data.
    /// Must run after demo submission clearing and before payment seed.
    /// </summary>
    private async Task SeedFailRebuyFixturesAsync()
    {
        _loggerService.LogInformation("Starting seed fail/rebuy test fixtures");

        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == FailRebuyMentorCode && !u.IsDeleted);
        if (mentor == null)
        {
            _loggerService.LogWarning(
                "Mentor {MentorCode} missing. Skipping fail/rebuy fixtures.",
                FailRebuyMentorCode);
            return;
        }

        var students = (await _unitOfWork.Users.GetAllAsync(
                u => u.Role == RoleType.Student && !u.IsDeleted))
            .ToDictionary(u => u.Code, u => u, StringComparer.OrdinalIgnoreCase);

        foreach (var code in FailRebuyClosedStudentCodes.Concat(FailRebuyActiveStudentCodes))
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
        var experientialModule = await EnsureFailRebuyModuleAsync(
            program.Id,
            FailRebuyExperientialModuleCode,
            "Fail Rebuy Lab",
            ModuleType.Experiential,
            moduleOrder: 1,
            seedTime);
        var researchModule = await EnsureFailRebuyModuleAsync(
            program.Id,
            FailRebuyResearchModuleCode,
            "Fail Rebuy Research",
            ModuleType.Research,
            moduleOrder: 2,
            seedTime);

        var labCourse = await EnsureFailRebuyCourseAsync(
            experientialModule.Id,
            "CRS-FAILREBUY-01",
            "Lab Sessions",
            1,
            seedTime);
        var researchCourse = await EnsureFailRebuyCourseAsync(
            researchModule.Id,
            "CRS-FAILREBUY-02",
            "Research Studio",
            1,
            seedTime);

        var activities = new List<Activity>(5);
        for (var i = 1; i <= 5; i++)
        {
            activities.Add(await EnsureFailRebuyActivityAsync(
                labCourse.Id,
                $"ACT-FAILREBUY-01-0{i}",
                $"Lab Session {i}",
                i,
                seedTime));
        }

        var bank = await EnsureFailRebuyQuestionBankAsync(labCourse.Id, seedTime);
        var quiz = await EnsureFailRebuyQuizAsync(experientialModule.Id, labCourse.Id, bank.Id, seedTime);
        var upload = await EnsureFailRebuyUploadAsync(experientialModule.Id, labCourse.Id, seedTime);
        var (researchAssignment, researchMilestone) = await EnsureFailRebuyResearchAsync(
            researchModule.Id,
            seedTime);

        var classEntity = await EnsureFailRebuyClassAsync(program.Id, mentor.Id, seedTime);
        await EnsureFailRebuySessionsAsync(classEntity, experientialModule.Id, activities, seedTime);

        await EnsureFailRebuyEnrollmentsAsync(
            students,
            program,
            classEntity,
            experientialModule,
            researchModule,
            quiz,
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
            await _unitOfWork.Programs.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return existing;
        }

        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyProgramCode,
            Name = "Fail / Rebuy Test Track",
            SeriesName = "QA Fixtures",
            Description = "Isolated track for testing program close (attendance, academic fail, withdraw).",
            Level = DifficultyLevel.Beginner,
            Category = ProgramCategory.Technology,
            EstimatedDuration = "2 weeks",
            SkillsGained = "QA close-path coverage",
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
        DateTime seedTime)
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
            IsMandatory = true,
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
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Code = code,
            CourseId = courseId,
            Name = name,
            Description = name,
            ActivityType = ActivityType.LiveOnline,
            ActivityOrder = order,
            DurationMinutes = 90,
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

    private async Task<QuestionBank> EnsureFailRebuyQuestionBankAsync(Guid courseId, DateTime seedTime)
    {
        var existing = await _unitOfWork.QuestionBanks.FirstOrDefaultAsync(
            qb => qb.CourseId == courseId && qb.Name == "Fail Rebuy Quiz Bank" && !qb.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var bank = new QuestionBank
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = "Fail Rebuy Quiz Bank",
            Description = "Single easy question so a wrong answer reliably fails the quiz.",
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
            QuestionText = "What is 1 + 1?",
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

        var texts = new[] { "2", "3", "4", "5" };
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
        Guid moduleId,
        Guid courseId,
        Guid bankId,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == FailRebuyQuizCode && !a.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var quiz = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyQuizCode,
            ModuleId = moduleId,
            CourseId = courseId,
            Title = "Fail Rebuy Quiz",
            Description = "One attempt. Two decided recoveries are pre-seeded on trigger students.",
            AssignmentType = AssignmentType.Quiz,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(30),
            AvailableFrom = seedTime.AddDays(-7),
            AvailableUntil = seedTime.AddDays(60),
            AllowShuffle = false,
            ShuffleOptions = false,
            QuestionBankId = bankId,
            QuestionCount = 1,
            EasyPercent = 100,
            MediumPercent = 0,
            HardPercent = 0,
            TimeLimitMinutes = 15,
            MaxAttempts = 1,
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
            Title = "Fail Rebuy File Upload",
            Description = "Turned-in work waiting for a failing grade.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(30),
            AvailableFrom = seedTime.AddDays(-7),
            AvailableUntil = seedTime.AddDays(60),
            MaxAttempts = 1,
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
            Title = "Fail Rebuy Research Upload",
            Description = "Research deliverable waiting for a failing grade.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(30),
            AvailableFrom = seedTime.AddDays(-7),
            AvailableUntil = seedTime.AddDays(60),
            MaxAttempts = 1,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var milestone = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyResearchMilestoneCode,
            ModuleId = researchModuleId,
            Title = "Fail Rebuy Milestone",
            Description = "Single research milestone for close-path tests.",
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

    private async Task<Class> EnsureFailRebuyClassAsync(Guid programId, Guid mentorId, DateTime seedTime)
    {
        var existing = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == FailRebuyClassCode && !c.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var classEntity = new Class
        {
            Id = Guid.NewGuid(),
            Code = FailRebuyClassCode,
            Name = "Fail / Rebuy Current Cohort",
            ProgramId = programId,
            MentorId = mentorId,
            StartDate = seedTime.AddDays(-14),
            EndDate = seedTime.AddDays(42),
            MaxCapacity = 16,
            Kind = ClassKind.Standard,
            Status = ClassStatus.InProgress,
            MinHoursBeforeAssignmentJoin = 48,
            ScheduleSummary = "Weekday lab blocks",
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Classes.AddAsync(classEntity);
        await _unitOfWork.SaveChangesAsync();
        return classEntity;
    }

    private async Task EnsureFailRebuySessionsAsync(
        Class classEntity,
        Guid moduleId,
        IReadOnlyList<Activity> activities,
        DateTime seedTime)
    {
        for (var i = 0; i < activities.Count; i++)
        {
            var activity = activities[i];
            var existing = await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                cs => cs.ClassId == classEntity.Id
                      && cs.ActivityId == activity.Id
                      && !cs.IsDeleted);
            if (existing != null)
            {
                continue;
            }

            var isLive = i == 0;
            var start = isLive ? seedTime.AddHours(-1) : seedTime.AddDays(-10 + i).Date.AddHours(9);
            var session = new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = classEntity.Id,
                ModuleId = moduleId,
                ActivityId = activity.Id,
                SessionKind = SessionKind.LiveOnline,
                Title = activity.Name,
                StartTime = start,
                EndTime = start.AddMinutes(90),
                MeetingUrl = "https://meet.example.com/fail-rebuy",
                RequiresAttendance = true,
                Status = isLive ? ClassSessionStatus.InProgress : ClassSessionStatus.Completed,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ClassSessions.AddAsync(session);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureFailRebuyEnrollmentsAsync(
        Dictionary<string, User> students,
        Program program,
        Class classEntity,
        Module experientialModule,
        Module researchModule,
        Assignment quiz,
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
            DateTime? endedAt)
        {
            var student = students[studentCode];
            var existing = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student.Id
                      && pe.ProgramId == program.Id
                      && !pe.IsDeleted);
            if (existing != null)
            {
                return existing;
            }

            var enrollment = new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = program.Id,
                Status = status,
                ProgressPercent = status == EnrollmentStatus.Active ? 20m : 15m,
                EnrolledAt = seedTime.AddDays(-20),
                StartedAt = seedTime.AddDays(-18),
                EndReason = endReason,
                EndedModuleId = endedModuleId,
                EndedAt = endedAt,
                CreatedAt = seedTime.AddDays(-20),
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
            EnrollmentStatus status)
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
                ProgressPercent = status == EnrollmentStatus.Failed ? 10m : 20m,
                AttemptNumber = 1,
                EnrolledAt = pe.EnrolledAt,
                StartedAt = pe.StartedAt,
                CreatedAt = pe.EnrolledAt ?? seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ModuleEnrollments.AddAsync(enrollment);
            await _unitOfWork.SaveChangesAsync();
            return enrollment;
        }

        async Task EnsureSeat(ProgramEnrollment pe, ClassEnrollmentStatus status)
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
                    ClassId = classEntity.Id,
                    Status = status,
                    StudentMessage = "Seeded recovery for fail/rebuy close tests.",
                    ExtraAttemptsGranted = 0,
                    DecidedAt = status == AssessmentRecoveryRequestStatus.Pending
                        ? null
                        : seedTime.AddDays(-2),
                    DecidedBy = status == AssessmentRecoveryRequestStatus.Pending ? null : classEntity.MentorId,
                    CreatedAt = seedTime.AddDays(-3),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            await _unitOfWork.SaveChangesAsync();
        }

        async Task EnsureGradedFail(User student, ModuleEnrollment me, Assignment assignment, Guid? researchMilestoneId)
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
                Status = SubmissionStatus.Graded,
                AssignedGrade = 10m,
                ContentText = "Seeded failing attempt.",
                SubmittedAt = seedTime.AddDays(-4),
                GradedAt = seedTime.AddDays(-3),
                CreatedAt = seedTime.AddDays(-4),
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }

        async Task EnsureTurnedIn(User student, ModuleEnrollment me, Assignment assignment, Guid? researchMilestoneId)
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
                Status = SubmissionStatus.TurnedIn,
                ContentText = "Seeded work waiting for a failing grade.",
                FileUrl = "https://cdn.example.com/fail-rebuy/work.pdf",
                SubmittedAt = seedTime.AddDays(-1),
                CreatedAt = seedTime.AddDays(-1),
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }

        var pe026 = await EnsurePe(
            "STD-026",
            EnrollmentStatus.Failed,
            ProgramPurchaseEndReason.Attendance,
            experientialModule.Id,
            seedTime.AddDays(-1));
        var me026 = await EnsureMe(pe026, experientialModule, EnrollmentStatus.Failed);
        await EnsureSeat(pe026, ClassEnrollmentStatus.Withdrawn);
        var liveSession = (await _unitOfWork.ClassSessions.GetAllAsync(
                cs => cs.ClassId == classEntity.Id && !cs.IsDeleted))
            .OrderBy(cs => cs.StartTime)
            .FirstOrDefault();
        if (liveSession != null)
        {
            var existingAbsent = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(
                sa => sa.ClassSessionId == liveSession.Id
                      && sa.StudentId == pe026.StudentId
                      && !sa.IsDeleted);
            if (existingAbsent == null)
            {
                await _unitOfWork.SessionAttendances.AddAsync(new SessionAttendance
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = liveSession.Id,
                    StudentId = pe026.StudentId,
                    ModuleEnrollmentId = me026.Id,
                    Status = AttendanceStatus.Absent,
                    RecordedBy = classEntity.MentorId,
                    CheckedInAt = seedTime.AddDays(-1),
                    CreatedAt = seedTime.AddDays(-1),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
                await _unitOfWork.SaveChangesAsync();
            }
        }

        var pe027 = await EnsurePe(
            "STD-027",
            EnrollmentStatus.Failed,
            ProgramPurchaseEndReason.AcademicFail,
            experientialModule.Id,
            seedTime.AddDays(-1));
        var me027 = await EnsureMe(pe027, experientialModule, EnrollmentStatus.Failed);
        await EnsureSeat(pe027, ClassEnrollmentStatus.Withdrawn);
        await EnsureGradedFail(students["STD-027"], me027, quiz, researchMilestoneId: null);
        await EnsureRecoveries(
            students["STD-027"],
            me027,
            quiz,
            AssessmentRecoveryRequestStatus.Rejected,
            AssessmentRecoveryRequestStatus.Rejected);

        foreach (var code in FailRebuyActiveStudentCodes)
        {
            var pe = await EnsurePe(code, EnrollmentStatus.Active, null, null, null);
            var labMe = await EnsureMe(pe, experientialModule, EnrollmentStatus.Active);
            await EnsureMe(pe, researchModule, EnrollmentStatus.Active);
            await EnsureSeat(pe, ClassEnrollmentStatus.Active);

            if (code is "STD-030" or "STD-033")
            {
                await EnsureRecoveries(
                    students[code],
                    labMe,
                    quiz,
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
                await EnsureTurnedIn(students[code], labMe, upload, researchMilestoneId: null);
            }

            if (code == "STD-033")
            {
                await EnsureGradedFail(students[code], labMe, quiz, researchMilestoneId: null);
            }
        }

        var pe032 = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == students["STD-032"].Id
                  && pe.ProgramId == program.Id
                  && !pe.IsDeleted);
        if (pe032 != null)
        {
            var researchMe = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.ProgramEnrollmentId == pe032.Id
                      && me.ModuleId == researchModule.Id
                      && !me.IsDeleted);
            if (researchMe != null)
            {
                await EnsureRecoveries(
                    students["STD-032"],
                    researchMe,
                    researchAssignment,
                    AssessmentRecoveryRequestStatus.Rejected,
                    AssessmentRecoveryRequestStatus.Rejected);
                await EnsureTurnedIn(
                    students["STD-032"],
                    researchMe,
                    researchAssignment,
                    researchMilestone.Id);
            }
        }
    }
}
