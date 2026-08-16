using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Rich, range-aware dashboard seed so every preset feels like a live platform:
/// Last7Days / Last30Days (daily), Last90Days (near-daily), Last12Months (weekly),
/// plus matching previous-period windows (7/30/90/365 days before the current window).
/// </summary>
public partial class SeedService
{
    private const string DashboardSessionTitlePrefix = "[DASHR] Cohort session";
    private const string DashboardCompletedClassCode = "CLS-DASH-COMPLETED";
    private const string DashboardCancelledClassCode = "CLS-DASH-CANCELLED";
    private const string DashboardPaymentCodePrefix = "INV-DASHR-";
    private const string DashboardSubmissionCodePrefix = "SUB-DASHR-";
    private const string DashboardRichMarkerCode = "INV-DASHR-0001";

    private async Task SeedDashboardSupportDataAsync()
    {
        _loggerService.LogInformation("Starting rich range-aware dashboard support seed");

        await SeedDashboardEnrollmentDiversityAsync();
        await SeedDashboardModuleEnrollmentStatusesAsync();
        await SeedDashboardRevenueTimelineAsync();
        await SeedDashboardAssessmentTimelineAsync();
        await SeedDashboardOperationsClassesAsync();
        await SeedClassMentorRequestsForDashboardAsync();
        await SeedDashboardSessionsAndAttendanceAsync();
        await SeedDashboardClassEnrollmentStatusesAsync();

        _loggerService.LogInformation("Finished rich range-aware dashboard support seed");
    }

    /// <summary>
    /// Day plan covering every dashboard preset + its previous adjacent window.
    /// Value = suggested event count that day (higher on weekdays / recent days).
    /// </summary>
    private static List<(int DaysAgo, int Events)> BuildRichDashboardDayPlan(DateTime utcNow)
    {
        var plan = new Dictionary<int, int>();

        void Upsert(int daysAgo, int events)
        {
            if (daysAgo < 0)
            {
                return;
            }

            plan[daysAgo] = plan.TryGetValue(daysAgo, out var existing)
                ? Math.Max(existing, events)
                : events;
        }

        // Last7Days current (0–6) + previous (7–13): every day, several events.
        for (var d = 0; d <= 13; d++)
        {
            Upsert(d, IsWeekend(utcNow, d) ? 2 : 4);
        }

        // Last30Days current (0–29) + previous (30–59): every day.
        for (var d = 14; d <= 59; d++)
        {
            Upsert(d, IsWeekend(utcNow, d) ? 1 : 3);
        }

        // Last90Days current (0–89) + previous (90–179): every other day.
        for (var d = 60; d <= 179; d += 2)
        {
            Upsert(d, IsWeekend(utcNow, d) ? 1 : 2);
        }

        // Last12Months current (0–364): weekly anchors with a few events.
        for (var d = 180; d <= 364; d += 7)
        {
            Upsert(d, 2);
        }

        // Previous 12 months for Last12Months comparison (365–729): biweekly.
        for (var d = 365; d <= 729; d += 14)
        {
            Upsert(d, 1);
        }

        // Extra mid-month spikes so monthly charts are not flat.
        for (var month = 0; month < 24; month++)
        {
            Upsert(month * 30 + 5, 3);
            Upsert(month * 30 + 18, 2);
        }

        return plan
            .OrderBy(kv => kv.Key)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    private static bool IsWeekend(DateTime utcNow, int daysAgo)
    {
        var day = utcNow.AddDays(-daysAgo).DayOfWeek;
        return day is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    /// <summary>
    /// Fill student×program matrix + redistribute EnrolledAt across every preset window
    /// so enrollment trends / previous-period KPIs are non-flat for 7d/30d/90d/12mo.
    /// </summary>
    private async Task SeedDashboardEnrollmentDiversityAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard enrollment diversity (rich)");

        var programs = (await _unitOfWork.Programs.GetAllAsync(p => !p.IsDeleted))
            .Where(p => !GetDemoProgramCodeSet().Contains(p.Code))
            .OrderBy(p => p.Code)
            .ToList();
        var students = (await _unitOfWork.Users.GetAllAsync(
                u => !u.IsDeleted && u.Role == RoleType.Student))
            .OrderBy(u => u.Code)
            .ToList();

        if (programs.Count == 0 || students.Count < 8)
        {
            _loggerService.LogWarning("Not enough programs/students for enrollment diversity seed.");
            return;
        }

        var now = DateTime.UtcNow;
        var dayPlan = BuildRichDashboardDayPlan(now);
        var toAdd = new List<ProgramEnrollment>();

        // Ensure every EnrollmentStatus appears at least once on distinct pairs.
        var statusTargets = new (EnrollmentStatus Status, int StudentIndex, int ProgramIndex, int DaysAgo)[]
        {
            (EnrollmentStatus.Failed, 7, 0, 40),
            (EnrollmentStatus.PendingPayment, 8, 1, 18),
            (EnrollmentStatus.Deferred, 9, 2, 55),
            (EnrollmentStatus.Completed, 10, 0, 220),
            (EnrollmentStatus.Dropped, 11, 3 % programs.Count, 150),
            (EnrollmentStatus.Active, 12, 1, 5),
        };

        foreach (var target in statusTargets)
        {
            var student = students[target.StudentIndex % students.Count];
            var program = programs[target.ProgramIndex % programs.Count];
            if (await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted) != null)
            {
                continue;
            }

            if (toAdd.Any(pe => pe.StudentId == student.Id && pe.ProgramId == program.Id))
            {
                continue;
            }

            var enrolledAt = now.AddDays(-target.DaysAgo);
            toAdd.Add(CreateProgramEnrollment(
                student.Id,
                program.Id,
                target.Status,
                enrolledAt,
                target.Status == EnrollmentStatus.Completed ? 100m : 25m));
        }

        // Fill as many unique student×program pairs as possible, dated from the rich day plan.
        var dayCursor = 0;
        foreach (var student in students)
        {
            foreach (var program in programs)
            {
                if (await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                        pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted) != null)
                {
                    continue;
                }

                if (toAdd.Any(pe => pe.StudentId == student.Id && pe.ProgramId == program.Id))
                {
                    continue;
                }

                var (daysAgo, _) = dayPlan[dayCursor % dayPlan.Count];
                dayCursor++;
                var enrolledAt = now.AddDays(-daysAgo).AddHours(9 + (dayCursor % 8));
                var status = (dayCursor % 17) switch
                {
                    0 => EnrollmentStatus.Completed,
                    1 => EnrollmentStatus.PendingPayment,
                    2 => EnrollmentStatus.Deferred,
                    3 => EnrollmentStatus.Failed,
                    4 => EnrollmentStatus.Dropped,
                    _ => EnrollmentStatus.Active
                };

                toAdd.Add(CreateProgramEnrollment(
                    student.Id,
                    program.Id,
                    status,
                    enrolledAt,
                    status == EnrollmentStatus.Completed ? 100m : 15m + (dayCursor % 70)));
            }
        }

