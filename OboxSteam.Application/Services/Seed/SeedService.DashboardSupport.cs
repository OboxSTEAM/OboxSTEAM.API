using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Dense, idempotent seed data for Manager dashboard endpoints:
/// trends (daily/weekly/monthly), previous-period windows, all status buckets,
/// and entity-scoped filters (programId / moduleId / classId).
/// </summary>
public partial class SeedService
{
    private const string DashboardSessionTitlePrefix = "[DASH] Cohort session";
    private const string DashboardCompletedClassCode = "CLS-DASH-COMPLETED";
    private const string DashboardCancelledClassCode = "CLS-DASH-CANCELLED";
    private const string DashboardPaymentCodePrefix = "INV-DASH-";

    private async Task SeedDashboardSupportDataAsync()
    {
        _loggerService.LogInformation("Starting dense dashboard support seed");

        await SeedDashboardEnrollmentDiversityAsync();
        await SeedDashboardModuleEnrollmentStatusesAsync();
        await SeedDashboardRevenueTimelineAsync();
        await SeedDashboardAssessmentTimelineAsync();
        await SeedDashboardOperationsClassesAsync();
        await SeedClassMentorRequestsForDashboardAsync();
        await SeedDashboardSessionsAndAttendanceAsync();
        await SeedDashboardClassEnrollmentStatusesAsync();

        _loggerService.LogInformation("Finished dense dashboard support seed");
    }

    /// <summary>
    /// Extra program enrollments across statuses and a 12-month EnrolledAt timeline
    /// so enrollment trends / previous-period / status zero-fill charts are meaningful.
    /// </summary>
    private async Task SeedDashboardEnrollmentDiversityAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard enrollment diversity");

        var programs = (await _unitOfWork.Programs.GetAllAsync(p => !p.IsDeleted))
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

        var existingKeys = (await _unitOfWork.ProgramEnrollments.GetAllAsync(pe => !pe.IsDeleted))
            .Select(pe => (pe.StudentId, pe.ProgramId, pe.Status))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var toAdd = new List<ProgramEnrollment>();

        // Ensure every EnrollmentStatus appears at least once.
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
            if (target.StudentIndex >= students.Count)
            {
                continue;
            }

            var student = students[target.StudentIndex % students.Count];
            var program = programs[target.ProgramIndex % programs.Count];
            var key = (student.Id, program.Id, target.Status);
            if (existingKeys.Contains(key))
            {
                continue;
            }

