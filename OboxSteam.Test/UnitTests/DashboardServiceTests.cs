using OboxSteam.Application.DTOs.DashboardDTO;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class DashboardServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _student2Id = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _class2Id = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _class3Id = Guid.Parse("46464646-4646-4646-4646-464646464646");
    private readonly Guid _programEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _programEnrollment2Id = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _moduleEnrollment2Id = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _classEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _paymentInRangeId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _paymentOutOfRangeId = Guid.Parse("89898989-8989-8989-8989-898989898989");
    private readonly Guid _paymentRequest1Id = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private readonly Guid _paymentRequest2Id = Guid.Parse("9a9a9a9a-9a9a-9a9a-9a9a-9a9a9a9a9a9a");
    private readonly Guid _invoice1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _invoice2Id = Guid.Parse("abababab-abab-abab-abab-abababababab");
    private readonly Guid _assignmentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly Guid _submissionInRangeId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly Guid _submissionGradedId = Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd");
    private readonly Guid _submissionBacklogId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly Guid _classSessionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private readonly Guid _mentorRequestId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private readonly DateTime _now = DateTime.UtcNow;

    private readonly InMemoryUnitOfWork _db = new();

    private DashboardService CreateSut() => new(_db);

    private static DashboardFilterDto Last30DaysFilter() => new()
    {
        Range = DashboardRange.Last30Days,
    };

    private Program SeedProgram()
    {
        var program = new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Robotics Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        };
        _db.Programs.Seed(program);
        return program;
    }

    private Module SeedModule(Program program)
    {
        var module = new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Intro Module",
            ProgramId = program.Id,
            Program = program,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsDeleted = false,
        };
        _db.Modules.Seed(module);
        return module;
    }

    private Course SeedCourse(Module module)
    {
        var course = new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Intro Course",
            ModuleId = module.Id,
            Module = module,
            IsDeleted = false,
        };
        _db.Courses.Seed(course);
        return course;
    }

    private User SeedUser(Guid id, RoleType role, string code, int? maxConcurrent = null)
    {
        var user = new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            Status = AccountStatus.Active,
            MaxConcurrentClasses = maxConcurrent,
            IsDeleted = false,
        };
        _db.Users.Seed(user);
        return user;
    }

    private ProgramEnrollment SeedProgramEnrollment(
        Guid id,
        Guid studentId,
        Program program,
        EnrollmentStatus status,
        DateTime? enrolledAt,
        ModuleEnrollment? moduleEnrollment = null,
        ClassEnrollment? classEnrollment = null)
    {
        var enrollment = new ProgramEnrollment
        {
            Id = id,
            StudentId = studentId,
            ProgramId = program.Id,
            Program = program,
            Status = status,
            EnrolledAt = enrolledAt,
            ModuleEnrollments = moduleEnrollment is null ? [] : [moduleEnrollment],
            ClassEnrollments = classEnrollment is null ? [] : [classEnrollment],
            IsDeleted = false,
        };

        if (moduleEnrollment is not null)
        {
            moduleEnrollment.ProgramEnrollmentId = enrollment.Id;
            moduleEnrollment.ProgramEnrollment = enrollment;
        }

        if (classEnrollment is not null)
        {
            classEnrollment.ProgramEnrollmentId = enrollment.Id;
            classEnrollment.ProgramEnrollment = enrollment;
        }

        _db.ProgramEnrollments.Seed(enrollment);
        return enrollment;
    }

    private ModuleEnrollment SeedModuleEnrollment(
        Guid id,
        Guid studentId,
        Module module,
        ProgramEnrollment programEnrollment)
    {
        var enrollment = new ModuleEnrollment
        {
            Id = id,
            StudentId = studentId,
            ModuleId = module.Id,
            Module = module,
            ProgramEnrollmentId = programEnrollment.Id,
            ProgramEnrollment = programEnrollment,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(enrollment);
        return enrollment;
    }

    private Class SeedClass(
        Guid id,
        Program program,
        ClassStatus status,
        Guid? mentorId,
        DateTime startDate,
        DateTime endDate,
        ICollection<ClassEnrollment>? enrollments = null,
        ICollection<ClassSession>? sessions = null)
    {
        var entity = new Class
        {
            Id = id,
            Code = $"CLS-{id.ToString()[..8]}",
            Name = $"Class {id.ToString()[..8]}",
            ProgramId = program.Id,
            Program = program,
            MentorId = mentorId,
            Status = status,
            MaxCapacity = 10,
            StartDate = startDate,
            EndDate = endDate,
            ClassEnrollments = enrollments ?? [],
            ClassSessions = sessions ?? [],
            IsDeleted = false,
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private ClassEnrollment SeedClassEnrollment(
        Guid id,
        Class classEntity,
        Guid studentId,
        ProgramEnrollment programEnrollment,
        ClassEnrollmentStatus status = ClassEnrollmentStatus.Active)
    {
        var enrollment = new ClassEnrollment
        {
            Id = id,
            ClassId = classEntity.Id,
            Class = classEntity,
            StudentId = studentId,
            ProgramEnrollmentId = programEnrollment.Id,
            ProgramEnrollment = programEnrollment,
            Status = status,
            EnrolledAt = _now,
            IsDeleted = false,
        };
        _db.ClassEnrollments.Seed(enrollment);
        return enrollment;
    }

    private void SeedRevenueData(Program program, ProgramEnrollment programEnrollment)
    {
        var paymentInRange = new Payment
        {
            Id = _paymentInRangeId,
            Code = "PAY-IN-RANGE",
            StudentId = _studentId,
            PaidById = _studentId,
            ProgramEnrollmentId = programEnrollment.Id,
            ProgramEnrollment = programEnrollment,
            Amount = 1000m,
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Success,
            PaidAt = _now,
            IsDeleted = false,
        };

        var paymentOutOfRange = new Payment
        {
            Id = _paymentOutOfRangeId,
            Code = "PAY-OLD",
            StudentId = _studentId,
            PaidById = _studentId,
            ProgramEnrollmentId = programEnrollment.Id,
            ProgramEnrollment = programEnrollment,
            Amount = 500m,
            Gateway = PaymentGateway.VnPay,
            Status = PaymentStatus.Success,
            PaidAt = _now.AddDays(-45),
            IsDeleted = false,
        };

        _db.Payments.Seed(paymentInRange, paymentOutOfRange);

        _db.PaymentRequests.Seed(
            new PaymentRequest
            {
                Id = _paymentRequest1Id,
                StudentId = _studentId,
                ParentId = _student2Id,
                ProgramId = program.Id,
                Program = program,
                Amount = 300m,
                Token = "token-1",
                ExpiresAt = _now.AddDays(7),
                Status = PaymentRequestStatus.Pending,
                IsDeleted = false,
            },
            new PaymentRequest
            {
                Id = _paymentRequest2Id,
                StudentId = _studentId,
                ParentId = _student2Id,
                ProgramId = program.Id,
                Program = program,
                Amount = 200m,
                Token = "token-2",
                ExpiresAt = _now.AddDays(7),
                Status = PaymentRequestStatus.Pending,
                IsDeleted = false,
            });

        _db.Invoices.Seed(
            new Invoice
            {
                Id = _invoice1Id,
                InvoiceNumber = "INV-001",
                PaymentId = _paymentInRangeId,
                Payment = paymentInRange,
                IssuedToId = _studentId,
                BillingName = "Alice",
                BillingEmail = "alice@test.com",
                ItemDescription = "Program fee",
                SubTotal = 1000m,
                TotalAmount = 1000m,
                Currency = "VND",
                IsDeleted = false,
            },
            new Invoice
            {
                Id = _invoice2Id,
                InvoiceNumber = "INV-002",
                PaymentId = _paymentOutOfRangeId,
                Payment = paymentOutOfRange,
                IssuedToId = _studentId,
                BillingName = "Alice",
                BillingEmail = "alice@test.com",
                ItemDescription = "Program fee",
                SubTotal = 500m,
                TotalAmount = 500m,
                Currency = "VND",
                IsDeleted = false,
            });
    }

    private void SeedAssessmentData(Module module, ModuleEnrollment moduleEnrollment)
    {
        var assignment = new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-001",
            Title = "Quiz 1",
            ModuleId = module.Id,
            Module = module,
            AssignmentType = AssignmentType.Quiz,
            MaxPoints = 100,
            PassScore = 60m,
            IsDeleted = false,
        };
        _db.Assignments.Seed(assignment);

        _db.Submissions.Seed(
            new Submission
            {
                Id = _submissionInRangeId,
                Code = "SUB-001",
                AssignmentId = assignment.Id,
                Assignment = assignment,
                StudentId = _studentId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ModuleEnrollment = moduleEnrollment,
                Status = SubmissionStatus.TurnedIn,
                SubmittedAt = _now.AddDays(-1),
                IsDeleted = false,
            },
            new Submission
            {
                Id = _submissionGradedId,
                Code = "SUB-002",
                AssignmentId = assignment.Id,
                Assignment = assignment,
                StudentId = _student2Id,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ModuleEnrollment = moduleEnrollment,
                Status = SubmissionStatus.Graded,
                AssignedGrade = 80m,
                SubmittedAt = _now.AddDays(-2),
                GradedAt = _now.AddDays(-1),
                IsDeleted = false,
            },
            new Submission
            {
                Id = _submissionBacklogId,
                Code = "SUB-003",
                AssignmentId = assignment.Id,
                Assignment = assignment,
                StudentId = _studentId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ModuleEnrollment = moduleEnrollment,
                Status = SubmissionStatus.TurnedIn,
                SubmittedAt = _now.AddHours(-DashboardService.GradingBacklogThresholdHours - 1),
                IsDeleted = false,
            });
    }

    private ClassSession SeedClassSession(Class classEntity, Module module)
    {
        var session = new ClassSession
        {
            Id = _classSessionId,
            ClassId = classEntity.Id,
            Class = classEntity,
            ModuleId = module.Id,
            Module = module,
            SessionKind = SessionKind.Lesson,
            Title = "Session 1",
            StartTime = _now.AddDays(-1),
            EndTime = _now.AddDays(-1).AddHours(2),
            IsDeleted = false,
        };
        _db.ClassSessions.Seed(session);
        return session;
    }

    private void SeedAttendanceData(ClassSession session, ModuleEnrollment moduleEnrollment)
    {
        _db.SessionAttendances.Seed(
            new SessionAttendance
            {
                Id = Guid.Parse("11112222-3333-4444-5555-666677778888"),
                ClassSessionId = session.Id,
                ClassSession = session,
                StudentId = _studentId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ModuleEnrollment = moduleEnrollment,
                Status = AttendanceStatus.Present,
                IsDeleted = false,
            },
            new SessionAttendance
            {
                Id = Guid.Parse("11112222-3333-4444-5555-666677778889"),
                ClassSessionId = session.Id,
                ClassSession = session,
                StudentId = _student2Id,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ModuleEnrollment = moduleEnrollment,
                Status = AttendanceStatus.Present,
                IsDeleted = false,
            },
            new SessionAttendance
            {
                Id = Guid.Parse("11112222-3333-4444-5555-66667777888a"),
                ClassSessionId = session.Id,
                ClassSession = session,
                StudentId = _studentId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ModuleEnrollment = moduleEnrollment,
                Status = AttendanceStatus.Absent,
                IsDeleted = false,
            });
    }

    private void SeedOperationsData(Program program, ProgramEnrollment programEnrollment, Module module)
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", maxConcurrent: 5);

        var classEnrollment = new ClassEnrollment
        {
            Id = _classEnrollmentId,
            StudentId = _studentId,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = _now,
            IsDeleted = false,
        };

        var openClass = SeedClass(
            _classId,
            program,
            ClassStatus.Open,
            _mentorId,
            _now.AddDays(-5),
            _now.AddDays(25),
            enrollments: [classEnrollment]);

        classEnrollment.ClassId = openClass.Id;
        classEnrollment.Class = openClass;
        classEnrollment.ProgramEnrollmentId = programEnrollment.Id;
        classEnrollment.ProgramEnrollment = programEnrollment;
        _db.ClassEnrollments.Seed(classEnrollment);

        SeedClass(
            _class2Id,
            program,
            ClassStatus.InProgress,
            _mentorId,
            _now.AddDays(-10),
            _now.AddDays(20));

        SeedClass(
            _class3Id,
            program,
            ClassStatus.Draft,
            null,
            _now.AddDays(5),
            _now.AddDays(35));

        var mentorRequestClass = SeedClass(
            Guid.Parse("48484848-4848-4848-4848-484848484848"),
            program,
            ClassStatus.Open,
            null,
            _now.AddDays(1),
            _now.AddDays(31));

        _db.ClassMentorRequests.Seed(new ClassMentorRequest
        {
            Id = _mentorRequestId,
            ClassId = mentorRequestClass.Id,
            Class = mentorRequestClass,
            MentorId = _mentorId,
            Status = ClassMentorRequestStatus.Pending,
            IsDeleted = false,
        });

        var session = SeedClassSession(openClass, module);
        var moduleEnrollment = _db.ModuleEnrollments.Items.First();
        SeedAttendanceData(session, moduleEnrollment);
    }

    private void SeedFullDashboardData()
    {
        SeedUser(_studentId, RoleType.Student, "STU-001");
        SeedUser(_student2Id, RoleType.Student, "STU-002");

        var program = SeedProgram();
        var module = SeedModule(program);
        SeedCourse(module);

        var moduleEnrollment1 = new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = module.Id,
            Module = module,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        };

        var moduleEnrollment2 = new ModuleEnrollment
        {
            Id = _moduleEnrollment2Id,
            StudentId = _student2Id,
            ModuleId = module.Id,
            Module = module,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        };

        var programEnrollment1 = SeedProgramEnrollment(
            _programEnrollmentId,
            _studentId,
            program,
            EnrollmentStatus.Active,
            _now.AddDays(-3),
            moduleEnrollment1);

        SeedProgramEnrollment(
            _programEnrollment2Id,
            _student2Id,
            program,
            EnrollmentStatus.Completed,
            _now.AddDays(-60));

        _db.ModuleEnrollments.Seed(moduleEnrollment1, moduleEnrollment2);

        SeedRevenueData(program, programEnrollment1);
        SeedAssessmentData(module, moduleEnrollment1);
        SeedOperationsData(program, programEnrollment1, module);
    }

    // ── Smoke: GetOverviewAsync / GetLandingAsync ─────────────────────────────

    [Fact]
    public async Task GetOverviewAsync_ReturnsNonNullKpiSections()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetOverviewAsync(Last30DaysFilter());

        Assert.NotNull(result.Revenue);
        Assert.NotNull(result.Enrollment);
        Assert.NotNull(result.Assessment);
        Assert.NotNull(result.Operations);
        Assert.True(result.Revenue.TotalRevenue > 0);
        Assert.True(result.Enrollment.TotalPrograms > 0);
        Assert.True(result.Assessment.TotalSubmissions > 0);
        Assert.True(result.Operations.ActiveClassCount > 0);
    }

    [Fact]
    public async Task GetLandingAsync_ReturnsNonNullOverviewSections()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetLandingAsync(Last30DaysFilter());

        Assert.NotNull(result.Revenue);
        Assert.NotNull(result.Enrollment);
        Assert.NotNull(result.Assessment);
        Assert.NotNull(result.Operations);
        Assert.NotEmpty(result.Revenue.RevenueTrend.Points);
        Assert.NotEmpty(result.Enrollment.EnrollmentTrend.Points);
        Assert.NotEmpty(result.Assessment.SubmissionsTrend.Points);
        Assert.NotEmpty(result.Operations.AttendanceTrend.Points);
    }

    // ── GetRevenueOverviewAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetRevenueOverviewAsync_TotalRevenue_SumsSuccessPayments()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetRevenueOverviewAsync(Last30DaysFilter());

        Assert.Equal(1500m, result.TotalRevenue);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_RevenueInRange_OnlyCountsPaidInWindow()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetRevenueOverviewAsync(Last30DaysFilter());

        Assert.Equal(1000m, result.RevenueInRange);
        Assert.Equal(1000m, result.AverageOrderValue);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_PendingPaymentRequests_CountAndAmount()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetRevenueOverviewAsync(Last30DaysFilter());

        Assert.Equal(2, result.PendingPaymentRequestsCount);
        Assert.Equal(500m, result.PendingPaymentRequestsAmount);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_InvoiceCount_ReturnsNonDeletedInvoices()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetRevenueOverviewAsync(Last30DaysFilter());

        Assert.Equal(2, result.InvoiceCount);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_CustomDateRange_UsesFromAndToDate()
    {
        SeedFullDashboardData();
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            FromDate = _now.AddDays(-10),
            ToDate = _now,
        };

        var result = await sut.GetRevenueOverviewAsync(filter);

        Assert.Equal(1000m, result.RevenueInRange);
        Assert.Equal(DashboardTrendGranularity.Daily, result.RevenueTrend.Granularity);
    }

    // ── GetEnrollmentOverviewAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetEnrollmentOverviewAsync_ProgramModuleCourseCounts()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetEnrollmentOverviewAsync(Last30DaysFilter());

        Assert.Equal(1, result.TotalPrograms);
        Assert.Equal(1, result.TotalModules);
        Assert.Equal(1, result.TotalCourses);
    }

    [Fact]
    public async Task GetEnrollmentOverviewAsync_ActiveStudents_DistinctActiveEnrollments()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetEnrollmentOverviewAsync(Last30DaysFilter());

        Assert.Equal(1, result.ActiveStudents);
    }

    [Fact]
    public async Task GetEnrollmentOverviewAsync_Last30DaysRange_CountsNewEnrollmentsInRange()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetEnrollmentOverviewAsync(Last30DaysFilter());

        Assert.Equal(1, result.NewEnrollmentsInRange);
        Assert.Equal(50m, result.CompletionRate);
    }

    // ── GetAssessmentOverviewAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetAssessmentOverviewAsync_TotalAndInRangeSubmissions()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetAssessmentOverviewAsync(Last30DaysFilter());

        Assert.Equal(3, result.TotalSubmissions);
        Assert.Equal(3, result.SubmissionsInRange);
    }

    [Fact]
    public async Task GetAssessmentOverviewAsync_GradingBacklog_CountsStalePendingSubmissions()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetAssessmentOverviewAsync(Last30DaysFilter());

        Assert.Equal(1, result.GradingBacklogCount);
        Assert.Equal(DashboardService.GradingBacklogThresholdHours, result.GradingBacklogThresholdHours);
    }

    [Fact]
    public async Task GetAssessmentOverviewAsync_PassRate_FromGradedSubmissions()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetAssessmentOverviewAsync(Last30DaysFilter());

        Assert.Equal(100m, result.PassRate);
        Assert.Equal(80m, result.AverageScore);
    }

    // ── GetOperationsOverviewAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetOperationsOverviewAsync_ClassesByStatus()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetOperationsOverviewAsync(Last30DaysFilter());

        var openCount = result.ClassesByStatus.First(x => x.Status == nameof(ClassStatus.Open)).Count;
        var inProgressCount = result.ClassesByStatus.First(x => x.Status == nameof(ClassStatus.InProgress)).Count;
        var draftCount = result.ClassesByStatus.First(x => x.Status == nameof(ClassStatus.Draft)).Count;

        Assert.Equal(2, openCount);
        Assert.Equal(1, inProgressCount);
        Assert.Equal(1, draftCount);
    }

    [Fact]
    public async Task GetOperationsOverviewAsync_MentorUtilization_AssignedAndPending()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetOperationsOverviewAsync(Last30DaysFilter());

        var mentor = Assert.Single(result.MentorUtilization.Items);
        Assert.Equal(_mentorId, mentor.MentorId);
        Assert.Equal(2, mentor.Assigned);
        Assert.Equal(1, mentor.Pending);
        Assert.Equal(5, mentor.Max);
    }

    [Fact]
    public async Task GetOperationsOverviewAsync_AverageAttendanceRate()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetOperationsOverviewAsync(Last30DaysFilter());

        Assert.Equal(66.67m, result.AverageAttendanceRate);
    }

    [Fact]
    public async Task GetOperationsOverviewAsync_PendingMentorRequestsCount()
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetOperationsOverviewAsync(Last30DaysFilter());

        Assert.Equal(1, result.PendingMentorRequestsCount);
    }

    [Fact]
    public async Task GetOperationsOverviewAsync_CustomDateRange_AttendanceTrend()
    {
        SeedFullDashboardData();
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            FromDate = _now.AddDays(-7),
            ToDate = _now,
        };

        var result = await sut.GetOperationsOverviewAsync(filter);

        Assert.NotEmpty(result.AttendanceTrend.Points);
        Assert.Equal(DashboardTrendGranularity.Daily, result.AttendanceTrend.Granularity);
        Assert.True(result.AverageCapacityUtilization > 0);
    }

    [Fact]
    public async Task GetOverviewAsync_CustomDateRange_MapsActiveClassCount()
    {
        SeedFullDashboardData();
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            FromDate = _now.AddDays(-14),
            ToDate = _now,
        };

        var result = await sut.GetOverviewAsync(filter);

        Assert.Equal(3, result.Operations.ActiveClassCount);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_EmptyData_ReturnsZeros()
    {
        var sut = CreateSut();

        var result = await sut.GetRevenueOverviewAsync(Last30DaysFilter());

        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(0, result.InvoiceCount);
        Assert.Empty(result.RevenueByGateway);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_OrphanProgramEnrollmentFk_DoesNotThrow()
    {
        SeedFullDashboardData();
        // Simulates EF query-filter behavior: FK set, soft-deleted enrollment navigation null.
        _db.Payments.Seed(new Payment
        {
            Id = Guid.Parse("aeaeaeae-aeae-aeae-aeae-aeaeaeaeaeae"),
            Code = "PAY-ORPHAN-PE",
            StudentId = _studentId,
            PaidById = _studentId,
            ProgramEnrollmentId = Guid.Parse("bfbfbfbf-bfbf-bfbf-bfbf-bfbfbfbfbfbf"),
            ProgramEnrollment = null,
            Amount = 777m,
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Success,
            PaidAt = _now,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.GetRevenueOverviewAsync(Last30DaysFilter());

        Assert.Equal(2277m, result.TotalRevenue);
        Assert.Single(result.TopProgramsByRevenue.Items);
        Assert.Equal(1500m, result.TopProgramsByRevenue.Items[0].Amount);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_RefundedAmount_AndGatewayBreakdown()
    {
        SeedFullDashboardData();
        var program = _db.Programs.Items.Single();
        var pe = _db.ProgramEnrollments.Items.First();
        _db.Payments.Seed(new Payment
        {
            Id = Guid.Parse("acacacac-acac-acac-acac-acacacacacac"),
            Code = "PAY-REF",
            StudentId = _studentId,
            PaidById = _studentId,
            ProgramEnrollmentId = pe.Id,
            ProgramEnrollment = pe,
            Amount = 250m,
            Gateway = PaymentGateway.BankTransfer,
            Status = PaymentStatus.Refunded,
            PaidAt = _now,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.GetRevenueOverviewAsync(Last30DaysFilter());

        Assert.Equal(250m, result.RefundedAmount);
        Assert.Contains(result.RevenueByGateway, g => g.Gateway == PaymentGateway.Stripe);
        Assert.Single(result.TopProgramsByRevenue.Items);
        Assert.Equal(program.Name, result.TopProgramsByRevenue.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_ProgramScope_FiltersPayments()
    {
        SeedFullDashboardData();
        var otherProgramId = Guid.Parse("adadadad-adad-adad-adad-adadadadadad");
        _db.Programs.Seed(new Program
        {
            Id = otherProgramId,
            Code = "PRG-002",
            Name = "Other",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            Range = DashboardRange.Last30Days,
            ProgramId = otherProgramId,
        };

        var result = await sut.GetRevenueOverviewAsync(filter);

        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(0, result.InvoiceCount);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_ModuleEnrollmentPayment_ScopedByModule()
    {
        var program = SeedProgram();
        var module = SeedModule(program);
        SeedUser(_studentId, RoleType.Student, "STU-001");
        var pe = SeedProgramEnrollment(
            _programEnrollmentId,
            _studentId,
            program,
            EnrollmentStatus.Active,
            _now);
        var me = SeedModuleEnrollment(_moduleEnrollmentId, _studentId, module, pe);
        _db.Payments.Seed(new Payment
        {
            Id = _paymentInRangeId,
            Code = "PAY-MOD",
            StudentId = _studentId,
            PaidById = _studentId,
            ModuleEnrollmentId = me.Id,
            ModuleEnrollment = me,
            Amount = 400m,
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Success,
            PaidAt = _now,
            IsDeleted = false,
        });
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            Range = DashboardRange.Last30Days,
            ModuleId = module.Id,
        };

        var result = await sut.GetRevenueOverviewAsync(filter);

        Assert.Equal(400m, result.TotalRevenue);
    }

    [Fact]
    public async Task GetEnrollmentOverviewAsync_ProgramAndClassFilters()
    {
        SeedFullDashboardData();
        var sut = CreateSut();
        var programFilter = new DashboardFilterDto
        {
            Range = DashboardRange.Last30Days,
            ProgramId = _programId,
            EnrollmentStatus = EnrollmentStatus.Active,
            ClassEnrollmentStatus = ClassEnrollmentStatus.Active,
        };
        var classFilter = new DashboardFilterDto
        {
            Range = DashboardRange.Last30Days,
            ClassId = _classId,
        };

        var byProgram = await sut.GetEnrollmentOverviewAsync(programFilter);
        var byClass = await sut.GetEnrollmentOverviewAsync(classFilter);

        Assert.Equal(1, byProgram.ActiveStudents);
        Assert.True(byProgram.ProgramEnrollmentsByStatus.Count > 0);
        Assert.Equal(1, byClass.ClassEnrollmentsByStatus.Sum(x => x.Count));
    }

    [Fact]
    public async Task GetAssessmentOverviewAsync_SubmissionStatusFilter()
    {
        SeedFullDashboardData();
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            Range = DashboardRange.Last30Days,
            SubmissionStatus = SubmissionStatus.Graded,
            ModuleId = _moduleId,
        };

        var result = await sut.GetAssessmentOverviewAsync(filter);

        Assert.Equal(1, result.TotalSubmissions);
        Assert.Equal(100m, result.PassRate);
    }

    [Fact]
    public async Task GetOperationsOverviewAsync_ProgramAndClassFilters()
    {
        SeedFullDashboardData();
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            Range = DashboardRange.Last30Days,
            ProgramId = _programId,
            ClassId = _classId,
            ClassStatus = ClassStatus.Open,
            SortBy = "name",
            IsDescending = false,
            Page = 1,
            PageSize = 10,
        };

        var result = await sut.GetOperationsOverviewAsync(filter);

        Assert.Single(result.ClassesByStatus, x => x.Count > 0);
        Assert.Equal(0, result.PendingMentorRequestsCount);
    }

    [Theory]
    [InlineData(DashboardRange.Last7Days, DashboardTrendGranularity.Daily)]
    [InlineData(DashboardRange.Last90Days, DashboardTrendGranularity.Weekly)]
    [InlineData(DashboardRange.Last12Months, DashboardTrendGranularity.Monthly)]
    public async Task GetLandingAsync_PresetRanges_ReturnTrendGranularity(
        DashboardRange range,
        DashboardTrendGranularity expectedGranularity)
    {
        SeedFullDashboardData();
        var sut = CreateSut();

        var result = await sut.GetLandingAsync(new DashboardFilterDto { Range = range });

        Assert.Equal(expectedGranularity, result.Revenue.RevenueTrend.Granularity);
    }

    [Fact]
    public async Task GetRevenueOverviewAsync_ReversedCustomDates_SwapsRange()
    {
        SeedFullDashboardData();
        var sut = CreateSut();
        var filter = new DashboardFilterDto
        {
            FromDate = _now,
            ToDate = _now.AddDays(-10),
        };

        var result = await sut.GetRevenueOverviewAsync(filter);

        Assert.Equal(1000m, result.RevenueInRange);
    }
}
