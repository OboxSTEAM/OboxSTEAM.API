using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Capstone Flow 2 state on <c>CLS-CAP-SMARTCITY-2026A</c>: Module 1 (Theory) complete for the roster,
/// and the Module 2 LiveOnline + Offline pair pinned to the seed clock (Jitsi join and QR check-in work
/// right after <c>seed/all</c>). Module 3 has no offline showcase.
/// </summary>
public partial class SeedService
{
    private static readonly string[] CapstoneCoTeachExpertCodes = ["EXP-007", "EXP-001"];
    private const string CapstoneBoardRole = "Capstone Co-Teach Advisor";
    private const string CapstoneOfflineVenue = "NVH 601";
    private const int CapstoneLiveOnlineStartsInMinutes = 5;
    private const int CapstoneOfflineStartedMinutesAgo = 30;

    private static DemoProgramDefinition GetCapstoneLiveDefinition()
        => GetDemoProgramDefinitions().First(d => d.ProgramCode == CapstoneLiveProgramCode);

    private sealed record CapstoneLiveSessions(
        ClassSession LiveOnline,
        Activity LiveOnlineActivity,
        ClassSession Offline,
        Activity OfflineActivity);

    private async Task<CapstoneLiveSessions?> LoadCapstoneLiveSessionsAsync(Class classEntity)
    {
        var definition = GetCapstoneLiveDefinition();
        var liveCode = definition.ActivityCode(2, 1);
        var offlineCode = definition.ActivityCode(2, 2);
        var codes = new[] { liveCode, offlineCode };

        var activities = (await _unitOfWork.Activities.GetAllAsync(
                a => codes.Contains(a.Code) && !a.IsDeleted))
            .ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);
        if (!activities.TryGetValue(liveCode, out var liveActivity)
            || !activities.TryGetValue(offlineCode, out var offlineActivity))
        {
            return null;
        }

