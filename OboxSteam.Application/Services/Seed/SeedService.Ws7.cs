using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private const string Ws7ProgramCode = "PRG-WS7";
    private const string Ws7TheoryModuleCode = "MOD-WS7-THEORY";
    private const string Ws7ExpModuleCode = "MOD-WS7-EXP";
    private const string Ws7TheoryAssignmentCode = "ASN-WS7-THEORY";
    private const string Ws7ExpAssignmentCode = "ASN-WS7-EXP";
    private const string Ws7SourceClassCode = "CLS-WS7-SOURCE";
    private const string Ws7OpenClassCode = "CLS-WS7-OPEN";
    private const string Ws7FullClassCode = "CLS-WS7-FULL";
    private const string Ws7RemedialClassCode = "CLS-WS7-REM";

    private static readonly string[] Ws7ScenarioStudentCodes =
    [
        "STD-WS7-A",
        "STD-WS7-B",
        "STD-WS7-C",
        "STD-WS7-D",
        "STD-WS7-E",
        "STD-WS7-F",
    ];

    private static readonly string[] Ws7SeatFillerCodes =
    [
        "STD-WS7-X1",
        "STD-WS7-X2",
        "STD-WS7-X3",
        "STD-WS7-X4",
        "STD-WS7-X5",
        "STD-WS7-X6",
        "STD-WS7-X7",
    ];

    public async Task SeedWs7FeTestDataAsync()
    {
        _seedNow = DateTime.UtcNow;
        _loggerService.LogInformation("Starting WS7 FE test seed");

        if (await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == Ws7ProgramCode && !p.IsDeleted) != null)
        {
            _loggerService.LogInformation("WS7 seed already applied (program {Code}); skipping.", Ws7ProgramCode);
            return;
        }

        var staff = await SeedWs7StaffAsync();
        var curriculum = await SeedWs7CurriculumAsync();
        var classes = await SeedWs7ClassesAsync(staff.MentorId, curriculum);
        await SeedWs7SharedEnrollmentsAsync(staff, curriculum, classes);
        await _unitOfWork.SaveChangesAsync();
        await SeedWs7ScenarioStatesAsync(staff, curriculum, classes);

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished WS7 FE test seed");
    }

    private async Task<(Guid MentorId, Guid ManagerId, Guid ParentId)> SeedWs7StaffAsync()
    {
        var mentor = await EnsureWs7UserAsync(
            "MNT-WS7",
            "ws7-mentor@oboxsteam.com",
            "WS7 Mentor",
            RoleType.Mentor,
            "Mentor@123");
        var manager = await EnsureWs7UserAsync(
            "MNG-WS7",
            "ws7-manager@oboxsteam.com",
            "WS7 Manager",
            RoleType.Manager,
            "Manager@123");
        var parent = await EnsureWs7UserAsync(
            "PRT-WS7",
            "ws7-parent@oboxsteam.com",
            "WS7 Parent",
            RoleType.Parent,
            "Parent@123");

        User? studentA = null;
        foreach (var code in Ws7ScenarioStudentCodes)
        {
            var student = await EnsureWs7UserAsync(
                code,
                $"{code.ToLowerInvariant()}@oboxsteam.com",
                $"WS7 Student {code[^1]}",
                RoleType.Student,
                "Student@123");
            if (code == "STD-WS7-A")
            {
                studentA = student;
            }
        }

        foreach (var code in Ws7SeatFillerCodes)
        {
            await EnsureWs7UserAsync(
                code,
                $"{code.ToLowerInvariant()}@oboxsteam.com",
                code,
                RoleType.Student,
                "Student@123");
        }

        if (studentA == null)
        {
            throw ErrorHelper.Internal("WS7 student A missing after seed.");
        }

        if (await _unitOfWork.ParentStudents.FirstOrDefaultAsync(
                ps => ps.ParentId == parent.Id && ps.StudentId == studentA.Id && !ps.IsDeleted) == null)
        {
            await _unitOfWork.ParentStudents.AddAsync(new ParentStudent
            {
                Id = Guid.NewGuid(),
                ParentId = parent.Id,
                StudentId = studentA.Id,
                IsVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        await _unitOfWork.SaveChangesAsync();

        return (mentor.Id, manager.Id, parent.Id);
    }

    private async Task<Ws7CurriculumRefs> SeedWs7CurriculumAsync()
    {
        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = Ws7ProgramCode,
            Name = "WS7 Retake Ladder Test",
            SeriesName = "WS7",
            Description = "Isolated program for WS7 FE retake / redelivery scenarios.",
            Level = DifficultyLevel.Beginner,
            Category = ProgramCategory.Technology,
            EstimatedDuration = "6 weeks",
            SkillsGained = "Recovery, redelivery, retake checkout",
            Status = ProgramStatus.Active,
            Price = 2_000_000m,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Programs.AddAsync(program);

        var theoryModule = new Module
        {
            Id = Guid.NewGuid(),
            Code = Ws7TheoryModuleCode,
            ProgramId = program.Id,
            Name = "WS7 Theory Foundations",
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsMandatory = true,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var expModule = new Module
        {
            Id = Guid.NewGuid(),
            Code = Ws7ExpModuleCode,
            ProgramId = program.Id,
            Name = "WS7 Experiential Lab",
            ModuleType = ModuleType.Experiential,
            ModuleOrder = 2,
            PrerequisiteModuleId = theoryModule.Id,
            IsMandatory = true,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Modules.AddRangeAsync([theoryModule, expModule]);

        var theoryCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CRS-WS7-THEORY",
            ModuleId = theoryModule.Id,
            Name = "WS7 Theory Course",
            CourseOrder = 1,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var expCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CRS-WS7-EXP",
            ModuleId = expModule.Id,
            Name = "WS7 Experiential Course",
            CourseOrder = 1,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Courses.AddRangeAsync([theoryCourse, expCourse]);

        await _unitOfWork.Activities.AddRangeAsync(
        [
            new Activity
            {
                Id = Guid.NewGuid(),
                Code = "ACT-WS7-THEORY",
                CourseId = theoryCourse.Id,
                Name = "WS7 Theory Reading",
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 1,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
            new Activity
            {
                Id = Guid.NewGuid(),
                Code = "ACT-WS7-EXP",
                CourseId = expCourse.Id,
                Name = "WS7 Lab Prep",
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 1,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
        ]);

        var theoryAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = Ws7TheoryAssignmentCode,
            ModuleId = theoryModule.Id,
            CourseId = theoryCourse.Id,
            Title = "WS7 Theory Check",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 10,
            PassScore = 5,
            MaxAttempts = 2,
            IsRequiredForModulePass = true,
            AvailableFrom = AtDays(-60),
            AvailableUntil = AtDays(120),
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var expAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = Ws7ExpAssignmentCode,
            ModuleId = expModule.Id,
            CourseId = expCourse.Id,
            Title = "WS7 Experiential Deliverable",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 10,
            PassScore = 5,
            MaxAttempts = 1,
            IsRequiredForModulePass = true,
            AvailableFrom = AtDays(-60),
            AvailableUntil = AtDays(120),
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Assignments.AddRangeAsync([theoryAssignment, expAssignment]);

        return new Ws7CurriculumRefs(program, theoryModule, expModule, theoryAssignment, expAssignment);
    }

    private async Task<Ws7ClassRefs> SeedWs7ClassesAsync(Guid mentorId, Ws7CurriculumRefs curriculum)
    {
        var sourceClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = Ws7SourceClassCode,
            Name = "WS7 Source Cohort",
            ProgramId = curriculum.Program.Id,
            MentorId = mentorId,
            StartDate = AtDays(-42).Date,
            EndDate = AtDays(42).Date,
            MaxCapacity = 20,
            Status = ClassStatus.InProgress,
            ScheduleSummary = "Mon & Wed 09:00-11:30",
            MinHoursBeforeAssignmentJoin = 48,
            CreatedAt = AtDays(-49),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var openClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = Ws7OpenClassCode,
            Name = "WS7 Open Cohort",
            ProgramId = curriculum.Program.Id,
            MentorId = mentorId,
            StartDate = AtDays(14).Date,
            EndDate = AtDays(98).Date,
            MaxCapacity = 10,
            Status = ClassStatus.Open,
            ScheduleSummary = "Sat & Sun 09:00-11:30",
            MinHoursBeforeAssignmentJoin = 48,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var fullClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = Ws7FullClassCode,
            Name = "WS7 Full Cohort",
            ProgramId = curriculum.Program.Id,
            MentorId = mentorId,
            StartDate = AtDays(21).Date,
            EndDate = AtDays(105).Date,
            MaxCapacity = 4,
            Status = ClassStatus.Open,
            ScheduleSummary = "Tue & Thu 18:00-20:30",
            MinHoursBeforeAssignmentJoin = 48,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        var remedialClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = Ws7RemedialClassCode,
            Name = "WS7 Remedial Intensive",
            ProgramId = curriculum.Program.Id,
            MentorId = mentorId,
            Kind = ClassKind.Remedial,
            RemedialModuleId = curriculum.ExpModule.Id,
            StartDate = AtDays(10).Date,
            EndDate = AtDays(45).Date,
            MaxCapacity = 8,
            Status = ClassStatus.Open,
            ScheduleSummary = "Daily intensive 14:00-17:00",
            MinHoursBeforeAssignmentJoin = 48,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Classes.AddRangeAsync([sourceClass, openClass, fullClass, remedialClass]);

        var sessions = new List<ClassSession>
        {
            BuildWs7Session(sourceClass.Id, curriculum.TheoryModule.Id, "WS7 Theory Live (done)",
                AtDays(-35), AtDays(-35).AddHours(2), ClassSessionStatus.Completed),
            BuildWs7Session(sourceClass.Id, curriculum.ExpModule.Id, "WS7 EXP started on source",
                AtDays(-7), AtDays(-7).AddHours(2), ClassSessionStatus.InProgress),
            BuildWs7Session(openClass.Id, curriculum.ExpModule.Id, "WS7 EXP future on open",
                AtDays(21), AtDays(21).AddHours(2), ClassSessionStatus.Scheduled),
            BuildWs7Session(openClass.Id, curriculum.ExpModule.Id, "WS7 EXP future session 2",
                AtDays(28), AtDays(28).AddHours(2), ClassSessionStatus.Scheduled),
            BuildWs7Session(fullClass.Id, curriculum.ExpModule.Id, "WS7 EXP future on full",
                AtDays(22), AtDays(22).AddHours(2), ClassSessionStatus.Scheduled),
            BuildWs7Session(remedialClass.Id, curriculum.ExpModule.Id, "WS7 Remedial intensive 1",
                AtDays(12), AtDays(12).AddHours(3), ClassSessionStatus.Scheduled),
            BuildWs7Session(remedialClass.Id, curriculum.ExpModule.Id, "WS7 Remedial intensive 2",
                AtDays(19), AtDays(19).AddHours(3), ClassSessionStatus.Scheduled),
        };
        await _unitOfWork.ClassSessions.AddRangeAsync(sessions);

        return new Ws7ClassRefs(sourceClass, openClass, fullClass, remedialClass);
    }

    private async Task SeedWs7SharedEnrollmentsAsync(
        (Guid MentorId, Guid ManagerId, Guid ParentId) staff,
        Ws7CurriculumRefs curriculum,
        Ws7ClassRefs classes)
    {
        var enrolledAt = AtDays(-40);
        var paidAt = AtDays(-39);

        foreach (var studentCode in Ws7ScenarioStudentCodes)
        {
            var student = await RequireUserByCodeAsync(studentCode);
            var programEnrollment = new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = curriculum.Program.Id,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 10m,
                EnrolledAt = enrolledAt,
                StartedAt = AtDays(-38),
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramEnrollments.AddAsync(programEnrollment);

            await _unitOfWork.Payments.AddAsync(new Payment
            {
                Id = Guid.NewGuid(),
                Code = $"PAY-{studentCode}",
                StudentId = student.Id,
                PaidById = student.Id,
                ProgramEnrollmentId = programEnrollment.Id,
                Amount = curriculum.Program.Price!.Value,
                Currency = "VND",
                Gateway = PaymentGateway.Stripe,
                Status = PaymentStatus.Success,
                PaidAt = paidAt,
                CreatedAt = paidAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });

            await _unitOfWork.ClassEnrollments.AddAsync(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = classes.SourceClass.Id,
                StudentId = student.Id,
                ProgramEnrollmentId = programEnrollment.Id,
                Kind = ClassEnrollmentKind.Primary,
                Status = ClassEnrollmentStatus.Active,
                EnrolledAt = enrolledAt,
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });

            await _unitOfWork.ModuleEnrollments.AddAsync(new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ModuleId = curriculum.TheoryModule.Id,
                ProgramEnrollmentId = programEnrollment.Id,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100m,
                AttemptNumber = 1,
                EnrolledAt = enrolledAt,
                StartedAt = AtDays(-38),
                CompletedAt = AtDays(-30),
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        await SeedWs7SeatFillersAsync(curriculum, classes, enrolledAt);
    }

    private async Task SeedWs7SeatFillersAsync(
        Ws7CurriculumRefs curriculum,
        Ws7ClassRefs classes,
        DateTime enrolledAt)
    {
        for (var i = 0; i < 3; i++)
        {
            await SeedWs7FillerClassSeatAsync(
                Ws7SeatFillerCodes[i],
                curriculum,
                classes.OpenClass.Id,
                enrolledAt);
        }

        for (var i = 3; i < 6; i++)
        {
            await SeedWs7FillerClassSeatAsync(
                Ws7SeatFillerCodes[i],
                curriculum,
                classes.FullClass.Id,
                enrolledAt);
        }

        await SeedWs7FillerClassSeatAsync(
            "STD-WS7-X7",
            curriculum,
            classes.SourceClass.Id,
            enrolledAt);
    }

    private async Task SeedWs7FillerClassSeatAsync(
        string studentCode,
        Ws7CurriculumRefs curriculum,
        Guid classId,
        DateTime enrolledAt)
    {
        var student = await RequireUserByCodeAsync(studentCode);
        var programEnrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ProgramId = curriculum.Program.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = enrolledAt,
            CreatedAt = enrolledAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramEnrollments.AddAsync(programEnrollment);
        await _unitOfWork.ClassEnrollments.AddAsync(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = student.Id,
            ProgramEnrollmentId = programEnrollment.Id,
            Kind = ClassEnrollmentKind.Primary,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = enrolledAt,
            CreatedAt = enrolledAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task SeedWs7ScenarioStatesAsync(
        (Guid MentorId, Guid ManagerId, Guid ParentId) staff,
        Ws7CurriculumRefs curriculum,
        Ws7ClassRefs classes)
    {
        await SeedWs7ScenarioAAsync(curriculum, classes);
        await SeedWs7ScenarioBAsync(curriculum, classes);
        await SeedWs7ScenarioCAsync(curriculum, classes);
        await SeedWs7ScenarioDAsync(curriculum, classes);
        await SeedWs7ScenarioEAsync(curriculum, classes);
        await SeedWs7ScenarioFAsync(curriculum, classes);
    }

    private async Task SeedWs7ScenarioAAsync(Ws7CurriculumRefs curriculum, Ws7ClassRefs classes)
    {
        var student = await RequireUserByCodeAsync("STD-WS7-A");
        var programEnrollment = await RequireProgramEnrollmentAsync(student.Id, curriculum.Program.Id);
        var expEnrollment = await AddWs7ModuleEnrollmentAsync(
            student.Id,
            curriculum.ExpModule.Id,
            programEnrollment.Id,
            EnrollmentStatus.Active,
            attemptNumber: 1);

        await AddWs7FailedSubmissionAsync(
            student.Id,
            curriculum.ExpAssignment,
            expEnrollment.Id,
            attemptNumber: 1,
            grade: 0m);
    }

    private async Task SeedWs7ScenarioBAsync(Ws7CurriculumRefs curriculum, Ws7ClassRefs classes)
    {
        var student = await RequireUserByCodeAsync("STD-WS7-B");
        var programEnrollment = await RequireProgramEnrollmentAsync(student.Id, curriculum.Program.Id);
        var expEnrollment = await AddWs7ModuleEnrollmentAsync(
            student.Id,
            curriculum.ExpModule.Id,
            programEnrollment.Id,
            EnrollmentStatus.Active,
            attemptNumber: 1);

        await AddWs7FailedSubmissionAsync(
            student.Id,
            curriculum.ExpAssignment,
            expEnrollment.Id,
            attemptNumber: 1,
            grade: 0m);

        await _unitOfWork.ClassRedeliveryRequests.AddAsync(new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ModuleEnrollmentId = expEnrollment.Id,
            ModuleId = curriculum.ExpModule.Id,
            SourceClassId = classes.SourceClass.Id,
            RequestedByUserId = student.Id,
            Status = ClassRedeliveryRequestStatus.AwaitingClassSelection,
            RequestMessage = "WS7 seed: pick a standard cohort",
            CreatedAt = AtDays(-2),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task SeedWs7ScenarioCAsync(
        Ws7CurriculumRefs curriculum,
        Ws7ClassRefs classes)
    {
        var studentC = await RequireUserByCodeAsync("STD-WS7-C");
        var peC = await RequireProgramEnrollmentAsync(studentC.Id, curriculum.Program.Id);

        var expC = await AddWs7ModuleEnrollmentAsync(
            studentC.Id, curriculum.ExpModule.Id, peC.Id, EnrollmentStatus.Active, 1);

        await AddWs7FailedSubmissionAsync(studentC.Id, curriculum.ExpAssignment, expC.Id, 1, 0m);

        await _unitOfWork.ClassRedeliveryRequests.AddAsync(new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = studentC.Id,
            ModuleEnrollmentId = expC.Id,
            ModuleId = curriculum.ExpModule.Id,
            SourceClassId = classes.SourceClass.Id,
            RequestedByUserId = studentC.Id,
            Status = ClassRedeliveryRequestStatus.PendingManager,
            CreatedAt = AtDays(-5),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });

        var filler = await RequireUserByCodeAsync("STD-WS7-X7");
        var fillerPe = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == filler.Id && pe.ProgramId == curriculum.Program.Id && !pe.IsDeleted);
        if (fillerPe == null)
        {
            throw ErrorHelper.Internal("WS7 waitlist filler program enrollment missing.");
        }

        var fillerExp = await AddWs7ModuleEnrollmentAsync(
            filler.Id, curriculum.ExpModule.Id, fillerPe.Id, EnrollmentStatus.Active, 1);
        await AddWs7FailedSubmissionAsync(filler.Id, curriculum.ExpAssignment, fillerExp.Id, 1, 0m);
        await _unitOfWork.ClassRedeliveryRequests.AddAsync(new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = filler.Id,
            ModuleEnrollmentId = fillerExp.Id,
            ModuleId = curriculum.ExpModule.Id,
            SourceClassId = classes.SourceClass.Id,
            RequestedByUserId = filler.Id,
            Status = ClassRedeliveryRequestStatus.PendingManager,
            CreatedAt = AtDays(-4),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task SeedWs7ScenarioDAsync(Ws7CurriculumRefs curriculum, Ws7ClassRefs classes)
    {
        var student = await RequireUserByCodeAsync("STD-WS7-D");
        var programEnrollment = await RequireProgramEnrollmentAsync(student.Id, curriculum.Program.Id);
        var expEnrollment = await AddWs7ModuleEnrollmentAsync(
            student.Id,
            curriculum.ExpModule.Id,
            programEnrollment.Id,
            EnrollmentStatus.Active,
            attemptNumber: 1);

        await AddWs7FailedSubmissionAsync(
            student.Id,
            curriculum.ExpAssignment,
            expEnrollment.Id,
            attemptNumber: 1,
            grade: 0m);

        await _unitOfWork.ClassRedeliveryRequests.AddAsync(new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ModuleEnrollmentId = expEnrollment.Id,
            ModuleId = curriculum.ExpModule.Id,
            SourceClassId = classes.SourceClass.Id,
            RequestedByUserId = student.Id,
            Status = ClassRedeliveryRequestStatus.AwaitingIntensiveConsent,
            TargetClassId = classes.RemedialClass.Id,
            RequestMessage = "WS7 seed: intensive remedial offer",
            CreatedAt = AtDays(-1),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task SeedWs7ScenarioEAsync(Ws7CurriculumRefs curriculum, Ws7ClassRefs classes)
    {
        var student = await RequireUserByCodeAsync("STD-WS7-E");
        var programEnrollment = await RequireProgramEnrollmentAsync(student.Id, curriculum.Program.Id);
        var sourceExpEnrollment = await AddWs7ModuleEnrollmentAsync(
            student.Id,
            curriculum.ExpModule.Id,
            programEnrollment.Id,
            EnrollmentStatus.Active,
            attemptNumber: 1);

        await AddWs7FailedSubmissionAsync(
            student.Id,
            curriculum.ExpAssignment,
            sourceExpEnrollment.Id,
            attemptNumber: 1,
            grade: 0m);

        var retakeEnrollment = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ModuleId = curriculum.ExpModule.Id,
            ProgramEnrollmentId = programEnrollment.Id,
            Status = EnrollmentStatus.PendingPayment,
            ProgressPercent = 0m,
            AttemptNumber = 2,
            EnrolledAt = AtDays(-1),
            CreatedAt = AtDays(-1),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.ModuleEnrollments.AddAsync(retakeEnrollment);

        await _unitOfWork.ClassRedeliveryRequests.AddAsync(new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ModuleEnrollmentId = sourceExpEnrollment.Id,
            ModuleId = curriculum.ExpModule.Id,
            SourceClassId = classes.SourceClass.Id,
            RequestedByUserId = student.Id,
            Status = ClassRedeliveryRequestStatus.MatchedPendingPayment,
            TargetClassId = classes.OpenClass.Id,
            RetakeModuleEnrollmentId = retakeEnrollment.Id,
            ResolutionType = RedeliveryResolutionType.StudentSelectedCohort,
            CreatedAt = AtDays(-1),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task SeedWs7ScenarioFAsync(Ws7CurriculumRefs curriculum, Ws7ClassRefs classes)
    {
        var student = await RequireUserByCodeAsync("STD-WS7-F");
        var programEnrollment = await RequireProgramEnrollmentAsync(student.Id, curriculum.Program.Id);
        await AddWs7ModuleEnrollmentAsync(
            student.Id,
            curriculum.ExpModule.Id,
            programEnrollment.Id,
            EnrollmentStatus.Completed,
            attemptNumber: 1,
            completedAt: AtDays(-3));

        await _unitOfWork.ClassEnrollments.AddAsync(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classes.RemedialClass.Id,
            StudentId = student.Id,
            ProgramEnrollmentId = programEnrollment.Id,
            Kind = ClassEnrollmentKind.Retake,
            Status = ClassEnrollmentStatus.Completed,
            EnrolledAt = AtDays(-20),
            CreatedAt = AtDays(-20),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task<User> EnsureWs7UserAsync(
        string code,
        string email,
        string fullName,
        RoleType role,
        string password)
    {
        var existing = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == code && !u.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Code = code,
            Email = email,
            PasswordHash = new PasswordHasher().HashPassword(password)!,
            FullName = fullName,
            Role = role,
            Status = AccountStatus.Active,
            IsEmailVerified = true,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Users.AddAsync(user);
        return user;
    }

    private async Task<User> RequireUserByCodeAsync(string code)
        => await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == code && !u.IsDeleted)
           ?? throw ErrorHelper.Internal($"WS7 seed user '{code}' not found.");

    private async Task<ProgramEnrollment> RequireProgramEnrollmentAsync(Guid studentId, Guid programId)
        => await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
               pe => pe.StudentId == studentId
                     && pe.ProgramId == programId
                     && !pe.IsDeleted)
           ?? throw ErrorHelper.Internal("WS7 program enrollment missing.");

    private async Task<ModuleEnrollment> AddWs7ModuleEnrollmentAsync(
        Guid studentId,
        Guid moduleId,
        Guid programEnrollmentId,
        EnrollmentStatus status,
        int attemptNumber,
        DateTime? completedAt = null)
    {
        var enrollment = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ModuleId = moduleId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = status,
            ProgressPercent = status == EnrollmentStatus.Completed ? 100m : 40m,
            AttemptNumber = attemptNumber,
            EnrolledAt = AtDays(-25),
            StartedAt = AtDays(-24),
            CompletedAt = completedAt,
            CreatedAt = AtDays(-25),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.ModuleEnrollments.AddAsync(enrollment);
        return enrollment;
    }

    private async Task AddWs7FailedSubmissionAsync(
        Guid studentId,
        Assignment assignment,
        Guid moduleEnrollmentId,
        int attemptNumber,
        decimal grade)
    {
        await _unitOfWork.Submissions.AddAsync(new Submission
        {
            Id = Guid.NewGuid(),
            Code = $"SUB-WS7-{Guid.NewGuid():N}"[..20],
            AssignmentId = assignment.Id,
            StudentId = studentId,
            ModuleEnrollmentId = moduleEnrollmentId,
            AttemptNumber = attemptNumber,
            Status = SubmissionStatus.Graded,
            AssignedGrade = grade,
            MentorFeedback = "WS7 seed: below pass score",
            SubmittedAt = AtDays(-5),
            GradedAt = AtDays(-4),
            CreatedAt = AtDays(-5),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private static ClassSession BuildWs7Session(
        Guid classId,
        Guid moduleId,
        string title,
        DateTime start,
        DateTime end,
        ClassSessionStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            Title = title,
            StartTime = start,
            EndTime = end,
            SessionKind = SessionKind.LiveOnline,
            Status = status,
            RequiresAttendance = true,
            CreatedAt = start.AddDays(-1),
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

    private sealed record Ws7CurriculumRefs(
        Program Program,
        Module TheoryModule,
        Module ExpModule,
        Assignment TheoryAssignment,
        Assignment ExpAssignment);

    private sealed record Ws7ClassRefs(
        Class SourceClass,
        Class OpenClass,
        Class FullClass,
        Class RemedialClass);
}
