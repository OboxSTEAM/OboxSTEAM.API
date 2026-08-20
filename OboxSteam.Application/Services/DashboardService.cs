using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.DashboardDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class DashboardService : IDashboardService
{
    public const int GradingBacklogThresholdHours = 48;

    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(DashboardFilterDto filter)
    {
        var landing = await GetLandingAsync(filter);
        return MapOverview(landing);
    }

    public async Task<DashboardLandingDto> GetLandingAsync(DashboardFilterDto filter)
    {
        var revenue = await GetRevenueOverviewAsync(filter);
        var enrollment = await GetEnrollmentOverviewAsync(filter);
        var assessment = await GetAssessmentOverviewAsync(filter);
        var operations = await GetOperationsOverviewAsync(filter);

        return new DashboardLandingDto
        {
            Revenue = revenue,
            Enrollment = enrollment,
            Assessment = assessment,
            Operations = operations
        };
    }

    public Task<RevenueOverviewDto> GetRevenueOverviewAsync(DashboardFilterDto filter)
    {
        var (from, to, granularity) = ResolveRange(filter);
        var (previousFrom, previousTo) = PreviousWindow(from, to);

        var payments = ApplyPaymentEntityScope(
            _unitOfWork.Payments.GetQueryable().Where(p => !p.IsDeleted),
            filter);

        var successPayments = payments.Where(p => p.Status == PaymentStatus.Success);
        var totalRevenue = successPayments.Sum(p => (decimal?)p.Amount) ?? 0m;

        var scopedForTrend = filter.PaymentStatus.HasValue
            ? payments.Where(p => p.Status == filter.PaymentStatus.Value)
            : successPayments;

        var successInRange = successPayments
            .Where(p => p.PaidAt != null && p.PaidAt >= from && p.PaidAt <= to);
        var revenueInRange = successInRange.Sum(p => (decimal?)p.Amount) ?? 0m;
        var successCountInRange = successInRange.Count();
        var averageOrderValue = successCountInRange == 0
            ? 0m
            : Math.Round(revenueInRange / successCountInRange, 2);

        var revenueInPreviousRange = successPayments
            .Where(p => p.PaidAt != null && p.PaidAt >= previousFrom && p.PaidAt < previousTo)
            .Sum(p => (decimal?)p.Amount) ?? 0m;

        var refundedAmount = payments
            .Where(p => p.Status == PaymentStatus.Refunded)
            .Sum(p => (decimal?)p.Amount) ?? 0m;

        var trendSource = scopedForTrend
            .Where(p => p.PaidAt != null && p.PaidAt >= from && p.PaidAt <= to);

        var pendingRequests = ApplyPaymentRequestEntityScope(
                _unitOfWork.PaymentRequests.GetQueryable()
                    .Where(pr => !pr.IsDeleted && pr.Status == PaymentRequestStatus.Pending),
                filter)
            .Select(pr => pr.Amount)
            .ToList();

        var invoiceQuery = _unitOfWork.Invoices
            .GetQueryable()
            .Where(i => !i.IsDeleted);

        if (filter.ProgramId.HasValue || filter.ModuleId.HasValue || filter.ClassId.HasValue)
        {
            var scopedPaymentIds = ApplyPaymentEntityScope(
                    _unitOfWork.Payments.GetQueryable().Where(p => !p.IsDeleted),
                    filter)
                .Select(p => p.Id);
            invoiceQuery = invoiceQuery.Where(i => scopedPaymentIds.Contains(i.PaymentId));
        }

        var invoiceCount = invoiceQuery.Count();

        var trendPoints = trendSource
            .Select(p => new { p.PaidAt, p.Amount })
            .ToList();

        var revenueTrend = BuildTrend(
            trendPoints.Select(x => (x.PaidAt!.Value, x.Amount)),
            from,
            to,
            granularity,
            DashboardTrendValueKind.Currency);

        var revenueByGateway = trendSource
            .GroupBy(p => p.Gateway)
            .Select(g => new RevenueByGatewayDto
            {
                Gateway = g.Key,
                Amount = g.Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var topSource = filter.PaymentStatus.HasValue
            ? payments.Where(p => p.Status == filter.PaymentStatus.Value)
            : successPayments;

        // Soft-deleted enrollments/programs are query-filtered out, so require live navigations
        // (FK alone leaves null joins that throw on non-nullable ProgramId materialization).
        var programRevenue = topSource
            .Where(p => p.ProgramEnrollment != null && p.ProgramEnrollment.Program != null)
            .Select(p => new
            {
                p.ProgramEnrollment!.ProgramId,
                ProgramName = p.ProgramEnrollment.Program!.Name,
                p.Amount
            })
            .GroupBy(x => new { x.ProgramId, x.ProgramName })
            .Select(g => new TopProgramRevenueDto
            {
                ProgramId = g.Key.ProgramId,
                ProgramName = g.Key.ProgramName,
                Amount = g.Sum(x => x.Amount)
            })
            .ToList();

        var topPrograms = Paginate(
            SortList(programRevenue, filter.SortBy, filter.IsDescending, "amount",
                (item, key) => key switch
                {
                    "name" => item.ProgramName,
                    "amount" => item.Amount,
                    _ => item.Amount
                }),
            filter.Page,
            filter.PageSize);

        return Task.FromResult(new RevenueOverviewDto
        {
            TotalRevenue = totalRevenue,
            RevenueInRange = revenueInRange,
            RevenueInPreviousRange = revenueInPreviousRange,
            AverageOrderValue = averageOrderValue,
            PendingPaymentRequestsCount = pendingRequests.Count,
            PendingPaymentRequestsAmount = pendingRequests.Sum(),
            RefundedAmount = refundedAmount,
            InvoiceCount = invoiceCount,
            RevenueTrend = revenueTrend,
            RevenueByGateway = revenueByGateway,
            TopProgramsByRevenue = topPrograms
        });
    }

    public Task<EnrollmentOverviewDto> GetEnrollmentOverviewAsync(DashboardFilterDto filter)
    {
        var (from, to, granularity) = ResolveRange(filter);
        var (previousFrom, previousTo) = PreviousWindow(from, to);

        var programsQuery = _unitOfWork.Programs.GetQueryable().Where(p => !p.IsDeleted);
        var modulesQuery = _unitOfWork.Modules.GetQueryable().Where(m => !m.IsDeleted);
        var coursesQuery = _unitOfWork.Courses.GetQueryable().Where(c => !c.IsDeleted);

        if (filter.ProgramId.HasValue)
        {
            programsQuery = programsQuery.Where(p => p.Id == filter.ProgramId.Value);
            modulesQuery = modulesQuery.Where(m => m.ProgramId == filter.ProgramId.Value);
            coursesQuery = coursesQuery.Where(c => c.Module.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ModuleId.HasValue)
        {
            modulesQuery = modulesQuery.Where(m => m.Id == filter.ModuleId.Value);
            coursesQuery = coursesQuery.Where(c => c.ModuleId == filter.ModuleId.Value);
        }

        var programEnrollments = _unitOfWork.ProgramEnrollments
            .GetQueryable()
            .Where(pe => !pe.IsDeleted);

        if (filter.ProgramId.HasValue)
        {
            programEnrollments = programEnrollments.Where(pe => pe.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ModuleId.HasValue)
        {
            var moduleId = filter.ModuleId.Value;
            programEnrollments = programEnrollments.Where(pe =>
                pe.ModuleEnrollments.Any(me => !me.IsDeleted && me.ModuleId == moduleId));
        }

        if (filter.ClassId.HasValue)
        {
            var classId = filter.ClassId.Value;
            programEnrollments = programEnrollments.Where(pe =>
                pe.ClassEnrollments.Any(ce => !ce.IsDeleted && ce.ClassId == classId));
        }

        if (filter.EnrollmentStatus.HasValue)
        {
            programEnrollments = programEnrollments.Where(pe => pe.Status == filter.EnrollmentStatus.Value);
        }

        var moduleEnrollments = _unitOfWork.ModuleEnrollments
            .GetQueryable()
            .Where(me => !me.IsDeleted);

        if (filter.ProgramId.HasValue)
        {
            var programId = filter.ProgramId.Value;
            moduleEnrollments = moduleEnrollments.Where(me => me.Module.ProgramId == programId);
        }

        if (filter.ModuleId.HasValue)
        {
            moduleEnrollments = moduleEnrollments.Where(me => me.ModuleId == filter.ModuleId.Value);
        }

        if (filter.ClassId.HasValue)
        {
            var classId = filter.ClassId.Value;
            var peIds = _unitOfWork.ClassEnrollments.GetQueryable()
                .Where(ce => !ce.IsDeleted && ce.ClassId == classId)
                .Select(ce => ce.ProgramEnrollmentId);
            moduleEnrollments = moduleEnrollments.Where(me =>
                peIds.Any(id => (Guid?)id == me.ProgramEnrollmentId));
        }

        if (filter.EnrollmentStatus.HasValue)
        {
            moduleEnrollments = moduleEnrollments.Where(me => me.Status == filter.EnrollmentStatus.Value);
        }

        var classEnrollments = _unitOfWork.ClassEnrollments
            .GetQueryable()
            .Where(ce => !ce.IsDeleted);

        if (filter.ProgramId.HasValue)
        {
            classEnrollments = classEnrollments.Where(ce => ce.Class.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ModuleId.HasValue)
        {
            var moduleId = filter.ModuleId.Value;
            classEnrollments = classEnrollments.Where(ce =>
                ce.ProgramEnrollment.ModuleEnrollments.Any(me => !me.IsDeleted && me.ModuleId == moduleId));
        }

        if (filter.ClassId.HasValue)
        {
            classEnrollments = classEnrollments.Where(ce => ce.ClassId == filter.ClassId.Value);
        }

        if (filter.ClassEnrollmentStatus.HasValue)
        {
            classEnrollments = classEnrollments.Where(ce => ce.Status == filter.ClassEnrollmentStatus.Value);
        }

        var totalProgramEnrollments = programEnrollments.Count();
        var completedCount = programEnrollments.Count(pe => pe.Status == EnrollmentStatus.Completed);
        var completionRate = totalProgramEnrollments == 0
            ? 0m
            : Math.Round((decimal)completedCount / totalProgramEnrollments * 100m, 2);

        var previousWindowEnrollments = programEnrollments
            .Where(pe => pe.EnrolledAt != null && pe.EnrolledAt >= previousFrom && pe.EnrolledAt < previousTo)
            .ToList();
        var previousTotal = previousWindowEnrollments.Count;
        var previousCompleted = previousWindowEnrollments.Count(pe => pe.Status == EnrollmentStatus.Completed);
        var completionRateInPreviousRange = previousTotal == 0
            ? 0m
            : Math.Round((decimal)previousCompleted / previousTotal * 100m, 2);

        var activeStudents = programEnrollments
            .Where(pe => pe.Status == EnrollmentStatus.Active)
            .Select(pe => pe.StudentId)
            .Distinct()
            .Count();

        var newEnrollmentsInRange = programEnrollments
            .Count(pe => pe.EnrolledAt != null && pe.EnrolledAt >= from && pe.EnrolledAt <= to);

        var newEnrollmentsInPreviousRange = previousTotal;

        var programStatusBreakdown = BuildStatusCounts(
            programEnrollments
                .GroupBy(pe => pe.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Status, x => x.Count));

        var moduleStatusBreakdown = BuildStatusCounts(
            moduleEnrollments
                .GroupBy(me => me.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Status, x => x.Count));

        var classStatusBreakdown = BuildStatusCounts(
            classEnrollments
                .GroupBy(ce => ce.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Status, x => x.Count));

        var enrollmentDates = programEnrollments
            .Where(pe => pe.EnrolledAt != null && pe.EnrolledAt >= from && pe.EnrolledAt <= to)
            .Select(pe => pe.EnrolledAt)
            .ToList()
            .ConvertAll(d => d!.Value);

        var enrollmentTrend = BuildTrend(
            enrollmentDates.Select(d => (d, 1m)),
            from,
            to,
            granularity,
            DashboardTrendValueKind.Count);

        var topProgramRows = programEnrollments
            .Where(pe => pe.Program != null)
            .Select(pe => new { pe.ProgramId, ProgramName = pe.Program!.Name })
            .GroupBy(x => new { x.ProgramId, x.ProgramName })
            .Select(g => new TopProgramEnrollmentDto
            {
                ProgramId = g.Key.ProgramId,
                ProgramName = g.Key.ProgramName,
                Count = g.Count()
            })
            .ToList();

        var topPrograms = Paginate(
            SortList(topProgramRows, filter.SortBy, filter.IsDescending, "count",
                (item, key) => key switch
                {
                    "name" => item.ProgramName,
                    "count" => item.Count,
                    _ => item.Count
                }),
            filter.Page,
            filter.PageSize);

        return Task.FromResult(new EnrollmentOverviewDto
        {
            TotalPrograms = programsQuery.Count(),
            TotalModules = modulesQuery.Count(),
            TotalCourses = coursesQuery.Count(),
            ActiveStudents = activeStudents,
            NewEnrollmentsInRange = newEnrollmentsInRange,
            NewEnrollmentsInPreviousRange = newEnrollmentsInPreviousRange,
            CompletionRate = completionRate,
            CompletionRateInPreviousRange = completionRateInPreviousRange,
            ProgramEnrollmentsByStatus = programStatusBreakdown,
            ModuleEnrollmentsByStatus = moduleStatusBreakdown,
            ClassEnrollmentsByStatus = classStatusBreakdown,
            EnrollmentTrend = enrollmentTrend,
            TopProgramsByEnrollment = topPrograms
        });
    }

    public Task<AssessmentOverviewDto> GetAssessmentOverviewAsync(DashboardFilterDto filter)
    {
        var (from, to, granularity) = ResolveRange(filter);
        var (previousFrom, previousTo) = PreviousWindow(from, to);
        var backlogCutoff = DateTime.UtcNow.AddHours(-GradingBacklogThresholdHours);

        var submissions = _unitOfWork.Submissions
            .GetQueryable()
            .Where(s => !s.IsDeleted);

        submissions = ApplySubmissionEntityScope(submissions, filter);

        if (filter.SubmissionStatus.HasValue)
        {
            submissions = submissions.Where(s => s.Status == filter.SubmissionStatus.Value);
        }

        var totalSubmissions = submissions.Count();

        var submissionsInRange = submissions.Count(s =>
            s.SubmittedAt != null && s.SubmittedAt >= from && s.SubmittedAt <= to);

        var submissionsInPreviousRange = submissions.Count(s =>
            s.SubmittedAt != null && s.SubmittedAt >= previousFrom && s.SubmittedAt < previousTo);

        var byStatus = BuildStatusCounts(
            submissions
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Status, x => x.Count));

        var gradingBacklogCount = submissions.Count(s =>
            (s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.TurnedIn)
            && s.SubmittedAt != null
            && s.SubmittedAt < backlogCutoff);

        var turnaroundHours = submissions
            .Where(s => s.Status == SubmissionStatus.Graded
                        && s.SubmittedAt != null
                        && s.GradedAt != null)
            .Select(s => new { s.SubmittedAt, s.GradedAt })
            .ToList()
            .Select(x => (x.GradedAt!.Value - x.SubmittedAt!.Value).TotalHours)
            .ToList();

        var averageTurnaround = turnaroundHours.Count == 0
            ? 0d
            : Math.Round(turnaroundHours.Average(), 2);

        var gradedWithScore = submissions
            .Where(s => s.Status == SubmissionStatus.Graded && s.AssignedGrade != null)
            .Select(s => new
            {
                Grade = s.AssignedGrade,
                PassScore = s.Assignment.PassScore,
                GradedAt = s.GradedAt
            })
            .ToList()
            .ConvertAll(x => new
            {
                Grade = x.Grade!.Value,
                x.PassScore,
                x.GradedAt
            });

        var averageScore = gradedWithScore.Count == 0
            ? 0m
            : Math.Round(gradedWithScore.Average(x => x.Grade), 2);

        var passRate = gradedWithScore.Count == 0
            ? 0m
            : Math.Round(
                (decimal)gradedWithScore.Count(x => x.Grade >= x.PassScore) / gradedWithScore.Count * 100m,
                2);

        var previousGraded = gradedWithScore
            .Where(x => x.GradedAt != null && x.GradedAt >= previousFrom && x.GradedAt < previousTo)
            .ToList();
        var passRateInPreviousRange = previousGraded.Count == 0
            ? 0m
            : Math.Round(
                (decimal)previousGraded.Count(x => x.Grade >= x.PassScore) / previousGraded.Count * 100m,
                2);

        var trendDates = submissions
            .Where(s => s.SubmittedAt != null && s.SubmittedAt >= from && s.SubmittedAt <= to)
            .Select(s => s.SubmittedAt)
            .ToList()
            .ConvertAll(d => d!.Value);

        var submissionsTrend = BuildTrend(
            trendDates.Select(d => (d, 1m)),
            from,
            to,
            granularity,
            DashboardTrendValueKind.Count);

        return Task.FromResult(new AssessmentOverviewDto
        {
            TotalSubmissions = totalSubmissions,
            SubmissionsInRange = submissionsInRange,
            SubmissionsInPreviousRange = submissionsInPreviousRange,
            SubmissionsByStatus = byStatus,
            GradingBacklogCount = gradingBacklogCount,
            GradingBacklogThresholdHours = GradingBacklogThresholdHours,
            AverageGradingTurnaroundHours = averageTurnaround,
            PassRate = passRate,
            PassRateInPreviousRange = passRateInPreviousRange,
            AverageScore = averageScore,
            SubmissionsTrend = submissionsTrend
        });
    }

    public async Task<OperationsOverviewDto> GetOperationsOverviewAsync(DashboardFilterDto filter)
    {
        var (from, to, granularity) = ResolveRange(filter);
        var (previousFrom, previousTo) = PreviousWindow(from, to);

        var classes = _unitOfWork.Classes
            .GetQueryable()
            .Where(c => !c.IsDeleted);

        if (filter.ProgramId.HasValue)
        {
            classes = classes.Where(c => c.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ModuleId.HasValue)
        {
            var moduleId = filter.ModuleId.Value;
            classes = classes.Where(c =>
                c.ClassSessions.Any(cs => !cs.IsDeleted && cs.ModuleId == moduleId));
        }

        if (filter.ClassId.HasValue)
        {
            classes = classes.Where(c => c.Id == filter.ClassId.Value);
        }

        if (filter.ClassStatus.HasValue)
        {
            classes = classes.Where(c => c.Status == filter.ClassStatus.Value);
        }

        var classesByStatus = BuildStatusCounts(
            classes
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Status, x => x.Count));

        var capacityRows = classes
            .Where(c => c.Status != ClassStatus.Draft
                        && c.Status != ClassStatus.ReadyForMentor
                        && c.MaxCapacity > 0)
            .Select(c => new
            {
                c.MaxCapacity,
                c.StartDate,
                c.EndDate,
                Enrolled = c.ClassEnrollments.Count(ce =>
                    !ce.IsDeleted && ce.Status == ClassEnrollmentStatus.Active)
            })
            .ToList();

        var averageCapacityUtilization = AverageCapacity(capacityRows
            .Select(x => (x.MaxCapacity, x.Enrolled))
            .ToList());

        var previousCapacityRows = capacityRows
            .Where(c => c.StartDate <= previousTo && c.EndDate >= previousFrom)
            .Select(x => (x.MaxCapacity, x.Enrolled))
            .ToList();
        var averageCapacityUtilizationInPreviousRange = AverageCapacity(previousCapacityRows);

        var pendingMentorRequests = _unitOfWork.ClassMentorRequests
            .GetQueryable()
            .Where(r => !r.IsDeleted && r.Status == ClassMentorRequestStatus.Pending);

        if (filter.ProgramId.HasValue)
        {
            pendingMentorRequests = pendingMentorRequests
                .Where(r => r.Class.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ClassId.HasValue)
        {
            pendingMentorRequests = pendingMentorRequests
                .Where(r => r.ClassId == filter.ClassId.Value);
        }

        var pendingMentorRequestsCount = pendingMentorRequests.Count();

        var attendances = _unitOfWork.SessionAttendances
            .GetQueryable()
            .Where(a => !a.IsDeleted);

        if (filter.ProgramId.HasValue)
        {
            attendances = attendances.Where(a => a.ClassSession.Class.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ModuleId.HasValue)
        {
            attendances = attendances.Where(a => a.ClassSession.ModuleId == filter.ModuleId.Value);
        }

        if (filter.ClassId.HasValue)
        {
            attendances = attendances.Where(a => a.ClassSession.ClassId == filter.ClassId.Value);
        }

        var totalAttendance = attendances.Count();
        var presentCount = attendances.Count(a => a.Status == AttendanceStatus.Present);
        var averageAttendanceRate = totalAttendance == 0
            ? 0m
            : Math.Round((decimal)presentCount / totalAttendance * 100m, 2);

        var previousAttendances = attendances
            .Where(a => a.ClassSession.StartTime >= previousFrom && a.ClassSession.StartTime < previousTo)
            .Select(a => a.Status)
            .ToList();
        var previousAttendanceTotal = previousAttendances.Count;
        var previousPresent = previousAttendances.Count(s => s == AttendanceStatus.Present);
        var averageAttendanceRateInPreviousRange = previousAttendanceTotal == 0
            ? 0m
            : Math.Round((decimal)previousPresent / previousAttendanceTotal * 100m, 2);

        var attendanceTrendSource = attendances
            .Where(a => a.ClassSession.StartTime >= from && a.ClassSession.StartTime <= to)
            .Select(a => new { a.ClassSession.StartTime, IsPresent = a.Status == AttendanceStatus.Present })
            .ToList();

        var attendanceTrend = BuildRateTrend(
            attendanceTrendSource.Select(x => (x.StartTime, x.IsPresent)),
            from,
            to,
            granularity);

        var mentors = _unitOfWork.Users
            .GetQueryable()
            .Where(u => !u.IsDeleted && u.Role == RoleType.Mentor)
            .Select(u => new { u.Id, Name = u.FullName ?? u.Email, u.MaxConcurrentClasses })
            .ToList();

        var utilization = new List<MentorUtilizationDto>();
        foreach (var mentor in mentors)
        {
            var (assigned, pending) = await ClassMentorRequestValidator.GetUsageBreakdownAsync(
                _unitOfWork,
                mentor.Id);

            utilization.Add(new MentorUtilizationDto
            {
                MentorId = mentor.Id,
                MentorName = mentor.Name,
                Assigned = assigned,
                Pending = pending,
                Max = mentor.MaxConcurrentClasses
                      ?? MentorRequestConstants.DefaultMaxConcurrentClasses
            });
        }

        var pagedUtilization = Paginate(
            SortList(utilization, filter.SortBy, filter.IsDescending, "assigned",
                (item, key) => key switch
                {
                    "name" => item.MentorName,
                    "assigned" => item.Assigned,
                    "pending" => item.Pending,
                    "max" => item.Max,
                    "count" => item.Assigned + item.Pending,
                    _ => item.Assigned
                }),
            filter.Page,
            filter.PageSize);

        return new OperationsOverviewDto
        {
            ClassesByStatus = classesByStatus,
            AverageCapacityUtilization = averageCapacityUtilization,
            AverageCapacityUtilizationInPreviousRange = averageCapacityUtilizationInPreviousRange,
            PendingMentorRequestsCount = pendingMentorRequestsCount,
            AverageAttendanceRate = averageAttendanceRate,
            AverageAttendanceRateInPreviousRange = averageAttendanceRateInPreviousRange,
            AttendanceTrend = attendanceTrend,
            MentorUtilization = pagedUtilization
        };
    }

    private static DashboardOverviewDto MapOverview(DashboardLandingDto landing)
    {
        var activeClassCount = landing.Operations.ClassesByStatus
            .Where(x => x.Status is nameof(ClassStatus.Open) or nameof(ClassStatus.InProgress))
            .Sum(x => x.Count);

        return new DashboardOverviewDto
        {
            Revenue = new RevenueKpiSummaryDto
            {
                TotalRevenue = landing.Revenue.TotalRevenue,
                RevenueInRange = landing.Revenue.RevenueInRange,
                RevenueInPreviousRange = landing.Revenue.RevenueInPreviousRange,
                PendingPaymentRequestsCount = landing.Revenue.PendingPaymentRequestsCount,
                PendingPaymentRequestsAmount = landing.Revenue.PendingPaymentRequestsAmount,
                RefundedAmount = landing.Revenue.RefundedAmount
            },
            Enrollment = new EnrollmentKpiSummaryDto
            {
                TotalPrograms = landing.Enrollment.TotalPrograms,
                ActiveStudents = landing.Enrollment.ActiveStudents,
                NewEnrollmentsInRange = landing.Enrollment.NewEnrollmentsInRange,
                NewEnrollmentsInPreviousRange = landing.Enrollment.NewEnrollmentsInPreviousRange,
                CompletionRate = landing.Enrollment.CompletionRate,
                CompletionRateInPreviousRange = landing.Enrollment.CompletionRateInPreviousRange
            },
            Assessment = new AssessmentKpiSummaryDto
            {
                TotalSubmissions = landing.Assessment.TotalSubmissions,
                SubmissionsInRange = landing.Assessment.SubmissionsInRange,
                SubmissionsInPreviousRange = landing.Assessment.SubmissionsInPreviousRange,
                GradingBacklogCount = landing.Assessment.GradingBacklogCount,
                GradingBacklogThresholdHours = landing.Assessment.GradingBacklogThresholdHours,
                PassRate = landing.Assessment.PassRate,
                PassRateInPreviousRange = landing.Assessment.PassRateInPreviousRange,
                AverageScore = landing.Assessment.AverageScore
            },
            Operations = new OperationsKpiSummaryDto
            {
                ActiveClassCount = activeClassCount,
                AverageCapacityUtilization = landing.Operations.AverageCapacityUtilization,
                AverageCapacityUtilizationInPreviousRange =
                    landing.Operations.AverageCapacityUtilizationInPreviousRange,
                PendingMentorRequestsCount = landing.Operations.PendingMentorRequestsCount,
                AverageAttendanceRate = landing.Operations.AverageAttendanceRate,
                AverageAttendanceRateInPreviousRange =
                    landing.Operations.AverageAttendanceRateInPreviousRange
            }
        };
    }

    private IQueryable<Domain.Entities.Payment> ApplyPaymentEntityScope(
        IQueryable<Domain.Entities.Payment> payments,
        DashboardFilterDto filter)
    {
        if (filter.ProgramId.HasValue)
        {
            var programId = filter.ProgramId.Value;
            payments = payments.Where(p =>
                (p.ProgramEnrollment != null && !p.ProgramEnrollment.IsDeleted && p.ProgramEnrollment.ProgramId == programId)
                || (p.ModuleEnrollment != null && !p.ModuleEnrollment.IsDeleted && p.ModuleEnrollment.Module.ProgramId == programId));
        }

        if (filter.ModuleId.HasValue)
        {
            var moduleId = filter.ModuleId.Value;
            payments = payments.Where(p =>
                (p.ModuleEnrollment != null && !p.ModuleEnrollment.IsDeleted && p.ModuleEnrollment.ModuleId == moduleId)
                || (p.ProgramEnrollment != null
                    && !p.ProgramEnrollment.IsDeleted
                    && p.ProgramEnrollment.ModuleEnrollments.Any(me => !me.IsDeleted && me.ModuleId == moduleId)));
        }

        if (filter.ClassId.HasValue)
        {
            var classId = filter.ClassId.Value;
            var peIds = _unitOfWork.ClassEnrollments
                .GetQueryable()
                .Where(ce => !ce.IsDeleted && ce.ClassId == classId)
                .Select(ce => ce.ProgramEnrollmentId);

            payments = payments.Where(p =>
                peIds.Any(id => (Guid?)id == p.ProgramEnrollmentId));
        }

        return payments;
    }

    private IQueryable<Domain.Entities.PaymentRequest> ApplyPaymentRequestEntityScope(
        IQueryable<Domain.Entities.PaymentRequest> requests,
        DashboardFilterDto filter)
    {
        if (filter.ProgramId.HasValue)
        {
            requests = requests.Where(pr => pr.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ModuleId.HasValue)
        {
            requests = requests.Where(pr => pr.ModuleId == filter.ModuleId.Value);
        }

        if (filter.ClassId.HasValue)
        {
            var classId = filter.ClassId.Value;
            var peIds = _unitOfWork.ClassEnrollments
                .GetQueryable()
                .Where(ce => !ce.IsDeleted && ce.ClassId == classId)
                .Select(ce => ce.ProgramEnrollmentId);

            requests = requests.Where(pr =>
                peIds.Any(id => (Guid?)id == pr.ProgramEnrollmentId));
        }

        return requests;
    }

    private IQueryable<Domain.Entities.Submission> ApplySubmissionEntityScope(
        IQueryable<Domain.Entities.Submission> submissions,
        DashboardFilterDto filter)
    {
        if (filter.ProgramId.HasValue)
        {
            var programId = filter.ProgramId.Value;
            submissions = submissions.Where(s => s.Assignment.Module.ProgramId == programId);
        }

        if (filter.ModuleId.HasValue)
        {
            submissions = submissions.Where(s => s.Assignment.ModuleId == filter.ModuleId.Value);
        }

        if (filter.ClassId.HasValue)
        {
            var classId = filter.ClassId.Value;
            var peIds = _unitOfWork.ClassEnrollments
                .GetQueryable()
                .Where(ce => !ce.IsDeleted && ce.ClassId == classId)
                .Select(ce => ce.ProgramEnrollmentId);

            submissions = submissions.Where(s =>
                s.ModuleEnrollment != null
                && peIds.Any(id => (Guid?)id == s.ModuleEnrollment.ProgramEnrollmentId));
        }

        return submissions;
    }

    private static (DateTime From, DateTime To, DashboardTrendGranularity Granularity) ResolveRange(
        DashboardFilterDto filter)
    {
        var to = DateTime.UtcNow;

        if (filter.FromDate.HasValue && filter.ToDate.HasValue)
        {
            var from = EnsureUtc(filter.FromDate.Value);
            to = EnsureUtc(filter.ToDate.Value);
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var days = (to - from).TotalDays;
            var granularity = days <= 45
                ? DashboardTrendGranularity.Daily
                : days <= 120
                    ? DashboardTrendGranularity.Weekly
                    : DashboardTrendGranularity.Monthly;

            return (from, to, granularity);
        }

        return filter.Range switch
        {
            DashboardRange.Last7Days => (to.AddDays(-7), to, DashboardTrendGranularity.Daily),
            DashboardRange.Last30Days => (to.AddDays(-30), to, DashboardTrendGranularity.Daily),
            DashboardRange.Last90Days => (to.AddDays(-90), to, DashboardTrendGranularity.Weekly),
            DashboardRange.Last12Months => (to.AddMonths(-12), to, DashboardTrendGranularity.Monthly),
            _ => (to.AddDays(-30), to, DashboardTrendGranularity.Daily)
        };
    }

    /// <summary>
    /// Adjacent previous window of equal duration: [previousFrom, previousTo).
    /// </summary>
    private static (DateTime PreviousFrom, DateTime PreviousTo) PreviousWindow(DateTime from, DateTime to)
    {
        var duration = to - from;
        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromDays(30);
        }

        return (from - duration, from);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);

    private static decimal AverageCapacity(List<(int MaxCapacity, int Enrolled)> rows)
    {
        if (rows.Count == 0)
        {
            return 0m;
        }

        return Math.Round(
            rows.Average(x => (decimal)x.Enrolled / x.MaxCapacity * 100m),
            2);
    }

    private static List<StatusCountDto> BuildStatusCounts<TEnum>(Dictionary<TEnum, int> counts)
        where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .OrderBy(v => Convert.ToInt32(v))
            .Select(v => new StatusCountDto
            {
                Status = v.ToString(),
                Count = counts.TryGetValue(v, out var count) ? count : 0
            })
            .ToList();
    }

    private static TrendSeriesDto BuildTrend(
        IEnumerable<(DateTime Date, decimal Value)> points,
        DateTime from,
        DateTime to,
        DashboardTrendGranularity granularity,
        DashboardTrendValueKind valueKind)
    {
        var buckets = CreateBuckets(from, to, granularity);
        var grouped = points
            .GroupBy(p => BucketKey(p.Date, granularity))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));

        return new TrendSeriesDto
        {
            FromDate = from,
            ToDate = to,
            Granularity = granularity,
            ValueKind = valueKind,
            Points = buckets
                .Select(b => new TrendPointDto
                {
                    Label = b.Label,
                    BucketStart = b.BucketStart,
                    Value = grouped.TryGetValue(b.Key, out var value) ? value : 0m
                })
                .ToList()
        };
    }

    private static TrendSeriesDto BuildRateTrend(
        IEnumerable<(DateTime Date, bool IsPresent)> points,
        DateTime from,
        DateTime to,
        DashboardTrendGranularity granularity)
    {
        var buckets = CreateBuckets(from, to, granularity);
        var grouped = points
            .GroupBy(p => BucketKey(p.Date, granularity))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var total = g.Count();
                    if (total == 0)
                    {
                        return 0m;
                    }

                    return Math.Round((decimal)g.Count(x => x.IsPresent) / total * 100m, 2);
                });

        return new TrendSeriesDto
        {
            FromDate = from,
            ToDate = to,
            Granularity = granularity,
            ValueKind = DashboardTrendValueKind.Percent,
            Points = buckets
                .Select(b => new TrendPointDto
                {
                    Label = b.Label,
                    BucketStart = b.BucketStart,
                    Value = grouped.TryGetValue(b.Key, out var value) ? value : 0m
                })
                .ToList()
        };
    }

    private static List<(string Key, string Label, DateTime BucketStart)> CreateBuckets(
        DateTime from,
        DateTime to,
        DashboardTrendGranularity granularity)
    {
        var buckets = new List<(string Key, string Label, DateTime BucketStart)>();
        var cursor = granularity switch
        {
            DashboardTrendGranularity.Daily => DateTime.SpecifyKind(from.Date, DateTimeKind.Utc),
            DashboardTrendGranularity.Weekly => DateTime.SpecifyKind(StartOfWeek(from), DateTimeKind.Utc),
            _ => new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var end = to;

        while (cursor <= end)
        {
            var key = BucketKey(cursor, granularity);
            var label = granularity switch
            {
                DashboardTrendGranularity.Daily => cursor.ToString("yyyy-MM-dd"),
                DashboardTrendGranularity.Weekly => $"W{ISOWeek(cursor):00} {cursor:yyyy}",
                _ => cursor.ToString("MMM yyyy")
            };

            buckets.Add((key, label, cursor));

            cursor = granularity switch
            {
                DashboardTrendGranularity.Daily => cursor.AddDays(1),
                DashboardTrendGranularity.Weekly => cursor.AddDays(7),
                _ => cursor.AddMonths(1)
            };
        }

        return buckets;
    }

    private static string BucketKey(DateTime date, DashboardTrendGranularity granularity)
        => granularity switch
        {
            DashboardTrendGranularity.Daily => date.ToString("yyyy-MM-dd"),
            DashboardTrendGranularity.Weekly => $"{StartOfWeek(date):yyyy-MM-dd}",
            _ => $"{date:yyyy-MM}"
        };

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private static int ISOWeek(DateTime date)
    {
        var day = date.DayOfWeek;
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }

        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }

    private static List<T> SortList<T>(
        List<T> items,
        string? sortBy,
        bool isDescending,
        string defaultKey,
        Func<T, string, IComparable> selector)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) ? defaultKey : sortBy.Trim().ToLowerInvariant();

        return isDescending
            ? items.OrderByDescending(i => selector(i, key)).ToList()
            : items.OrderBy(i => selector(i, key)).ToList();
    }

    private static Pagination<T> Paginate<T>(List<T> items, int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize < 1 ? 5 : pageSize;
        var pageItems = items
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToList();

        return new Pagination<T>(pageItems, items.Count, safePage, safeSize);
    }
}