            // Avoid unique conflicts when an Active enrollment already exists for the same pair.
            if (await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student.Id
                          && pe.ProgramId == program.Id
                          && !pe.IsDeleted) != null
                && target.Status == EnrollmentStatus.Active)
            {
                continue;
            }

            if (await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student.Id
                          && pe.ProgramId == program.Id
                          && pe.Status == target.Status
                          && !pe.IsDeleted) != null)
            {
                continue;
            }

            // For non-active statuses, still only one PE per student+program typically —
            // skip if any PE already exists for the pair.
            if (await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted) != null)
            {
                continue;
            }

            var enrolledAt = now.AddDays(-target.DaysAgo);
            toAdd.Add(new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = program.Id,
                Status = target.Status,
                ProgressPercent = target.Status == EnrollmentStatus.Completed ? 100m : 25m,
                EnrolledAt = enrolledAt,
                StartedAt = target.Status is EnrollmentStatus.PendingPayment
                    ? null
                    : enrolledAt.AddDays(2),
                CompletedAt = target.Status == EnrollmentStatus.Completed
                    ? enrolledAt.AddDays(90)
                    : null,
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            existingKeys.Add(key);
        }

        // Spread Active enrollments across ~360 days (monthly + weekly density for trends).
        var robotics = programs.FirstOrDefault(p => p.Code == "PRG-ROBOTICS") ?? programs[0];
        var webdev = programs.FirstOrDefault(p => p.Code == "PRG-WEBDEV") ?? programs[Math.Min(1, programs.Count - 1)];
        var dayOffsets = new[]
        {
            1, 3, 5, 8, 12, 16, 20, 25, 28,
            35, 42, 49, 56, 63, 70, 77, 85,
            100, 120, 140, 160, 180, 200, 230, 260, 290, 320, 350
        };

        var studentCursor = 0;
        foreach (var daysAgo in dayOffsets)
        {
            // Pick students that do not already have a robotics PE when possible.
            User? student = null;
            for (var attempt = 0; attempt < students.Count; attempt++)
            {
                var candidate = students[(studentCursor + attempt) % students.Count];
                studentCursor++;
                var program = daysAgo % 2 == 0 ? robotics : webdev;
                if (await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                        pe => pe.StudentId == candidate.Id
                              && pe.ProgramId == program.Id
                              && !pe.IsDeleted) != null)
                {
                    continue;
                }

                if (toAdd.Any(pe => pe.StudentId == candidate.Id && pe.ProgramId == program.Id))
                {
                    continue;
                }

                student = candidate;
                var enrolledAt = now.AddDays(-daysAgo);
                toAdd.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ProgramId = program.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = daysAgo > 200 ? 80m : 30m,
                    EnrolledAt = enrolledAt,
                    StartedAt = enrolledAt.AddDays(1),
                    CreatedAt = enrolledAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
                break;
            }
        }

        // Previous-period window density: cluster enrollments in [now-60d, now-30d)
        // so Last30Days previous-period KPIs are non-zero.
        for (var i = 31; i <= 58; i += 3)
        {
            var student = students[i % students.Count];
            var program = programs[i % programs.Count];
            if (await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted) != null)
            {
                continue;
            }

            if (toAdd.Any(pe => pe.StudentId == student.Id && pe.ProgramId == program.Id))
            {
                continue;
            }

            var enrolledAt = now.AddDays(-i);
            toAdd.Add(new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = program.Id,
                Status = i % 5 == 0 ? EnrollmentStatus.Completed : EnrollmentStatus.Active,
                ProgressPercent = i % 5 == 0 ? 100m : 40m,
                EnrolledAt = enrolledAt,
                StartedAt = enrolledAt.AddDays(1),
                CompletedAt = i % 5 == 0 ? enrolledAt.AddDays(20) : null,
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
        }

        if (toAdd.Count == 0)
        {
            _loggerService.LogInformation("Dashboard enrollment diversity already present, skipping");
            return;
        }

        await _unitOfWork.ProgramEnrollments.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed dashboard enrollment diversity — {Count} program enrollment(s).",
            toAdd.Count);
    }

    /// <summary>Ensure ModuleEnrollment status breakdown covers every EnrollmentStatus.</summary>
    private async Task SeedDashboardModuleEnrollmentStatusesAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard module enrollment statuses");

        var modules = (await _unitOfWork.Modules.GetAllAsync(m => !m.IsDeleted))
            .OrderBy(m => m.Code)
            .Take(6)
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
        var statuses = Enum.GetValues<EnrollmentStatus>();
        var toAdd = new List<ModuleEnrollment>();

        for (var i = 0; i < statuses.Length; i++)
        {
            var status = statuses[i];
            var student = students[i % students.Count];
            var module = modules[i % modules.Count];

            var exists = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student.Id
                      && me.ModuleId == module.Id
                      && me.Status == status
                      && !me.IsDeleted);
            if (exists != null)
            {
                continue;
            }

            // Skip if any active attempt already occupies the pair for Active.
            if (status == EnrollmentStatus.Active
                && await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student.Id
                          && me.ModuleId == module.Id
                          && !me.IsDeleted) != null)
            {
                continue;
            }

            if (await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student.Id && me.ModuleId == module.Id && !me.IsDeleted) != null)
            {
                continue;
            }

            var pe = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                e => e.StudentId == student.Id
                     && e.ProgramId == module.ProgramId
                     && !e.IsDeleted);

            toAdd.Add(new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ModuleId = module.Id,
                ProgramEnrollmentId = pe?.Id,
                Status = status,
                ProgressPercent = status == EnrollmentStatus.Completed ? 100m : 20m,
                AttemptNumber = 1,
                EnrolledAt = now.AddDays(-(10 + i * 7)),
                StartedAt = now.AddDays(-(8 + i * 7)),
                CompletedAt = status == EnrollmentStatus.Completed ? now.AddDays(-3) : null,
                CreatedAt = now.AddDays(-(10 + i * 7)),
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
        }

        if (toAdd.Count == 0)
        {
            _loggerService.LogInformation("Module enrollment status diversity already present, skipping");
            return;
        }

        await _unitOfWork.ModuleEnrollments.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed dashboard module enrollment statuses — {Count} row(s).",
            toAdd.Count);
    }

    /// <summary>
    /// Dense payment timeline (~1 year) across gateways and statuses for revenue trends,
    /// previous-period comparison, and program/module/class filters.
    /// </summary>
    private async Task SeedDashboardRevenueTimelineAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard revenue timeline");

        if (await _unitOfWork.Payments.FirstOrDefaultAsync(
                p => p.Code == $"{DashboardPaymentCodePrefix}001" && !p.IsDeleted) != null)
        {
            _loggerService.LogInformation("Dashboard revenue timeline already seeded, skipping");
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
        var gateways = new[] { PaymentGateway.Stripe, PaymentGateway.VnPay, PaymentGateway.BankTransfer };
        var payments = new List<Payment>();
        var invoices = new List<Invoice>();

        // Daily density for last 45 days + weekly for 90d + monthly for 12mo.
        var dayOffsets = new List<int>();
        for (var d = 0; d <= 44; d += 2)
        {
            dayOffsets.Add(d);
        }

        for (var d = 50; d <= 90; d += 7)
        {
            dayOffsets.Add(d);
        }

        for (var d = 100; d <= 360; d += 20)
        {
            dayOffsets.Add(d);
        }

        // Extra cluster in previous 30d window relative to Last30Days (days 31–59).
        for (var d = 31; d <= 59; d += 2)
        {
            if (!dayOffsets.Contains(d))
            {
                dayOffsets.Add(d);
            }
        }

        dayOffsets = dayOffsets.Distinct().OrderBy(x => x).ToList();
        var index = 1;

        foreach (var daysAgo in dayOffsets)
        {
            var enrollment = enrollments[index % enrollments.Count];
            var gateway = gateways[index % gateways.Length];
            var status = (index % 11) switch
            {
                0 => PaymentStatus.Failed,
                1 => PaymentStatus.Refunded,
                2 => PaymentStatus.Pending,
                3 => PaymentStatus.Cancelled,
                _ => PaymentStatus.Success
            };

            var amount = 400_000m + (index % 9) * 75_000m;
            var paidAt = status is PaymentStatus.Success or PaymentStatus.Refunded
                ? now.AddDays(-daysAgo).AddHours(10)
                : (DateTime?)null;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                Code = $"{DashboardPaymentCodePrefix}{index:D3}",
                StudentId = enrollment.StudentId,
                PaidById = enrollment.StudentId,
                ProgramEnrollmentId = enrollment.Id,
                ModuleEnrollmentId = null,
                Amount = amount,
                Currency = "VND",
                Gateway = gateway,
                TransactionId = status == PaymentStatus.Success
                    ? $"DASH-TXN-{index:D3}"
                    : null,
                Status = status,
                PaidAt = paidAt,
                CheckoutSessionId = status == PaymentStatus.Pending ? $"dash_sess_{index}" : null,
                CreatedAt = now.AddDays(-daysAgo),
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };
            payments.Add(payment);

            if (status == PaymentStatus.Success)
            {
                invoices.Add(new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = $"INV-DASH-DOC-{index:D3}",
                    PaymentId = payment.Id,
                    IssuedToId = enrollment.StudentId,
                    BillingName = enrollment.Student?.FullName ?? "Dashboard Seed Student",
                    BillingEmail = enrollment.Student?.Email ?? "dash-seed@oboxsteam.com",
                    ItemDescription = $"Dashboard seed — {enrollment.Program?.Name ?? "Program"}",
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

        // Module retake fee payments for moduleId filter coverage.
        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => !me.IsDeleted,
                me => me.Student,
                me => me.Module))
            .Take(6)
            .ToList();

        foreach (var me in moduleEnrollments)
        {
            var code = $"{DashboardPaymentCodePrefix}M{index:D3}";
            payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                Code = code,
                StudentId = me.StudentId,
                PaidById = me.StudentId,
                ProgramEnrollmentId = me.ProgramEnrollmentId,
                ModuleEnrollmentId = me.Id,
                Amount = me.Module?.RetakeFee > 0 ? me.Module.RetakeFee : 250_000m,
                Currency = "VND",
                Gateway = PaymentGateway.VnPay,
                TransactionId = $"DASH-RETAKE-{index:D3}",
                Status = PaymentStatus.Success,
                PaidAt = now.AddDays(-(index % 40)),
                CreatedAt = now.AddDays(-(index % 40)),
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

        // Extra pending payment requests for PendingPaymentRequestsAmount.
        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Role == RoleType.Parent && !u.IsDeleted);
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
        if (student != null && parent != null && program != null
            && await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.Token == "SEED-DASH-PAYREQ-001" && !pr.IsDeleted) == null)
        {
            var pe = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                e => e.StudentId == student.Id && e.ProgramId == program.Id && !e.IsDeleted);

            await _unitOfWork.PaymentRequests.AddAsync(new PaymentRequest
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ParentId = parent.Id,
                ProgramId = program.Id,
                ProgramEnrollmentId = pe?.Id,
                Amount = 980_000m,
                Currency = "VND",
                Token = "SEED-DASH-PAYREQ-001",
                ExpiresAt = now.AddDays(5),
                Status = PaymentRequestStatus.Pending,
                CreatedAt = now,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed dashboard revenue timeline — {PaymentCount} payment(s), {InvoiceCount} invoice(s).",
            payments.Count,
            invoices.Count);
    }

    /// <summary>
    /// Submission timeline across statuses with backlog (&gt;48h), pass/fail grades,
    /// and GradedAt in both current and previous windows.
    /// </summary>
    private async Task SeedDashboardAssessmentTimelineAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard assessment timeline");

        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var students = (await _unitOfWork.Users.GetAllAsync(
                u => !u.IsDeleted && u.Role == RoleType.Student))
            .OrderBy(u => u.Code)
            .Take(12)
            .ToList();
        var assignments = (await _unitOfWork.Assignments.GetAllAsync(a => !a.IsDeleted))
            .Take(4)
            .ToList();

        if (mentor == null || students.Count == 0 || assignments.Count == 0)
        {
            _loggerService.LogWarning("Missing mentor/students/assignments for assessment timeline.");
            return;
        }

        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(me => !me.IsDeleted))
            .ToList();
        var now = DateTime.UtcNow;
        var created = 0;

        async Task<bool> TryAddAsync(string code, Submission submission)
        {
            if (await SubmissionCodeExistsAsync(code))
            {
                return false;
            }

            submission.Code = code;
            await _unitOfWork.Submissions.AddAsync(submission);
            created++;
            return true;
        }

        // Backlog + pass/fail anchors (kept from earlier seed).
        var assignment0 = assignments[0];
        var s1 = students[0];
        var s2 = students.Count > 1 ? students[1] : students[0];
        var me1 = moduleEnrollments.FirstOrDefault(me => me.StudentId == s1.Id)
                  ?? moduleEnrollments.FirstOrDefault();

        await TryAddAsync("SUB-DASH-BACKLOG-01", new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment0.Id,
            StudentId = s1.Id,
            ModuleEnrollmentId = me1?.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.TurnedIn,
            ContentText = "Dashboard seed — backlog TurnedIn > 48h.",
            SubmittedAt = now.AddDays(-5),
            CreatedAt = now.AddDays(-6),
            CreatedBy = s1.Id,
            IsDeleted = false
        });

        await TryAddAsync("SUB-DASH-BACKLOG-02", new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment0.Id,
            StudentId = s2.Id,
            ModuleEnrollmentId = moduleEnrollments.FirstOrDefault(me => me.StudentId == s2.Id)?.Id ?? me1?.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            ContentText = "Dashboard seed — backlog Pending > 48h.",
            SubmittedAt = now.AddDays(-4),
            CreatedAt = now.AddDays(-4),
            CreatedBy = s2.Id,
            IsDeleted = false
        });

        await TryAddAsync("SUB-DASH-FAIL-01", new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment0.Id,
            StudentId = s2.Id,
            ModuleEnrollmentId = moduleEnrollments.FirstOrDefault(me => me.StudentId == s2.Id)?.Id ?? me1?.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            ContentText = "Dashboard seed — graded below PassScore.",
            AssignedGrade = Math.Max(0, assignment0.PassScore - 15),
            MentorFeedback = "Needs revision.",
            VerifiedBy = mentor.Id,
            SubmittedAt = now.AddDays(-10),
            GradedAt = now.AddDays(-8),
            CreatedAt = now.AddDays(-11),
            CreatedBy = s2.Id,
            IsDeleted = false
        });

        await TryAddAsync("SUB-DASH-PASS-01", new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment0.Id,
            StudentId = s1.Id,
            ModuleEnrollmentId = me1?.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            ContentText = "Dashboard seed — graded above PassScore.",
            AssignedGrade = assignment0.PassScore + 20,
            MentorFeedback = "Solid work.",
            VerifiedBy = mentor.Id,
            SubmittedAt = now.AddDays(-12),
            GradedAt = now.AddDays(-11),
            CreatedAt = now.AddDays(-13),
            CreatedBy = s1.Id,
            IsDeleted = false
        });

        await TryAddAsync("SUB-DASH-REVISION-01", new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment0.Id,
            StudentId = s1.Id,
            ModuleEnrollmentId = me1?.Id,
            AttemptNumber = 2,
            Status = SubmissionStatus.ReturnedForRevision,
            ContentText = "Dashboard seed — returned for revision.",
            AssignedGrade = assignment0.PassScore - 5,
            MentorFeedback = "Please revise section 2.",
            VerifiedBy = mentor.Id,
            SubmittedAt = now.AddDays(-6),
            GradedAt = now.AddDays(-5),
            CreatedAt = now.AddDays(-7),
            CreatedBy = s1.Id,
            IsDeleted = false
        });

        // Timeline for trends + previous-period PassRate / SubmissionsInPreviousRange.
        var dayOffsets = new List<int>();
        for (var d = 0; d <= 44; d += 3)
        {
            dayOffsets.Add(d);
        }

        for (var d = 48; d <= 90; d += 7)
        {
            dayOffsets.Add(d);
        }

        for (var d = 100; d <= 340; d += 25)
        {
            dayOffsets.Add(d);
        }

        for (var d = 32; d <= 58; d += 3)
        {
            if (!dayOffsets.Contains(d))
            {
                dayOffsets.Add(d);
            }
        }

        var seq = 1;
        foreach (var daysAgo in dayOffsets.Distinct().OrderBy(x => x))
        {
            var student = students[seq % students.Count];
            var assignment = assignments[seq % assignments.Count];
            var me = moduleEnrollments.FirstOrDefault(m => m.StudentId == student.Id)
                     ?? moduleEnrollments.FirstOrDefault();
            var code = $"SUB-DASH-TL-{seq:D3}";

            var status = (seq % 7) switch
            {
                0 => SubmissionStatus.Pending,
                1 => SubmissionStatus.TurnedIn,
                2 => SubmissionStatus.ReturnedForRevision,
                _ => SubmissionStatus.Graded
            };

            var submittedAt = now.AddDays(-daysAgo).AddHours(14);
            decimal? grade = null;
            DateTime? gradedAt = null;
            if (status is SubmissionStatus.Graded or SubmissionStatus.ReturnedForRevision)
            {
                var pass = seq % 3 != 0;
                grade = pass ? assignment.PassScore + 10 + (seq % 5) : Math.Max(0, assignment.PassScore - 10);
                gradedAt = submittedAt.AddHours(20 + (seq % 10));
            }

            await TryAddAsync(code, new Submission
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                StudentId = student.Id,
                ModuleEnrollmentId = me?.Id,
                AttemptNumber = 1,
                Status = status,
                ContentText = $"Dashboard timeline seed day-{daysAgo}.",
                AssignedGrade = grade,
                MentorFeedback = status == SubmissionStatus.Graded ? "Auto seed grade." : null,
                VerifiedBy = gradedAt.HasValue ? mentor.Id : null,
                SubmittedAt = submittedAt,
                GradedAt = gradedAt,
                CreatedAt = submittedAt.AddHours(-1),
                CreatedBy = student.Id,
                IsDeleted = false
            });

            seq++;
        }

        if (created > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed dashboard assessment timeline — {Count} submission(s).",
            created);
    }

    /// <summary>Completed / Cancelled classes with varied capacity for operations filters.</summary>
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
    /// Dedicated class sessions spanning 12 months with mixed AttendanceStatus for trends.
    /// </summary>
    private async Task SeedDashboardSessionsAndAttendanceAsync()
    {
        _loggerService.LogInformation("Starting seed dashboard sessions and attendance");

        if (await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                cs => cs.Title.StartsWith(DashboardSessionTitlePrefix) && !cs.IsDeleted) != null)
        {
            _loggerService.LogInformation("Dashboard sessions already seeded, skipping");
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
            .Take(8)
            .ToList();
        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(me => !me.IsDeleted))
            .ToList();

        if (students.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var dayOffsets = new List<int>();
        for (var d = 1; d <= 40; d += 2)
        {
            dayOffsets.Add(d);
        }

        for (var d = 45; d <= 90; d += 7)
        {
            dayOffsets.Add(d);
        }

        for (var d = 100; d <= 340; d += 30)
        {
            dayOffsets.Add(d);
        }

        // Previous-period attendance cluster.
        for (var d = 32; d <= 58; d += 4)
        {
            if (!dayOffsets.Contains(d))
            {
                dayOffsets.Add(d);
            }
        }

        var attendanceStatuses = Enum.GetValues<AttendanceStatus>();
        var sessions = new List<ClassSession>();
        var attendances = new List<SessionAttendance>();
        var seq = 0;

        foreach (var daysAgo in dayOffsets.Distinct().OrderBy(x => x))
        {
            var start = now.AddDays(-daysAgo).Date.AddHours(9);
            var session = new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = classEntity.Id,
                ModuleId = module.Id,
                ActivityId = activity?.Id,
                SessionKind = SessionKind.Lesson,
                Title = $"{DashboardSessionTitlePrefix} #{seq + 1:D2}",
                Description = "Dense dashboard attendance seed session.",
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
            foreach (var student in students)
            {
                var me = moduleEnrollments.FirstOrDefault(m => m.StudentId == student.Id)
                         ?? moduleEnrollments.FirstOrDefault();
                if (me == null)
                {
                    continue;
                }

                var status = attendanceStatuses[statusIndex % attendanceStatuses.Length];
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
            "Finished seed dashboard sessions/attendance — {SessionCount} session(s), {AttendanceCount} attendance row(s).",
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
                pe = new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ProgramId = program.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 10m,
                    EnrolledAt = now.AddDays(-40),
                    CreatedAt = now.AddDays(-40),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };
                await _unitOfWork.ProgramEnrollments.AddAsync(pe);
                await _unitOfWork.SaveChangesAsync();
            }

            // If student already has Active enrollment on this class, update status instead of duplicating.
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