        if (toAdd.Count > 0)
        {
            await _unitOfWork.ProgramEnrollments.AddRangeAsync(toAdd);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Added {Count} dashboard program enrollment(s).",
                toAdd.Count);
        }

        // Redistribute EnrolledAt on ALL existing PEs so charts are dense even when matrix was already full.
        var allEnrollments = (await _unitOfWork.ProgramEnrollments.GetAllAsync(pe => !pe.IsDeleted))
            .OrderBy(pe => pe.CreatedAt)
            .ThenBy(pe => pe.Id)
            .ToList();

        var updated = 0;
        for (var i = 0; i < allEnrollments.Count; i++)
        {
            var pe = allEnrollments[i];
            var (daysAgo, _) = dayPlan[i % dayPlan.Count];
            // Spread multiple enrollments that land on the same plan day across hours.
            var enrolledAt = now.AddDays(-daysAgo).AddHours(8 + (i % 10)).AddMinutes((i * 7) % 60);

            if (pe.EnrolledAt.HasValue
                && Math.Abs((pe.EnrolledAt.Value - enrolledAt).TotalHours) < 12)
            {
                continue;
            }

            pe.EnrolledAt = enrolledAt;
            pe.StartedAt = pe.Status is EnrollmentStatus.PendingPayment
                ? null
                : enrolledAt.AddDays(1);
            if (pe.Status == EnrollmentStatus.Completed)
            {
                pe.CompletedAt = enrolledAt.AddDays(60 + (i % 40));
                pe.ProgressPercent = 100m;
            }

            await _unitOfWork.ProgramEnrollments.Update(pe);
            updated++;
        }

        if (updated > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed dashboard enrollment diversity — added {Added}, redistributed {Updated}.",
            toAdd.Count,
            updated);
    }

    private static ProgramEnrollment CreateProgramEnrollment(
        Guid studentId,
        Guid programId,
        EnrollmentStatus status,
        DateTime enrolledAt,
        decimal progressPercent)
        => new()
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ProgramId = programId,
            Status = status,
            ProgressPercent = progressPercent,
            EnrolledAt = enrolledAt,
            StartedAt = status is EnrollmentStatus.PendingPayment ? null : enrolledAt.AddDays(1),
            CompletedAt = status == EnrollmentStatus.Completed ? enrolledAt.AddDays(75) : null,
            CreatedAt = enrolledAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

    /// <summary>Ensure ModuleEnrollment status breakdown covers every EnrollmentStatus.</summary>
    private async Task SeedDashboardModuleEnrollmentStatusesAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard module enrollment statuses");

        var demoProgramIds = await GetDemoProgramIdsAsync();
        var modules = (await _unitOfWork.Modules.GetAllAsync(
                m => !m.IsDeleted && !demoProgramIds.Contains(m.ProgramId)))
            .OrderBy(m => m.Code)
            .Take(8)
            .ToList();
        var students = (await _unitOfWork.Users.GetAllAsync(
                u => !u.IsDeleted && u.Role == RoleType.Student))
            .OrderBy(u => u.Code)
            .ToList();

        if (modules.Count == 0 || students.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var dayPlan = BuildRichDashboardDayPlan(now);
        var statuses = Enum.GetValues<EnrollmentStatus>();
        var toAdd = new List<ModuleEnrollment>();
        var cursor = 0;

        // One of each status, then fill more dated module enrollments for trends.
        for (var i = 0; i < statuses.Length; i++)
        {
            TryQueueModuleEnrollment(
                toAdd,
                students[i % students.Count],
                modules[i % modules.Count],
                statuses[i],
                now.AddDays(-(10 + i * 7)));
        }

        foreach (var (daysAgo, events) in dayPlan.Where(p => p.DaysAgo <= 365).Take(80))
        {
            for (var e = 0; e < Math.Min(events, 2); e++)
            {
                var student = students[cursor % students.Count];
                var module = modules[cursor % modules.Count];
                cursor++;
                TryQueueModuleEnrollment(
                    toAdd,
                    student,
                    module,
                    EnrollmentStatus.Active,
                    now.AddDays(-daysAgo).AddHours(10 + e));
            }
        }

        // Persist only rows that do not collide with DB.
        var moduleProgramById = modules.ToDictionary(m => m.Id, m => m.ProgramId);
        var persisted = new List<ModuleEnrollment>();
        foreach (var me in toAdd)
        {
            var studentId = me.StudentId;
            var moduleId = me.ModuleId;

            if (await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    x => x.StudentId == studentId
                         && x.ModuleId == moduleId
                         && !x.IsDeleted) != null)
            {
                continue;
            }

            if (!moduleProgramById.TryGetValue(moduleId, out var programId))
            {
                continue;
            }

            var pe = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                e => e.StudentId == studentId
                     && e.ProgramId == programId
                     && !e.IsDeleted);
            me.ProgramEnrollmentId = pe?.Id;
            persisted.Add(me);
        }

        if (persisted.Count == 0)
        {
            _loggerService.LogInformation("Module enrollment status diversity already present, skipping inserts");
            return;
        }

        await _unitOfWork.ModuleEnrollments.AddRangeAsync(persisted);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed dashboard module enrollment statuses — {Count} row(s).",
            persisted.Count);
    }

    private static void TryQueueModuleEnrollment(
        List<ModuleEnrollment> toAdd,
        User student,
        Module module,
        EnrollmentStatus status,
        DateTime enrolledAt)
    {
        if (toAdd.Any(me => me.StudentId == student.Id && me.ModuleId == module.Id))
        {
            return;
        }

        toAdd.Add(new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ModuleId = module.Id,
            Status = status,
            ProgressPercent = status == EnrollmentStatus.Completed ? 100m : 20m,
            AttemptNumber = 1,
            EnrolledAt = enrolledAt,
            StartedAt = enrolledAt.AddDays(1),
            CompletedAt = status == EnrollmentStatus.Completed ? enrolledAt.AddDays(20) : null,
            CreatedAt = enrolledAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        });
    }

    /// <summary>
    /// Multi-event/day payment timeline covering every preset + previous window,
    /// all gateways/statuses, plus module-retake rows for moduleId filters.
    /// </summary>
    private async Task SeedDashboardRevenueTimelineAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard revenue timeline (rich)");

        if (await _unitOfWork.Payments.FirstOrDefaultAsync(
                p => p.Code == DashboardRichMarkerCode && !p.IsDeleted) != null)
        {
            _loggerService.LogInformation("Rich dashboard revenue timeline already seeded, skipping");
            return;
        }

        var enrollments = (await _unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => !pe.IsDeleted,
                pe => pe.Student,
                pe => pe.Program))
            .Where(pe => pe.Student != null)
            .ToList();

        if (enrollments.Count == 0)
        {
            _loggerService.LogWarning("No program enrollments for revenue timeline seed.");
            return;
        }

        var now = DateTime.UtcNow;
        var dayPlan = BuildRichDashboardDayPlan(now);
        var gateways = new[] { PaymentGateway.Stripe, PaymentGateway.VnPay, PaymentGateway.BankTransfer };
        var payments = new List<Payment>();
        var invoices = new List<Invoice>();
        var index = 1;

        foreach (var (daysAgo, events) in dayPlan)
        {
            for (var e = 0; e < events; e++)
            {
                var enrollment = enrollments[(index + e) % enrollments.Count];
                var gateway = gateways[index % gateways.Length];
                // ~70% success so revenue trends look healthy but not fake-perfect.
                var status = (index % 10) switch
                {
                    0 => PaymentStatus.Failed,
                    1 => PaymentStatus.Refunded,
                    2 => PaymentStatus.Pending,
                    3 => PaymentStatus.Cancelled,
                    _ => PaymentStatus.Success
                };

                var amount = 350_000m + (index % 12) * 85_000m + (e * 15_000m);
                var paidAt = status is PaymentStatus.Success or PaymentStatus.Refunded
                    ? now.AddDays(-daysAgo).AddHours(9 + e).AddMinutes((index * 3) % 50)
                    : (DateTime?)null;

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    Code = $"{DashboardPaymentCodePrefix}{index:D4}",
                    StudentId = enrollment.StudentId,
                    PaidById = enrollment.StudentId,
                    ProgramEnrollmentId = enrollment.Id,
                    ModuleEnrollmentId = null,
                    Amount = amount,
                    Currency = "VND",
                    Gateway = gateway,
                    TransactionId = status == PaymentStatus.Success
                        ? $"DASHR-TXN-{index:D4}"
                        : null,
                    Status = status,
                    PaidAt = paidAt,
                    CheckoutSessionId = status == PaymentStatus.Pending ? $"dashr_sess_{index}" : null,
                    CreatedAt = now.AddDays(-daysAgo).AddHours(8 + e),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };
                payments.Add(payment);

                if (status == PaymentStatus.Success)
                {
                    invoices.Add(new Invoice
                    {
                        Id = Guid.NewGuid(),
                        InvoiceNumber = $"INV-DASHR-DOC-{index:D4}",
                        PaymentId = payment.Id,
                        IssuedToId = enrollment.StudentId,
                        BillingName = enrollment.Student?.FullName ?? "Dashboard Seed Student",
                        BillingEmail = enrollment.Student?.Email ?? "dash-seed@oboxsteam.com",
                        ItemDescription = $"Dashboard rich seed — {enrollment.Program?.Name ?? "Program"}",
                        SubTotal = amount,
                        TotalAmount = amount,
                        Currency = "VND",
                        CreatedAt = paidAt ?? now,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                index++;
            }
        }

        // Module retake fees spread across last 90 days for moduleId filter demos.
        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => !me.IsDeleted,
                me => me.Student,
                me => me.Module))
            .Take(20)
            .ToList();

        for (var i = 0; i < moduleEnrollments.Count; i++)
        {
            var me = moduleEnrollments[i];
            var daysAgo = 3 + i * 4;
            payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                Code = $"{DashboardPaymentCodePrefix}M{index:D4}",
                StudentId = me.StudentId,
                PaidById = me.StudentId,
                ProgramEnrollmentId = me.ProgramEnrollmentId,
                ModuleEnrollmentId = me.Id,
                Amount = me.Module?.RetakeFee > 0 ? me.Module.RetakeFee : 250_000m,
                Currency = "VND",
                Gateway = PaymentGateway.VnPay,
                TransactionId = $"DASHR-RETAKE-{index:D4}",
                Status = PaymentStatus.Success,
                PaidAt = now.AddDays(-daysAgo).AddHours(11),
                CreatedAt = now.AddDays(-daysAgo),
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            index++;
        }

        await _unitOfWork.Payments.AddRangeAsync(payments);
        await _unitOfWork.SaveChangesAsync();

        if (invoices.Count > 0)
        {
            await _unitOfWork.Invoices.AddRangeAsync(invoices);
            await _unitOfWork.SaveChangesAsync();
        }

        await SeedDashboardPendingPaymentRequestsAsync(now);

        _loggerService.LogInformation(
            "Finished rich dashboard revenue timeline — {PaymentCount} payment(s), {InvoiceCount} invoice(s).",
            payments.Count,
            invoices.Count);
    }

    private async Task SeedDashboardPendingPaymentRequestsAsync(DateTime now)
    {
        var specs = new (string Token, string StudentCode, string ProgramCode, decimal Amount, int ExpireDays)[]
        {
            ("SEED-DASHR-PAYREQ-001", "STD-002", "PRG-WEBDEV", 980_000m, 5),
            ("SEED-DASHR-PAYREQ-002", "STD-003", "PRG-ROBOTICS", 1_150_000m, 3),
            ("SEED-DASHR-PAYREQ-003", "STD-004", "PRG-STEAM-01", 750_000m, 7),
        };

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Role == RoleType.Parent && !u.IsDeleted);
        if (parent == null)
        {
            return;
        }

        var added = 0;
        foreach (var spec in specs)
        {
            if (await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                    pr => pr.Token == spec.Token && !pr.IsDeleted) != null)
            {
                continue;
            }

            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == spec.StudentCode);
            var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == spec.ProgramCode);
            if (student == null || program == null)
            {
                continue;
            }

            var pe = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                e => e.StudentId == student.Id && e.ProgramId == program.Id && !e.IsDeleted);

            await _unitOfWork.PaymentRequests.AddAsync(new PaymentRequest
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ParentId = parent.Id,
                ProgramId = program.Id,
                ProgramEnrollmentId = pe?.Id,
                Amount = spec.Amount,
                Currency = "VND",
                Token = spec.Token,
                ExpiresAt = now.AddDays(spec.ExpireDays),
                Status = PaymentRequestStatus.Pending,
                CreatedAt = now.AddDays(-1),
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            added++;
        }

        if (added > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Dense submissions across every preset window (backlog, pass/fail, revision, trends).
    /// </summary>
    private async Task SeedDashboardAssessmentTimelineAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard assessment timeline (rich)");

        if (await _unitOfWork.Submissions.FirstOrDefaultAsync(
                s => s.Code == $"{DashboardSubmissionCodePrefix}0001" && !s.IsDeleted) != null)
        {
            _loggerService.LogInformation("Rich dashboard assessment timeline already seeded, skipping");
            return;
        }

        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var students = (await _unitOfWork.Users.GetAllAsync(
                u => !u.IsDeleted && u.Role == RoleType.Student))
            .OrderBy(u => u.Code)
            .ToList();

        // Keep dashboard volume off research milestone deliverables so SUB-RML* UI seeds
        // are not blocked / mixed with SUB-DASHR* rows that have no FileUrl / milestone.
        // Also keep demo showcase assignments submission-free for live mentor grading demos.
        var researchAssignmentIds = (await _unitOfWork.ResearchMilestones.GetAllAsync(rm => !rm.IsDeleted))
            .Select(rm => rm.AssignmentId)
            .ToHashSet();
        var demoProgramIds = await GetDemoProgramIdsAsync();
        var demoModuleIds = demoProgramIds.Count == 0
            ? new HashSet<Guid>()
            : (await _unitOfWork.Modules.GetAllAsync(
                    m => demoProgramIds.Contains(m.ProgramId) && !m.IsDeleted))
                .Select(m => m.Id)
                .ToHashSet();
        var assignments = (await _unitOfWork.Assignments.GetAllAsync(
                a => !a.IsDeleted
                     && !researchAssignmentIds.Contains(a.Id)
                     && !demoModuleIds.Contains(a.ModuleId)))
            .OrderBy(a => a.Code)
            .Take(8)
            .ToList();

        if (mentor == null || students.Count == 0 || assignments.Count == 0)
        {
            _loggerService.LogWarning("Missing mentor/students/assignments for assessment timeline.");
            return;
        }

        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(me => !me.IsDeleted))
            .ToList();
        var now = DateTime.UtcNow;
        var dayPlan = BuildRichDashboardDayPlan(now);
        var created = 0;
        var index = 1;

        async Task AddIfMissingAsync(Submission submission)
        {
            if (await SubmissionCodeExistsAsync(submission.Code))
            {
                return;
            }

            await _unitOfWork.Submissions.AddAsync(submission);
            created++;
        }

        // Anchor cases for backlog / pass / fail / revision (always present).
        var a0 = assignments[0];
        var s1 = students[0];
        var s2 = students.Count > 1 ? students[1] : students[0];
        var me1 = moduleEnrollments.FirstOrDefault(me => me.StudentId == s1.Id)
                  ?? moduleEnrollments.FirstOrDefault();

        await AddIfMissingAsync(new Submission
        {
            Id = Guid.NewGuid(),
            Code = $"{DashboardSubmissionCodePrefix}BACKLOG-01",
            AssignmentId = a0.Id,
            StudentId = s1.Id,
            ModuleEnrollmentId = me1?.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.TurnedIn,
            ContentText = "Rich dashboard seed — backlog TurnedIn > 48h.",
            SubmittedAt = now.AddDays(-5),
            CreatedAt = now.AddDays(-6),
            CreatedBy = s1.Id,
            IsDeleted = false
        });

        await AddIfMissingAsync(new Submission
        {
            Id = Guid.NewGuid(),
            Code = $"{DashboardSubmissionCodePrefix}BACKLOG-02",
            AssignmentId = a0.Id,
            StudentId = s2.Id,
            ModuleEnrollmentId = moduleEnrollments.FirstOrDefault(me => me.StudentId == s2.Id)?.Id ?? me1?.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            ContentText = "Rich dashboard seed — backlog Pending > 48h.",
            SubmittedAt = now.AddDays(-4),
            CreatedAt = now.AddDays(-4),
            CreatedBy = s2.Id,
            IsDeleted = false
        });

        foreach (var (daysAgo, events) in dayPlan)
        {
            for (var e = 0; e < events; e++)
            {
                var student = students[(index + e) % students.Count];
                var assignment = assignments[index % assignments.Count];
                var me = moduleEnrollments.FirstOrDefault(m => m.StudentId == student.Id)
                         ?? moduleEnrollments.FirstOrDefault();
                var code = $"{DashboardSubmissionCodePrefix}{index:D4}";

                var status = (index % 8) switch
                {
                    0 => SubmissionStatus.Pending,
                    1 => SubmissionStatus.TurnedIn,
                    2 => SubmissionStatus.ReturnedForRevision,
                    _ => SubmissionStatus.Graded
                };

                // Keep some recent TurnedIn/Pending older than 48h for backlog KPI.
                if (daysAgo is >= 3 and <= 10 && index % 5 == 0)
                {
                    status = index % 2 == 0 ? SubmissionStatus.TurnedIn : SubmissionStatus.Pending;
                }

                var submittedAt = now.AddDays(-daysAgo).AddHours(10 + e).AddMinutes((index * 5) % 55);
                decimal? grade = null;
                DateTime? gradedAt = null;
                if (status is SubmissionStatus.Graded or SubmissionStatus.ReturnedForRevision)
                {
                    var pass = index % 4 != 0;
                    grade = pass
                        ? assignment.PassScore + 8 + (index % 12)
                        : Math.Max(0, assignment.PassScore - 8 - (index % 6));
                    gradedAt = submittedAt.AddHours(6 + (index % 18));
                }

                await AddIfMissingAsync(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    AssignmentId = assignment.Id,
                    StudentId = student.Id,
                    ModuleEnrollmentId = me?.Id,
                    AttemptNumber = 1 + (index % 2),
                    Status = status,
                    ContentText = $"Rich dashboard timeline seed day-{daysAgo} event-{e}.",
                    AssignedGrade = grade,
                    MentorFeedback = status == SubmissionStatus.Graded ? "Auto rich-seed grade." : null,
                    VerifiedBy = gradedAt.HasValue ? mentor.Id : null,
                    SubmittedAt = submittedAt,
                    GradedAt = gradedAt,
                    CreatedAt = submittedAt.AddHours(-1),
                    CreatedBy = student.Id,
                    IsDeleted = false
                });

                index++;
            }
        }

        if (created > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished rich dashboard assessment timeline — {Count} submission(s).",
            created);
    }

    /// <summary>Completed / Cancelled / sparse Open classes for operations filters.</summary>
    private async Task SeedDashboardOperationsClassesAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard operations classes");

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS")
                      ?? await _unitOfWork.Programs.FirstOrDefaultAsync(p => !p.IsDeleted);
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-002");
        if (program == null || mentor == null)
        {
            _loggerService.LogWarning("Missing program/mentor for dashboard operations classes.");
            return;
        }

        var now = DateTime.UtcNow;
        var definitions = new (string Code, string Name, ClassStatus Status, int Cap, int StartOffset, int EndOffset)[]
        {
            (DashboardCompletedClassCode, "Dashboard Completed Cohort", ClassStatus.Completed, 20, -120, -30),
            (DashboardCancelledClassCode, "Dashboard Cancelled Cohort", ClassStatus.Cancelled, 15, -60, 30),
            ("CLS-DASH-INPROGRESS", "Dashboard InProgress Sparse", ClassStatus.InProgress, 30, -20, 80),
            ("CLS-DASH-OPEN-EMPTY", "Dashboard Open Low Fill", ClassStatus.Open, 40, 10, 100),
        };

        var added = 0;
        foreach (var def in definitions)
        {
            if (await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == def.Code && !c.IsDeleted) != null)
            {
                continue;
            }

            await _unitOfWork.Classes.AddAsync(new Class
            {
                Id = Guid.NewGuid(),
                Code = def.Code,
                Name = def.Name,
                ProgramId = program.Id,
                MentorId = def.Status == ClassStatus.Cancelled ? null : mentor.Id,
                StartDate = now.AddDays(def.StartOffset),
                EndDate = now.AddDays(def.EndOffset),
                MaxCapacity = def.Cap,
                Status = def.Status,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = "Dashboard seed schedule",
                CreatedAt = now.AddDays(def.StartOffset),
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            added++;
        }

        if (added > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation("Finished seed dashboard operations classes — {Count} class(es).", added);
    }

    private async Task SeedClassMentorRequestsForDashboardAsync()
    {
        _loggerService.LogInformation("Starting seed class mentor requests for dashboard");

        if (await _unitOfWork.ClassMentorRequests.FirstOrDefaultAsync(
                r => r.Message != null && r.Message.Contains("dashboard seed request") && !r.IsDeleted) != null)
        {
            _loggerService.LogInformation("Dashboard mentor requests already seeded, skipping");
            return;
        }

        var targetClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
                              c => c.Code == "CLS-ROBOTICS-2026D" && !c.IsDeleted)
                          ?? await _unitOfWork.Classes.FirstOrDefaultAsync(
                              c => !c.IsDeleted && c.Status == ClassStatus.Draft);
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-003");
        var mentor2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-004");
        var mentor3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-005");

        if (targetClass == null || mentor == null)
        {
            _loggerService.LogWarning("No suitable class/mentor for mentor-request seed.");
            return;
        }

        var now = DateTime.UtcNow;
        var requests = new List<ClassMentorRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                MentorId = mentor.Id,
                Status = ClassMentorRequestStatus.Pending,
                Message = "Available for this cohort — dashboard seed request.",
                CreatedAt = now.AddDays(-2),
                CreatedBy = mentor.Id,
                IsDeleted = false
            }
        };

        if (mentor2 != null)
        {
            requests.Add(new ClassMentorRequest
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                MentorId = mentor2.Id,
                Status = ClassMentorRequestStatus.Pending,
                Message = "Second pending dashboard seed request for utilization metrics.",
                CreatedAt = now.AddDays(-1),
                CreatedBy = mentor2.Id,
                IsDeleted = false
            });
        }

        if (mentor3 != null)
        {
            requests.Add(new ClassMentorRequest
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                MentorId = mentor3.Id,
                Status = ClassMentorRequestStatus.Withdrawn,
                Message = "Withdrawn dashboard seed request.",
                CreatedAt = now.AddDays(-4),
                CreatedBy = mentor3.Id,
                IsDeleted = false
            });
        }

        await _unitOfWork.ClassMentorRequests.AddRangeAsync(requests);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed class mentor requests — {Count} request(s).", requests.Count);
    }

    /// <summary>
    /// One attendance session per planned day (plus multi-student roster) so
    /// 7d/30d daily charts and 90d/12mo weekly-monthly charts stay populated.
    /// </summary>
    private async Task SeedDashboardSessionsAndAttendanceAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard sessions and attendance (rich)");

        if (await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                cs => cs.Title.StartsWith(DashboardSessionTitlePrefix) && !cs.IsDeleted) != null)
        {
            _loggerService.LogInformation("Rich dashboard sessions already seeded, skipping");
            return;
        }

        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
                              c => c.Code == "CLS-ROBOTICS-2026A" && !c.IsDeleted)
                          ?? await _unitOfWork.Classes.FirstOrDefaultAsync(
                              c => c.Status == ClassStatus.InProgress && !c.IsDeleted);
        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01")
                     ?? await _unitOfWork.Modules.FirstOrDefaultAsync(m => !m.IsDeleted);
        var activity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => !a.IsDeleted);

        if (classEntity == null || module == null)
        {
            _loggerService.LogWarning("Missing class/module for dashboard session seed.");
            return;
        }

        var students = (await _unitOfWork.Users.GetAllAsync(
                u => !u.IsDeleted && u.Role == RoleType.Student))
            .OrderBy(u => u.Code)
            .Take(10)
            .ToList();
        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(me => !me.IsDeleted))
            .ToList();

        if (students.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var dayPlan = BuildRichDashboardDayPlan(now);
        var attendanceStatuses = Enum.GetValues<AttendanceStatus>();
        var sessions = new List<ClassSession>();
        var attendances = new List<SessionAttendance>();
        var seq = 0;

        foreach (var (daysAgo, _) in dayPlan)
        {
            var start = now.AddDays(-daysAgo).Date.AddHours(9);
            var session = new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = classEntity.Id,
                ModuleId = module.Id,
                ActivityId = activity?.Id,
                SessionKind = SessionKind.Lesson,
                Title = $"{DashboardSessionTitlePrefix} #{seq + 1:D3}",
                Description = "Rich dashboard attendance seed session.",
                StartTime = start,
                EndTime = start.AddHours(2),
                Location = "Lab A",
                RequiresAttendance = true,
                RequiresMentorCheckIn = false,
                Status = ClassSessionStatus.Completed,
                CreatedAt = start,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };
            sessions.Add(session);

            var statusIndex = seq;
            // Fewer students on older/monthly sessions to vary attendance rate.
            var rosterSize = daysAgo <= 60 ? students.Count : Math.Max(4, students.Count - 3);
            foreach (var student in students.Take(rosterSize))
            {
                var me = moduleEnrollments.FirstOrDefault(m => m.StudentId == student.Id)
                         ?? moduleEnrollments.FirstOrDefault();
                if (me == null)
                {
                    continue;
                }

                var status = attendanceStatuses[statusIndex % attendanceStatuses.Length];
                // Bias recent sessions toward Present so current-window rate > previous-window.
                if (daysAgo <= 30 && statusIndex % 5 != 0)
                {
                    status = AttendanceStatus.Present;
                }

                statusIndex++;

                attendances.Add(new SessionAttendance
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = session.Id,
                    StudentId = student.Id,
                    ModuleEnrollmentId = me.Id,
                    Status = status,
                    CheckedInAt = status is AttendanceStatus.Present or AttendanceStatus.Late
                        ? start.AddMinutes(5)
                        : null,
                    CreatedAt = start,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            seq++;
        }

        await _unitOfWork.ClassSessions.AddRangeAsync(sessions);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.SessionAttendances.AddRangeAsync(attendances);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished rich dashboard sessions/attendance — {SessionCount} session(s), {AttendanceCount} attendance row(s).",
            sessions.Count,
            attendances.Count);
    }

    /// <summary>ClassEnrollment status diversity (Transferred / Withdrawn / Completed).</summary>
    private async Task SeedDashboardClassEnrollmentStatusesAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard class enrollment statuses");

        var openClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == "CLS-OPEN-005" && !c.IsDeleted);
        var completedClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == DashboardCompletedClassCode && !c.IsDeleted);
        if (openClass == null)
        {
            _loggerService.LogWarning("CLS-OPEN-005 not found for class enrollment status seed.");
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Id == openClass.ProgramId);
        if (program == null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var plans = new (string StudentCode, ClassEnrollmentStatus Status, Guid ClassId)[]
        {
            ("STD-021", ClassEnrollmentStatus.Withdrawn, openClass.Id),
            ("STD-022", ClassEnrollmentStatus.Transferred, openClass.Id),
            ("STD-023", ClassEnrollmentStatus.Completed, completedClass?.Id ?? openClass.Id),
        };

        var added = 0;
        foreach (var plan in plans)
        {
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == plan.StudentCode);
            if (student == null)
            {
                continue;
            }

            var existing = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.StudentId == student.Id
                      && ce.ClassId == plan.ClassId
                      && ce.Status == plan.Status
                      && !ce.IsDeleted);
            if (existing != null)
            {
                continue;
            }

            var pe = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                e => e.StudentId == student.Id && e.ProgramId == program.Id && !e.IsDeleted);
            if (pe == null)
            {
                pe = CreateProgramEnrollment(
                    student.Id,
                    program.Id,
                    EnrollmentStatus.Active,
                    now.AddDays(-40),
                    10m);
                await _unitOfWork.ProgramEnrollments.AddAsync(pe);
                await _unitOfWork.SaveChangesAsync();
            }

            var activeOnClass = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.StudentId == student.Id && ce.ClassId == plan.ClassId && !ce.IsDeleted);
            if (activeOnClass != null)
            {
                activeOnClass.Status = plan.Status;
                await _unitOfWork.ClassEnrollments.Update(activeOnClass);
                added++;
                continue;
            }

            await _unitOfWork.ClassEnrollments.AddAsync(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = plan.ClassId,
                StudentId = student.Id,
                ProgramEnrollmentId = pe.Id,
                Status = plan.Status,
                EnrolledAt = now.AddDays(-20),
                CreatedAt = now.AddDays(-20),
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            added++;
        }

        if (added > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed dashboard class enrollment statuses — {Count} update(s).",
            added);
    }
}