        var activityIds = activities.Values.Select(a => a.Id).ToList();
        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classEntity.Id
                  && !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && cs.ActivityId != null
                  && activityIds.Contains(cs.ActivityId.Value));

        var live = sessions.FirstOrDefault(cs => cs.ActivityId == liveActivity.Id);
        var offline = sessions.FirstOrDefault(cs => cs.ActivityId == offlineActivity.Id);
        if (live == null || offline == null)
        {
            return null;
        }

        return new CapstoneLiveSessions(
            live,
            liveActivity,
            offline,
            offlineActivity);
    }

    /// <summary>
    /// Pins the capstone class sessions to <see cref="_seedNow"/>. Must run after
    /// <c>RealignSeedSessionWallClocksAsync</c> (which resets demo classes to the weekend grid)
    /// and before <c>EnsureAssignmentWorkWindowsAsync</c> so work windows follow the pinned times.
    /// </summary>
    private async Task ApplyCapstoneLiveSessionsAsync()
    {
        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == CapstoneLiveClassCode && !c.IsDeleted);
        if (classEntity == null)
        {
            _loggerService.LogWarning(
                "Capstone live sessions skipped: class {ClassCode} not found.",
                CapstoneLiveClassCode);
            return;
        }

        var sessions = await LoadCapstoneLiveSessionsAsync(classEntity);
        if (sessions == null)
        {
            _loggerService.LogWarning(
                "Capstone live sessions skipped: LiveOnline/Offline sessions missing on {ClassCode}.",
                CapstoneLiveClassCode);
            return;
        }

        var seedTime = _seedNow;

        // LiveOnline opens for join 15 minutes before start; joining before start + 10 minutes is Present.
        var liveStart = seedTime.AddMinutes(CapstoneLiveOnlineStartsInMinutes);
        await ApplyDemoSessionClockAsync(
            sessions.LiveOnline,
            SessionKind.LiveOnline,
            liveStart,
            liveStart.AddMinutes(sessions.LiveOnlineActivity.DurationMinutes ?? 90),
            classEntity.Code,
            ordinal: 0,
            seedTime);

        // Offline already running: the manager can mint a QR code and students can check in.
        var offlineStart = seedTime.AddMinutes(-CapstoneOfflineStartedMinutesAgo);
        await ApplyDemoSessionClockAsync(
            sessions.Offline,
            SessionKind.Offline,
            offlineStart,
            offlineStart.AddMinutes(sessions.OfflineActivity.DurationMinutes ?? 180),
            classEntity.Code,
            ordinal: 1,
            seedTime);
        sessions.Offline.Location = CapstoneOfflineVenue;
        sessions.Offline.Latitude = 10.870000;
        sessions.Offline.Longitude = 106.803000;
        sessions.Offline.MeetingUrl = null;
        sessions.Offline.RequiresAttendance = true;
        sessions.Offline.RequiresMentorCheckIn = true;
        await _unitOfWork.ClassSessions.Update(sessions.Offline);

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Capstone live pair pinned on {ClassCode}: LiveOnline {LiveStart:u}, Offline {OfflineStart:u}.",
            CapstoneLiveClassCode,
            liveStart,
            offlineStart);
    }

    private static readonly string[] CapstoneUnlockedWindowClassCodes = [CapstoneBuyClassCode, CapstoneLiveClassCode];

    /// <summary>
    /// Opens every assignment window on the two flow classes from seed time (or class start, if earlier)
    /// until the end of the class end day, so no quiz or milestone is time-locked during the demo.
    /// Module prerequisites and self-paced-before-assignment locks still apply. Must run after
    /// <c>EnsureAssignmentWorkWindowsAsync</c>, which places windows from the live timetable.
    /// </summary>
    private async Task OpenCapstoneAssignmentWindowsAsync()
    {
        var seedTime = _seedNow;
        var opened = 0;

        foreach (var classCode in CapstoneUnlockedWindowClassCodes)
        {
            var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code == classCode && !c.IsDeleted);
            if (classEntity == null)
            {
                _loggerService.LogWarning("Capstone window unlock skipped: class {ClassCode} not found.", classCode);
                continue;
            }

            var moduleIds = (await _unitOfWork.Modules.GetAllAsync(
                    m => m.ProgramId == classEntity.ProgramId && !m.IsDeleted))
                .Select(m => m.Id)
                .ToList();
            var assignments = await _unitOfWork.Assignments.GetAllAsync(
                a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted);
            var windows = await _unitOfWork.ClassSessions.GetAllAsync(
                cs => cs.ClassId == classEntity.Id
                      && cs.SessionKind == SessionKind.AssignmentWindow
                      && cs.AssignmentId != null
                      && !cs.IsDeleted);

            var windowStart = classEntity.StartDate < seedTime ? classEntity.StartDate : seedTime.AddHours(-1);
            var windowEnd = AssignmentWindowPlacement.EndOfClassDay(classEntity.EndDate);
            var status = SeedTimeline.ResolveSessionStatus(windowStart, windowEnd, seedTime);

            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var window = windows
                    .Where(cs => cs.AssignmentId == assignment.Id)
                    .OrderBy(cs => cs.StartTime)
                    .FirstOrDefault();
                if (window == null)
                {
                    await _unitOfWork.ClassSessions.AddAsync(
                        CreateSeedAssignmentWindow(classEntity, assignment, windowStart, windowEnd, venueOrdinal: 100 + index));
                    opened++;
                    continue;
                }

                window.StartTime = windowStart;
                window.EndTime = windowEnd;
                window.Status = status;
                window.UpdatedAt = seedTime;
                window.UpdatedBy = Guid.Empty;
                await _unitOfWork.ClassSessions.Update(window);
                opened++;
            }
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Capstone assignment windows opened until class end: {Count} window(s) on {ClassCodes}.",
            opened,
            string.Join(", ", CapstoneUnlockedWindowClassCodes));
    }

    private async Task CollectCapstoneWindowFailuresAsync(List<string> failures)
    {
        var seedTime = _seedNow;
        foreach (var classCode in CapstoneUnlockedWindowClassCodes)
        {
            var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code == classCode && !c.IsDeleted);
            if (classEntity == null)
            {
                continue;
            }

            var moduleIds = (await _unitOfWork.Modules.GetAllAsync(
                    m => m.ProgramId == classEntity.ProgramId && !m.IsDeleted))
                .Select(m => m.Id)
                .ToList();
            var assignments = await _unitOfWork.Assignments.GetAllAsync(
                a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted);
            var openAssignmentIds = (await _unitOfWork.ClassSessions.GetAllAsync(
                    cs => cs.ClassId == classEntity.Id
                          && cs.SessionKind == SessionKind.AssignmentWindow
                          && cs.AssignmentId != null
                          && cs.Status != ClassSessionStatus.Cancelled
                          && cs.StartTime <= seedTime
                          && cs.EndTime > seedTime
                          && !cs.IsDeleted))
                .Select(cs => cs.AssignmentId!.Value)
                .ToHashSet();

            var locked = assignments.Count(a => !openAssignmentIds.Contains(a.Id));
            if (locked > 0)
            {
                failures.Add($"{classCode} has {locked} assignment(s) without an open window at seed time");
            }
        }

        foreach (var openClass in GetDemoProgramDefinitions().SelectMany(d => d.AdditionalOpenClasses))
        {
            var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code == openClass.ClassCode && !c.IsDeleted);
            if (classEntity == null || classEntity.Status != ClassStatus.Open)
            {
                failures.Add($"Capstone open cohort {openClass.ClassCode} missing or not Open");
                continue;
            }

            var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
                ce => ce.ClassId == classEntity.Id && ce.Status == ClassEnrollmentStatus.Active && !ce.IsDeleted);
            if (seats.Count != openClass.StudentCodes.Length)
            {
                failures.Add(
                    $"Capstone open cohort {openClass.ClassCode} has {seats.Count} Active seat(s), expected {openClass.StudentCodes.Length}");
            }
        }
    }

    private async Task ApplyDemoSessionClockAsync(
        ClassSession session,
        SessionKind kind,
        DateTime startTime,
        DateTime endTime,
        string classCode,
        int ordinal,
        DateTime seedTime)
    {
        var status = SeedTimeline.ResolveSessionStatus(startTime, endTime, seedTime);
        var (location, meetingUrl, latitude, longitude) = SeedTimeline.ResolveSeedVenue(kind, classCode, ordinal);

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
    }

    /// <summary>
    /// Module 1 (Theory) complete for every roster student (both readings Done, quiz graded Passed)
    /// and classmate attendance on the live pair. Runs after <c>ClearDemoProgramSubmissionsAsync</c>
    /// so the quiz grades survive.
    /// </summary>
    private async Task ApplyCapstoneClassJourneyAsync()
    {
        var definition = GetCapstoneLiveDefinition();
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == definition.ProgramCode && !p.IsDeleted);
        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == definition.ClassCode && !c.IsDeleted);
        if (program == null || classEntity == null)
        {
            _loggerService.LogWarning("Capstone journey skipped: program or class missing.");
            return;
        }

        var theoryModuleCode = definition.ModuleCode(1);
        var experientialModuleCode = definition.ModuleCode(2);
        var theoryModule = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == theoryModuleCode && !m.IsDeleted);
        var experientialModule = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == experientialModuleCode && !m.IsDeleted);

        var reading1Code = definition.ActivityCode(1, 1);
        var reading2Code = definition.ActivityCode(1, 2);
        var readings = (await _unitOfWork.Activities.GetAllAsync(
                a => (a.Code == reading1Code || a.Code == reading2Code) && !a.IsDeleted))
            .ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);

        var quizCode = definition.AssignmentCode("QUIZ");
        var quiz = await _unitOfWork.Assignments.FirstOrDefaultAsync(a => a.Code == quizCode && !a.IsDeleted);
        var sessions = await LoadCapstoneLiveSessionsAsync(classEntity);

        if (theoryModule == null
            || experientialModule == null
            || !readings.TryGetValue(reading1Code, out var reading1)
            || !readings.TryGetValue(reading2Code, out var reading2)
            || quiz == null
            || sessions == null)
        {
            _loggerService.LogWarning("Capstone journey skipped: curriculum or live sessions missing.");
            return;
        }

        var seedTime = _seedNow;
        // Correct answers out of 5 drawn questions (pass score 50).
        var quizCorrectByStudent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [CapstoneDriverStudentCode] = 5,
            [CapstoneTruongStudentCode] = 4,
            [CapstoneLongStudentCode] = 3,
            [CapstoneHoaStudentCode] = 4,
        };

        var experientialEnrollments = new Dictionary<string, ModuleEnrollment>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < definition.StudentCodes.Length; index++)
        {
            var studentCode = definition.StudentCodes[index];
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode && !u.IsDeleted);
            if (student == null)
            {
                continue;
            }

            var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted);
            if (programEnrollment == null)
            {
                continue;
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
                continue;
            }

            experientialEnrollments[studentCode] = experientialMe;

            var quizSubmittedAt = seedTime.AddDays(-8);
            await EnsureDemoActivityDoneAsync(theoryMe, reading1, seedTime.AddDays(-10));
            await EnsureDemoActivityDoneAsync(theoryMe, reading2, seedTime.AddDays(-9));
            await _unitOfWork.SaveChangesAsync();
            await EnsureDemoQuizPassedAsync(
                student,
                theoryMe,
                quiz,
                quizCorrectByStudent.GetValueOrDefault(studentCode, 4),
                questionOffset: index * 2,
                quizSubmittedAt);

            theoryMe.CompletedAt ??= quizSubmittedAt;
            await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, theoryMe);
            await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                programEnrollment.Id,
                theoryMe);
            await _unitOfWork.SaveChangesAsync();
        }

        // Classmates on the live pair. Việt Anh stays Expected because he joins and checks in live.
        // Long is Late only on the Offline: a LiveOnline join before start is always Present.
        if (experientialEnrollments.TryGetValue(CapstoneTruongStudentCode, out var truongMe))
        {
            await EnsureCapstoneAttendanceAsync(
                sessions.LiveOnline,
                truongMe,
                AttendanceStatus.Present,
                sessions.LiveOnline.StartTime.AddMinutes(-3));
            await EnsureCapstoneAttendanceAsync(
                sessions.Offline,
                truongMe,
                AttendanceStatus.Present,
                sessions.Offline.StartTime.AddMinutes(2));
        }

        if (experientialEnrollments.TryGetValue(CapstoneLongStudentCode, out var longMe))
        {
            await EnsureCapstoneAttendanceAsync(
                sessions.Offline,
                longMe,
                AttendanceStatus.Late,
                sessions.Offline.StartTime.AddMinutes(15));
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Capstone journey: Module 1 complete for {Count} student(s) on {ClassCode}; classmate attendance seeded.",
            experientialEnrollments.Count,
            CapstoneLiveClassCode);
    }

    private async Task EnsureCapstoneAttendanceAsync(
        ClassSession session,
        ModuleEnrollment moduleEnrollment,
        AttendanceStatus status,
        DateTime checkedInAt)
    {
        var existing = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(
            sa => sa.ClassSessionId == session.Id
                  && sa.StudentId == moduleEnrollment.StudentId
                  && !sa.IsDeleted);
        if (existing != null)
        {
            existing.ModuleEnrollmentId = moduleEnrollment.Id;
            existing.Status = status;
            existing.CheckedInAt = checkedInAt;
            existing.RecordedBy = moduleEnrollment.StudentId;
            existing.UpdatedAt = _seedNow;
            existing.UpdatedBy = Guid.Empty;
            await _unitOfWork.SessionAttendances.Update(existing);
            return;
        }

        await _unitOfWork.SessionAttendances.AddAsync(new SessionAttendance
        {
            Id = Guid.NewGuid(),
            ClassSessionId = session.Id,
            StudentId = moduleEnrollment.StudentId,
            ModuleEnrollmentId = moduleEnrollment.Id,
            Status = status,
            CheckedInAt = checkedInAt,
            RecordedBy = moduleEnrollment.StudentId,
            CreatedAt = checkedInAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task EnsureDemoActivityDoneAsync(
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

    private async Task EnsureDemoQuizPassedAsync(
        User student,
        ModuleEnrollment moduleEnrollment,
        Assignment quiz,
        int correctCount,
        int questionOffset,
        DateTime submittedAt)
    {
        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.StudentId == student.Id
                 && s.AssignmentId == quiz.Id
                 && s.ModuleEnrollmentId == moduleEnrollment.Id
                 && !s.IsDeleted);

        var startedAt = submittedAt.AddMinutes(-(quiz.TimeLimitMinutes ?? 15) + 3);
        if (submission == null)
        {
            submission = new Submission
            {
                Id = Guid.NewGuid(),
                Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                AssignmentId = quiz.Id,
                StudentId = student.Id,
                ModuleEnrollmentId = moduleEnrollment.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.Graded,
                StartedAt = startedAt,
                SubmittedAt = submittedAt,
                GradedAt = submittedAt,
                CreatedAt = startedAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.Submissions.AddAsync(submission);
            await _unitOfWork.SaveChangesAsync();
        }

        var existingQuestions = await _unitOfWork.QuizQuestions.GetAllAsync(
            q => q.SubmissionId == submission.Id && !q.IsDeleted);
        if (existingQuestions.Count > 0 && submission.Status == SubmissionStatus.Graded)
        {
            return;
        }

        if (quiz.QuestionBankId is null)
        {
            _loggerService.LogWarning("Capstone quiz snapshot skipped: {Code} has no QuestionBankId.", quiz.Code);
            return;
        }

        var bankQuestions = (await _unitOfWork.BankQuestions.GetAllAsync(
                q => q.QuestionBankId == quiz.QuestionBankId.Value && !q.IsDeleted))
            .OrderBy(q => q.OrderIndex)
            .ToList();
        var drawCount = Math.Min(quiz.QuestionCount ?? 5, bankQuestions.Count);
        if (drawCount == 0)
        {
            return;
        }

        var drawn = Enumerable.Range(0, drawCount)
            .Select(i => bankQuestions[(questionOffset + i) % bankQuestions.Count])
            .ToList();
        var drawnIds = drawn.Select(q => q.Id).ToList();
        var optionsByBankQuestion = (await _unitOfWork.BankQuestionOptions.GetAllAsync(
                o => drawnIds.Contains(o.BankQuestionId) && !o.IsDeleted))
            .GroupBy(o => o.BankQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var quizQuestions = new List<QuizQuestion>();
        var quizOptions = new List<QuizOption>();
        var quizAnswers = new List<QuizAnswer>();

        for (var index = 0; index < drawn.Count; index++)
        {
            var bankQuestion = drawn[index];
            if (!optionsByBankQuestion.TryGetValue(bankQuestion.Id, out var bankOptions) || bankOptions.Count == 0)
            {
                continue;
            }

            var quizQuestion = new QuizQuestion
            {
                Id = Guid.NewGuid(),
                AssignmentId = quiz.Id,
                SubmissionId = submission.Id,
                BankQuestionId = bankQuestion.Id,
                QuestionText = bankQuestion.QuestionText,
                QuestionType = bankQuestion.QuestionType,
                Points = bankQuestion.Points,
                OrderIndex = index + 1,
                AttemptNumber = submission.AttemptNumber,
                CreatedAt = startedAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            quizQuestions.Add(quizQuestion);

            QuizOption? correctOption = null;
            QuizOption? wrongOption = null;
            foreach (var bankOption in bankOptions)
            {
                var option = new QuizOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = quizQuestion.Id,
                    OptionText = bankOption.OptionText,
                    IsCorrect = bankOption.IsCorrect,
                    CreatedAt = startedAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                };
                quizOptions.Add(option);
                if (bankOption.IsCorrect)
                {
                    correctOption = option;
                }
                else
                {
                    wrongOption ??= option;
                }
            }

            var selected = index < correctCount ? correctOption : wrongOption ?? correctOption;
            if (selected == null)
            {
                continue;
            }

            quizAnswers.Add(new QuizAnswer
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                QuizQuestionId = quizQuestion.Id,
                QuizOptionId = selected.Id,
                CreatedAt = submittedAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (quizQuestions.Count == 0)
        {
            return;
        }

        await _unitOfWork.QuizQuestions.AddRangeAsync(quizQuestions);
        await _unitOfWork.QuizOptions.AddRangeAsync(quizOptions);
        await _unitOfWork.QuizAnswers.AddRangeAsync(quizAnswers);

        foreach (var question in quizQuestions)
        {
            question.Options = quizOptions.Where(o => o.QuestionId == question.Id).ToList();
        }

        var grade = QuizScoreCalculator.Calculate(quiz, quizQuestions, quizAnswers);
        submission.Status = SubmissionStatus.Graded;
        submission.AssignedGrade = grade.AssignedGrade;
        submission.SubmittedAt ??= submittedAt;
        submission.GradedAt ??= submittedAt;
        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// EXP-007 and EXP-001 on both capstone program boards so Offline co-teach is valid.
    /// </summary>
    private async Task<List<Expert>> EnsureCapstoneProgramBoardsAsync()
    {
        var experts = (await _unitOfWork.Experts.GetAllAsync(
                e => CapstoneCoTeachExpertCodes.Contains(e.Code) && !e.IsDeleted))
            .OrderBy(e => Array.IndexOf(CapstoneCoTeachExpertCodes, e.Code))
            .ToList();
        if (experts.Count < CapstoneCoTeachExpertCodes.Length)
        {
            _loggerService.LogWarning(
                "Capstone co-teach experts incomplete: found {Found} of {Expected}.",
                experts.Count,
                CapstoneCoTeachExpertCodes.Length);
        }

        var demoProgramIds = await GetDemoProgramIdsAsync();
        foreach (var expert in experts)
        {
            foreach (var programId in demoProgramIds)
            {
                await EnsureExpertOnProgramBoardAsync(expert, programId, CapstoneBoardRole);
            }
        }

        return experts;
    }

    private async Task CollectCapstoneDemoFailuresAsync(List<string> failures)
    {
        await CollectCapstoneWindowFailuresAsync(failures);

        var buyClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == CapstoneBuyClassCode && !c.IsDeleted);
        if (buyClass == null || buyClass.Status != ClassStatus.Open)
        {
            failures.Add($"Capstone class {CapstoneBuyClassCode} missing or not Open");
        }

        var liveClass = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == CapstoneLiveClassCode && !c.IsDeleted);
        if (liveClass == null)
        {
            failures.Add($"Missing capstone class {CapstoneLiveClassCode}");
            return;
        }

        var sessions = await LoadCapstoneLiveSessionsAsync(liveClass);
        if (sessions == null)
        {
            failures.Add($"{CapstoneLiveClassCode} missing the LiveOnline/Offline pair");
            return;
        }

        var seedTime = _seedNow;
        if (sessions.Offline.Status != ClassSessionStatus.InProgress
            || sessions.Offline.StartTime > seedTime
            || sessions.Offline.EndTime <= seedTime)
        {
            failures.Add($"{CapstoneLiveClassCode} Offline session is not InProgress at seed time");
        }

        var joinOpensAt = sessions.LiveOnline.StartTime.AddMinutes(-ClassSessionJoinValidator.JoinOpenMinutesBeforeStart);
        if (seedTime < joinOpensAt || seedTime > sessions.LiveOnline.EndTime)
        {
            failures.Add($"{CapstoneLiveClassCode} LiveOnline join window is not open at seed time");
        }

        var acceptedExperts = await _unitOfWork.ClassSessionExperts.GetAllAsync(
            e => e.ClassSessionId == sessions.Offline.Id
                 && e.Status == ClassSessionExpertStatus.Accepted
                 && !e.IsDeleted);
        if (acceptedExperts.Count < CapstoneCoTeachExpertCodes.Length)
        {
            failures.Add(
                $"{CapstoneLiveClassCode} Offline session has {acceptedExperts.Count} Accepted co-teach expert(s), expected {CapstoneCoTeachExpertCodes.Length}");
        }

        var demoMentor = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == CapstoneDemoMentorCode && !u.IsDeleted);
        if (demoMentor == null)
        {
            failures.Add($"Missing capstone demo mentor {CapstoneDemoMentorCode}");
        }
        else
        {
            var mentoredClasses = await _unitOfWork.Classes.GetAllAsync(
                c => c.MentorId == demoMentor.Id && !c.IsDeleted);
            if (mentoredClasses.Count != 1 || mentoredClasses[0].Id != liveClass.Id)
            {
                failures.Add(
                    $"{CapstoneDemoMentorCode} must mentor only {CapstoneLiveClassCode}, got {mentoredClasses.Count} class(es)");
            }

            var mentorRequests = await _unitOfWork.ClassMentorRequests.GetAllAsync(
                r => r.MentorId == demoMentor.Id && !r.IsDeleted);
            if (mentorRequests.Count > 0)
            {
                failures.Add(
                    $"{CapstoneDemoMentorCode} should start the demo with no class mentor requests, got {mentorRequests.Count}");
            }
        }

        var driver = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == CapstoneDriverStudentCode && !u.IsDeleted);
        if (driver == null)
        {
            failures.Add($"Missing capstone student {CapstoneDriverStudentCode}");
            return;
        }

        var activeSeats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == driver.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
        if (activeSeats.Count != 1)
        {
            failures.Add(
                $"{CapstoneDriverStudentCode} expected exactly 1 Active class before the demo, got {activeSeats.Count}");
        }
    }
}
