using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedResearchMilestoneDataAsync()
    {
        _loggerService.LogInformation("Starting seed research milestones");
        var existingRoboticsMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-ROBOTICS-03-01" && !rm.IsDeleted);
        if (existingRoboticsMilestone != null)
        {
            _loggerService.LogInformation("Robotics research milestones already exist, skipping robotics seeding");
            return;
        }

        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
        if (moduleRobotics3 == null)
        {
            _loggerService.LogWarning("Module MOD-ROBOTICS-03 not found. Skipping research milestone seeding.");
            return;
        }

        var designBriefActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-07-01");
        var prototypeBuildActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-07-02");
        var finalPresentationActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-07-03");

        if (designBriefActivity == null || prototypeBuildActivity == null || finalPresentationActivity == null)
        {
            _loggerService.LogWarning(
                "Research module activities not found. Skipping research milestone seeding.");
            return;
        }

        var seedTime = DateTime.UtcNow;
        var availabilityFrom = seedTime.AddDays(-30);
        var availabilityUntil = seedTime.AddDays(90);

        var assignmentDesign = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = "ASG-ROBOTICS-03-01",
            ModuleId = moduleRobotics3.Id,
            Title = "Design Brief Submission",
            Description = "Submit your robot design document and component list.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(14),
            AvailableFrom = availabilityFrom,
            AvailableUntil = availabilityUntil,
            MaxAttempts = 3,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var assignmentPrototype = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = "ASG-ROBOTICS-03-02",
            ModuleId = moduleRobotics3.Id,
            Title = "Prototype Build Report",
            Description = "Upload photos, build notes, and a short test summary.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(28),
            AvailableFrom = availabilityFrom,
            AvailableUntil = availabilityUntil,
            MaxAttempts = 3,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var assignmentCapstone = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = "ASG-ROBOTICS-03-03",
            ModuleId = moduleRobotics3.Id,
            Title = "Capstone Presentation Deliverable",
            Description = "Submit your final presentation deck and demo video link.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 70m,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(42),
            AvailableFrom = availabilityFrom,
            AvailableUntil = availabilityUntil,
            MaxAttempts = 2,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var milestoneDesign = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = "RML-ROBOTICS-03-01",
            ModuleId = moduleRobotics3.Id,
            Title = "Design & Planning",
            Description = "Plan the robot challenge approach and document design choices.",
            MilestoneOrder = 1,
            IsCapstone = false,
            AssignmentId = assignmentDesign.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var milestonePrototype = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = "RML-ROBOTICS-03-02",
            ModuleId = moduleRobotics3.Id,
            Title = "Prototype Assembly",
            Description = "Build and test the first working prototype.",
            MilestoneOrder = 2,
            IsCapstone = false,
            AssignmentId = assignmentPrototype.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var milestoneCapstone = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = "RML-ROBOTICS-03-03",
            ModuleId = moduleRobotics3.Id,
            Title = "Capstone Presentation",
            Description = "Present the final robot and reflect on engineering trade-offs.",
            MilestoneOrder = 3,
            IsCapstone = true,
            AssignmentId = assignmentCapstone.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        await _unitOfWork.Assignments.AddRangeAsync(
            new List<Assignment> { assignmentDesign, assignmentPrototype, assignmentCapstone });
        await _unitOfWork.ResearchMilestones.AddRangeAsync(
            new List<ResearchMilestone> { milestoneDesign, milestonePrototype, milestoneCapstone });
        await _unitOfWork.SaveChangesAsync();

        var milestoneActivities = new List<ResearchMilestoneActivity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestoneDesign.Id,
                ActivityId = designBriefActivity.Id,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestonePrototype.Id,
                ActivityId = prototypeBuildActivity.Id,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestoneCapstone.Id,
                ActivityId = finalPresentationActivity.Id,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            }
        };

        await _unitOfWork.ResearchMilestoneActivities.AddRangeAsync(milestoneActivities);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed research milestones — 3 milestone(s) and 3 activity link(s) created.");
    }

    private async Task SeedResearchModuleEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed research module enrollments");
        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
        if (moduleRobotics3 == null)
        {
            _loggerService.LogWarning("Module MOD-ROBOTICS-03 not found. Skipping research module enrollment seeding.");
            return;
        }

        var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
        var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
        var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
        ProgramEnrollment? programEnrollmentStudent1 = null;
        if (student1 != null)
        {
            programEnrollmentStudent1 = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student1.Id && !pe.IsDeleted);
        }
        var enrollTime = DateTime.UtcNow;
        var moduleEnrollments = new List<ModuleEnrollment>();

        async Task TryAddEnrollmentAsync(User? student, Guid? programEnrollmentId, decimal progressPercent)
        {
            if (student == null)
            {
                return;
            }

            var exists = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student.Id
                      && me.ModuleId == moduleRobotics3.Id
                      && !me.IsDeleted);

            if (exists != null)
            {
                return;
            }

            moduleEnrollments.Add(new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ModuleId = moduleRobotics3.Id,
                ProgramEnrollmentId = programEnrollmentId,
                Status = EnrollmentStatus.Active,
                ProgressPercent = progressPercent,
                EnrolledAt = enrollTime.AddDays(-7),
                StartedAt = enrollTime.AddDays(-5),
                CreatedAt = enrollTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
        }

        await TryAddEnrollmentAsync(student1, programEnrollmentStudent1?.Id, 55m);
        await TryAddEnrollmentAsync(student2, null, 20m);
        await TryAddEnrollmentAsync(student3, null, 10m);

        if (moduleEnrollments.Count == 0)
        {
            _loggerService.LogInformation("Research module enrollments already exist, skipping seeding");
            return;
        }

        await _unitOfWork.ModuleEnrollments.AddRangeAsync(moduleEnrollments);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed research module enrollments — {Count} enrollment(s) created.",
            moduleEnrollments.Count);
    }

    private async Task SeedResearchActivityProgressAsync()
    {
        _loggerService.LogInformation("Starting seed research activity progress");
        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
        if (moduleRobotics3 == null)
        {
            _loggerService.LogWarning("Module MOD-ROBOTICS-03 not found. Skipping research activity progress seeding.");
            return;
        }

        var designBriefActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-07-01");
        var prototypeBuildActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-07-02");
        if (designBriefActivity == null || prototypeBuildActivity == null)
        {
            _loggerService.LogWarning("Research activities not found. Skipping research activity progress seeding.");
            return;
        }

        var studentCodes = new[] { "STD-001", "STD-002", "STD-003" };
        var progressTime = DateTime.UtcNow;
        var activityProgresses = new List<ActivityProgress>();

        foreach (var studentCode in studentCodes)
        {
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
            if (student == null)
            {
                continue;
            }

            var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student.Id
                      && me.ModuleId == moduleRobotics3.Id
                      && !me.IsDeleted);

            if (enrollment == null)
            {
                continue;
            }

            var existingProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                ap => ap.ModuleEnrollmentId == enrollment.Id
                      && ap.ActivityId == designBriefActivity.Id
                      && !ap.IsDeleted);

            if (existingProgress == null)
            {
                activityProgresses.Add(new ActivityProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ActivityId = designBriefActivity.Id,
                    ModuleEnrollmentId = enrollment.Id,
                    ActivityStatus = ActivityStatus.Done,
                    IsCompleted = true,
                    CompletedAt = progressTime.AddDays(-4),
                    CreatedAt = progressTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (studentCode == "STD-001")
            {
                var existingPrototypeProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                    ap => ap.ModuleEnrollmentId == enrollment.Id
                          && ap.ActivityId == prototypeBuildActivity.Id
                          && !ap.IsDeleted);

                if (existingPrototypeProgress == null)
                {
                    activityProgresses.Add(new ActivityProgress
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student.Id,
                        ActivityId = prototypeBuildActivity.Id,
                        ModuleEnrollmentId = enrollment.Id,
                        ActivityStatus = ActivityStatus.Done,
                        IsCompleted = true,
                        CompletedAt = progressTime.AddDays(-2),
                        CreatedAt = progressTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }
            }
        }

        if (activityProgresses.Count == 0)
        {
            _loggerService.LogInformation("Research activity progress already exists, skipping seeding");
            return;
        }

        await _unitOfWork.ActivityProgresses.AddRangeAsync(activityProgresses);
        await _unitOfWork.SaveChangesAsync();

        foreach (var studentCode in studentCodes)
        {
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
            if (student == null)
            {
                continue;
            }

            var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student.Id
                      && me.ModuleId == moduleRobotics3.Id
                      && !me.IsDeleted);

            if (enrollment == null)
            {
                continue;
            }

            var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                _unitOfWork,
                enrollment);

            if (moduleProgressPercent >= 100m && enrollment.ProgramEnrollmentId.HasValue)
            {
                await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                    _unitOfWork,
                    enrollment.ProgramEnrollmentId.Value,
                    enrollment);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed research activity progress — {Count} record(s) created.",
            activityProgresses.Count);
    }

    private async Task SeedEnrollmentActivityProgressAsync()
    {
        _loggerService.LogInformation("Starting seed enrollment activity progress");
        var seedTime = DateTime.UtcNow;

        await TrySeedModuleActivityProgressAsync(
            "STD-001",
            "MOD-ROBOTICS-01",
            "PRG-ROBOTICS",
            [
                ("ACT-ROBOTICS-01-01", ActivityStatus.Done, seedTime.AddDays(-6)),
                ("ACT-ROBOTICS-01-02", ActivityStatus.Done, seedTime.AddDays(-4)),
                ("ACT-ROBOTICS-01-03", ActivityStatus.InProgress, null),
            ]);

        await TrySeedModuleActivityProgressAsync(
            "STD-002",
            "MOD-WEBDEV-01",
            "PRG-WEBDEV",
            [
                ("ACT-WEBDEV-01-01", ActivityStatus.Done, seedTime.AddDays(-3)),
                ("ACT-WEBDEV-01-02", ActivityStatus.InProgress, null),
            ]);

        _loggerService.LogInformation("Finished seed enrollment activity progress");
    }

    private async Task TrySeedModuleActivityProgressAsync(
        string studentCode,
        string moduleCode,
        string programCode,
        (string ActivityCode, ActivityStatus Status, DateTime? CompletedAt)[] activitySeeds)
    {
        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == moduleCode);
        if (student == null || module == null)
        {
            _loggerService.LogWarning(
                "Student {StudentCode} or module {ModuleCode} not found. Skipping activity progress seeding.",
                studentCode,
                moduleCode);
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == programCode);
        ProgramEnrollment? programEnrollment = null;
        if (program != null)
        {
            programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted);
        }

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == module.Id
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted);

        if (moduleEnrollment == null)
        {
            _loggerService.LogWarning(
                "Active module enrollment not found for {StudentCode} / {ModuleCode}. Skipping activity progress seeding.",
                studentCode,
                moduleCode);
            return;
        }

        var enrollmentUpdated = false;
        if (programEnrollment != null && moduleEnrollment.ProgramEnrollmentId != programEnrollment.Id)
        {
            moduleEnrollment.ProgramEnrollmentId = programEnrollment.Id;
            enrollmentUpdated = true;
        }

        if (!moduleEnrollment.StartedAt.HasValue)
        {
            moduleEnrollment.StartedAt = DateTime.UtcNow.AddDays(-7);
            enrollmentUpdated = true;
        }

        if (enrollmentUpdated)
        {
            await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
        }

        var seedTime = DateTime.UtcNow;
        var progressChanged = enrollmentUpdated;

        foreach (var (activityCode, status, completedAt) in activitySeeds)
        {
            var activity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == activityCode);
            if (activity == null)
            {
                _loggerService.LogWarning("Activity {ActivityCode} not found. Skipping.", activityCode);
                continue;
            }

            var existingProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                      && ap.ActivityId == activity.Id
                      && !ap.IsDeleted);

            if (existingProgress != null)
            {
                if (existingProgress.ActivityStatus == status
                    && existingProgress.IsCompleted == (status == ActivityStatus.Done))
                {
                    continue;
                }

                existingProgress.ActivityStatus = status;
                existingProgress.IsCompleted = status == ActivityStatus.Done;
                existingProgress.CompletedAt = status == ActivityStatus.Done
                    ? completedAt ?? seedTime
                    : null;
                await _unitOfWork.ActivityProgresses.Update(existingProgress);
                progressChanged = true;
                continue;
            }

            await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ActivityId = activity.Id,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ActivityStatus = status,
                IsCompleted = status == ActivityStatus.Done,
                CompletedAt = status == ActivityStatus.Done ? completedAt ?? seedTime : null,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            progressChanged = true;
        }

        if (!progressChanged)
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync();

        var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
            _unitOfWork,
            moduleEnrollment);

        if (moduleProgressPercent >= 100m && moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                moduleEnrollment.ProgramEnrollmentId.Value,
                moduleEnrollment);
        }

        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Seeded activity progress for {StudentCode} / {ModuleCode} — module progress {ProgressPercent}%.",
            studentCode,
            moduleCode,
            moduleProgressPercent);
    }

    private async Task BackfillActivityProgressStatusAsync()
    {
        _loggerService.LogInformation("Starting backfill activity progress status");
        var progresses = await _unitOfWork.ActivityProgresses.GetAllAsync(ap => !ap.IsDeleted);
        var moduleEnrollmentIds = new HashSet<Guid>();
        var changed = false;

        foreach (var progress in progresses)
        {
            if (progress.IsCompleted && progress.ActivityStatus != ActivityStatus.Done)
            {
                progress.ActivityStatus = ActivityStatus.Done;
                progress.CompletedAt ??= DateTime.UtcNow;
                await _unitOfWork.ActivityProgresses.Update(progress);
                moduleEnrollmentIds.Add(progress.ModuleEnrollmentId);
                changed = true;
            }
        }

        if (!changed)
        {
            _loggerService.LogInformation("No activity progress status backfill required");
            return;
        }

        await _unitOfWork.SaveChangesAsync();

        foreach (var moduleEnrollmentId in moduleEnrollmentIds)
        {
            var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId);
            if (moduleEnrollment == null || moduleEnrollment.IsDeleted)
            {
                continue;
            }

            var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                _unitOfWork,
                moduleEnrollment);

            if (moduleProgressPercent >= 100m && moduleEnrollment.ProgramEnrollmentId.HasValue)
            {
                await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                    _unitOfWork,
                    moduleEnrollment.ProgramEnrollmentId.Value,
                    moduleEnrollment);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished backfill activity progress status — {Count} module enrollment(s) recalculated.",
            moduleEnrollmentIds.Count);
    }

    private async Task SeedResearchSubmissionsAsync()
    {
        _loggerService.LogInformation("Starting seed research submissions");

        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
        var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
        var milestoneDesign = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-ROBOTICS-03-01" && !rm.IsDeleted);
        var milestonePrototype = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-ROBOTICS-03-02" && !rm.IsDeleted);

        if (mentor == null
            || student1 == null
            || student2 == null
            || moduleRobotics3 == null
            || milestoneDesign == null
            || milestonePrototype == null)
        {
            _loggerService.LogWarning("Required research submission seed data not found. Skipping.");
            return;
        }

        var assignmentDesign = await _unitOfWork.Assignments.GetByIdAsync(milestoneDesign.AssignmentId);
        var assignmentPrototype = await _unitOfWork.Assignments.GetByIdAsync(milestonePrototype.AssignmentId);
        if (assignmentDesign == null || assignmentPrototype == null)
        {
            _loggerService.LogWarning("Research milestone assignments not found. Skipping research submission seeding.");
            return;
        }

        var enrollmentStudent1 = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student1.Id
                  && me.ModuleId == moduleRobotics3.Id
                  && !me.IsDeleted);
        var enrollmentStudent2 = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student2.Id
                  && me.ModuleId == moduleRobotics3.Id
                  && !me.IsDeleted);

        if (enrollmentStudent1 == null || enrollmentStudent2 == null)
        {
            _loggerService.LogWarning("Research module enrollments not found. Skipping research submission seeding.");
            return;
        }

        var seedTime = DateTime.UtcNow;
        var submissions = new List<Submission>();

        if (!await SubmissionCodeExistsAsync("SUB-RML0301A"))
        {
            submissions.Add(new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-RML0301A",
                AssignmentId = assignmentDesign.Id,
                StudentId = student1.Id,
                ModuleEnrollmentId = enrollmentStudent1.Id,
                ResearchMilestoneId = milestoneDesign.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.Graded,
                ContentText = "Our team chose a line-following chassis with ultrasonic obstacle detection.",
                FileUrl = "https://storage.oboxsteam.com/submissions/robotics-design-brief-std001.pdf",
                AssignedGrade = 85m,
                MentorFeedback = "Strong design rationale. Consider adding a power budget table.",
                VerifiedBy = mentor.Id,
                SubmittedAt = seedTime.AddDays(-6),
                GradedAt = seedTime.AddDays(-4),
                CreatedAt = seedTime.AddDays(-8),
                CreatedBy = mentor.Id,
                IsDeleted = false
            });
        }

        if (!await SubmissionCodeExistsAsync("SUB-RML0302A"))
        {
            submissions.Add(new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-RML0302A",
                AssignmentId = assignmentPrototype.Id,
                StudentId = student1.Id,
                ModuleEnrollmentId = enrollmentStudent1.Id,
                ResearchMilestoneId = milestonePrototype.Id,
                AttemptNumber = 0,
                Status = SubmissionStatus.Pending,
                CreatedAt = seedTime.AddDays(-1),
                CreatedBy = mentor.Id,
                IsDeleted = false
            });
        }

        if (!await SubmissionCodeExistsAsync("SUB-RML0301B"))
        {
            submissions.Add(new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-RML0301B",
                AssignmentId = assignmentDesign.Id,
                StudentId = student2.Id,
                ModuleEnrollmentId = enrollmentStudent2.Id,
                ResearchMilestoneId = milestoneDesign.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.ReturnedForRevision,
                ContentText = "Initial design draft with motor placement notes.",
                FileUrl = "https://storage.oboxsteam.com/submissions/robotics-design-brief-std002.pdf",
                MentorFeedback = "Please add sensor placement diagrams and a parts list before resubmitting.",
                SubmittedAt = seedTime.AddDays(-3),
                CreatedAt = seedTime.AddDays(-5),
                CreatedBy = mentor.Id,
                UpdatedAt = seedTime.AddDays(-2),
                UpdatedBy = mentor.Id,
                IsDeleted = false
            });
        }

        if (submissions.Count == 0)
        {
            _loggerService.LogInformation("Research submissions already exist, skipping seeding");
            return;
        }

        await _unitOfWork.Submissions.AddRangeAsync(submissions);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed research submissions — {Count} submission(s) created.",
            submissions.Count);
    }

    private async Task<bool> SubmissionCodeExistsAsync(string code)
        => await _unitOfWork.Submissions.FirstOrDefaultAsync(s => s.Code == code && !s.IsDeleted) != null;

    private async Task SeedExtendedResearchDataAsync()
    {
        await SeedWebDevResearchMilestonesAsync();
        await SeedWebDevResearchEnrollmentsAsync();
        await SeedExtendedResearchSubmissionsAsync();
    }

    private async Task SeedWebDevResearchMilestonesAsync()
    {
        _loggerService.LogInformation("Starting seed webdev research milestones");
        var existingMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-WEBDEV-03-01" && !rm.IsDeleted);
        if (existingMilestone != null)
        {
            _loggerService.LogInformation("WebDev research milestones already exist, skipping");
            return;
        }

        var moduleWebDev3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-03");
        if (moduleWebDev3 == null)
        {
            _loggerService.LogWarning("MOD-WEBDEV-03 not found. Skipping webdev research milestones.");
            return;
        }

        var seedTime = DateTime.UtcNow;
        var courseWebDev3 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-WEBDEV-03");
        if (courseWebDev3 == null)
        {
            courseWebDev3 = new Course
            {
                Id = Guid.NewGuid(),
                Code = "CRS-WEBDEV-03",
                ModuleId = moduleWebDev3.Id,
                Name = "Responsive Design & Deployment - Capstone Cohort",
                Description = "Research cohort for responsive design and deployment capstone work.",
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };
            await _unitOfWork.Courses.AddAsync(courseWebDev3);
            await _unitOfWork.SaveChangesAsync();

            var activities = new List<Activity>
            {
                NewActivity("ACT-WEBDEV-03-01", "Responsive Design Brief", ActivityType.SelfPaced, 1,
                    "Review responsive design requirements and breakpoints.", null, null, null, null, false, false),
                NewActivity("ACT-WEBDEV-03-02", "Deployment Workshop", ActivityType.LiveOnline, 2,
                    "Live session on hosting and deployment pipelines.",
                    "https://meet.google.com/webdev-deploy",
                    seedTime.AddDays(10).Date.AddHours(10),
                    seedTime.AddDays(10).Date.AddHours(12),
                    20, false, false),
                NewActivity("ACT-WEBDEV-03-03", "Capstone Demo Day", ActivityType.LiveOnline, 3,
                    "Present deployed capstone sites to mentors.",
                    "https://meet.google.com/webdev-capstone",
                    seedTime.AddDays(28).Date.AddHours(14),
                    seedTime.AddDays(28).Date.AddHours(16),
                    30, false, true)
            };

            foreach (var activity in activities)
            {
                activity.CourseId = courseWebDev3.Id;
                activity.CreatedAt = seedTime;
                activity.CreatedBy = Guid.Empty;
                activity.IsDeleted = false;
            }

            await _unitOfWork.Activities.AddRangeAsync(activities);
            await _unitOfWork.SaveChangesAsync();
        }

        var wireframeActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-01");
        var deploymentActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-02");
        var capstoneActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-03");

        if (wireframeActivity == null || deploymentActivity == null || capstoneActivity == null)
        {
            _loggerService.LogWarning("WebDev research activities not found. Skipping milestones.");
            return;
        }

        var availabilityFrom = seedTime.AddDays(-30);
        var availabilityUntil = seedTime.AddDays(90);

        var assignmentWireframe = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = "ASG-WEBDEV-03-01",
            ModuleId = moduleWebDev3.Id,
            Title = "Responsive Wireframe Package",
            Description = "Submit wireframes for mobile, tablet, and desktop breakpoints.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(14),
            AvailableFrom = availabilityFrom,
            AvailableUntil = availabilityUntil,
            MaxAttempts = 3,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var assignmentCapstone = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = "ASG-WEBDEV-03-02",
            ModuleId = moduleWebDev3.Id,
            Title = "Deployed Capstone Site",
            Description = "Submit the live URL and source archive for the capstone site.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 70m,
            IsRequiredForModulePass = true,
            DueDate = seedTime.AddDays(35),
            AvailableFrom = availabilityFrom,
            AvailableUntil = availabilityUntil,
            MaxAttempts = 2,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var milestoneWireframe = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = "RML-WEBDEV-03-01",
            ModuleId = moduleWebDev3.Id,
            Title = "Responsive Planning",
            Description = "Plan responsive layouts and document deployment approach.",
            MilestoneOrder = 1,
            IsCapstone = false,
            AssignmentId = assignmentWireframe.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        var milestoneCapstone = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = "RML-WEBDEV-03-02",
            ModuleId = moduleWebDev3.Id,
            Title = "Capstone Deployment",
            Description = "Ship and present the final responsive web project.",
            MilestoneOrder = 2,
            IsCapstone = true,
            AssignmentId = assignmentCapstone.Id,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        await _unitOfWork.Assignments.AddRangeAsync(
            new List<Assignment> { assignmentWireframe, assignmentCapstone });
        await _unitOfWork.ResearchMilestones.AddRangeAsync(
            new List<ResearchMilestone> { milestoneWireframe, milestoneCapstone });
        await _unitOfWork.SaveChangesAsync();

        await _unitOfWork.ResearchMilestoneActivities.AddRangeAsync(new List<ResearchMilestoneActivity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestoneWireframe.Id,
                ActivityId = wireframeActivity.Id,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestoneCapstone.Id,
                ActivityId = deploymentActivity.Id,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestoneCapstone.Id,
                ActivityId = capstoneActivity.Id,
                IsRequiredForSubmission = false,
                DisplayOrder = 2,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            }
        });
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed webdev research milestones — 2 milestone(s) and 3 activity link(s) created.");
    }

    private async Task SeedWebDevResearchEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed webdev research enrollments");
        var moduleWebDev3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-03");
        var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
        var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");

        if (moduleWebDev3 == null || student2 == null || programWebDev == null)
        {
            _loggerService.LogWarning("WebDev research enrollment prerequisites not found. Skipping.");
            return;
        }

        var existing = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student2.Id
                  && me.ModuleId == moduleWebDev3.Id
                  && !me.IsDeleted);

        if (existing != null)
        {
            _loggerService.LogInformation("WebDev research module enrollment already exists, skipping");
            return;
        }

        var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student2.Id
                  && pe.ProgramId == programWebDev.Id
                  && !pe.IsDeleted);

        var seedTime = DateTime.UtcNow;
        var moduleEnrollment = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student2.Id,
            ModuleId = moduleWebDev3.Id,
            ProgramEnrollmentId = programEnrollment?.Id,
            Status = EnrollmentStatus.Active,
            ProgressPercent = 5m,
            EnrolledAt = seedTime.AddDays(-3),
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };

        await _unitOfWork.ModuleEnrollments.AddAsync(moduleEnrollment);
        await _unitOfWork.SaveChangesAsync();

        var wireframeActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-01");
        if (wireframeActivity != null)
        {
            await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = student2.Id,
                ActivityId = wireframeActivity.Id,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                CompletedAt = seedTime.AddDays(-1),
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
            await _unitOfWork.SaveChangesAsync();

            var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                _unitOfWork,
                moduleEnrollment);

            if (moduleProgressPercent >= 100m && moduleEnrollment.ProgramEnrollmentId.HasValue)
            {
                await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                    _unitOfWork,
                    moduleEnrollment.ProgramEnrollmentId.Value,
                    moduleEnrollment);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation("Finished seed webdev research enrollment for STD-002.");
    }

    private async Task SeedExtendedResearchSubmissionsAsync()
    {
        _loggerService.LogInformation("Starting seed extended research submissions");

        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
        var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
        if (mentor == null || student1 == null || student2 == null)
        {
            return;
        }

        var seedTime = DateTime.UtcNow;
        var createdCount = 0;

        var gradedSubmission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.Code == "SUB-RML0301A" && !s.IsDeleted);
        if (gradedSubmission != null)
        {
            var existingEvidence = await _unitOfWork.SubmissionEvidences.FirstOrDefaultAsync(
                se => se.SubmissionId == gradedSubmission.Id && !se.IsDeleted);

            if (existingEvidence == null)
            {
                var media = new MediaAsset
                {
                    Id = Guid.NewGuid(),
                    UploaderId = student1.Id,
                    FileUrl = "https://storage.oboxsteam.com/submissions/evidence/robotics-sensor-photo.jpg",
                    FileType = "image/jpeg",
                    UploadedAt = seedTime.AddDays(-6),
                    CreatedAt = seedTime,
                    CreatedBy = student1.Id,
                    IsDeleted = false
                };
                await _unitOfWork.MediaAssets.AddAsync(media);
                await _unitOfWork.SubmissionEvidences.AddAsync(new SubmissionEvidence
                {
                    SubmissionId = gradedSubmission.Id,
                    MediaId = media.Id,
                    CreatedAt = seedTime,
                    CreatedBy = student1.Id,
                    IsDeleted = false
                });
                await _unitOfWork.SaveChangesAsync();
                createdCount++;
            }
        }

        var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
        var milestoneCapstone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-ROBOTICS-03-03" && !rm.IsDeleted);
        var enrollmentStudent1 = moduleRobotics3 == null
            ? null
            : await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student1.Id
                      && me.ModuleId == moduleRobotics3.Id
                      && !me.IsDeleted);

        if (milestoneCapstone != null
            && enrollmentStudent1 != null
            && !await SubmissionCodeExistsAsync("SUB-RML0303A"))
        {
            var assignmentCapstone = await _unitOfWork.Assignments.GetByIdAsync(milestoneCapstone.AssignmentId);
            if (assignmentCapstone != null)
            {
                await _unitOfWork.Submissions.AddAsync(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = "SUB-RML0303A",
                    AssignmentId = assignmentCapstone.Id,
                    StudentId = student1.Id,
                    ModuleEnrollmentId = enrollmentStudent1.Id,
                    ResearchMilestoneId = milestoneCapstone.Id,
                    AttemptNumber = 0,
                    Status = SubmissionStatus.Pending,
                    CreatedAt = seedTime,
                    CreatedBy = mentor.Id,
                    IsDeleted = false
                });
                await _unitOfWork.SaveChangesAsync();
                createdCount++;
            }
        }

        var moduleWebDev3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-03");
        var milestoneWebDev1 = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-WEBDEV-03-01" && !rm.IsDeleted);
        var enrollmentStudent2WebDev = moduleWebDev3 == null
            ? null
            : await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student2.Id
                      && me.ModuleId == moduleWebDev3.Id
                      && !me.IsDeleted);

        if (milestoneWebDev1 != null
            && enrollmentStudent2WebDev != null
            && !await SubmissionCodeExistsAsync("SUB-WDV0301B"))
        {
            var assignmentWebDev = await _unitOfWork.Assignments.GetByIdAsync(milestoneWebDev1.AssignmentId);
            if (assignmentWebDev != null)
            {
                await _unitOfWork.Submissions.AddAsync(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = "SUB-WDV0301B",
                    AssignmentId = assignmentWebDev.Id,
                    StudentId = student2.Id,
                    ModuleEnrollmentId = enrollmentStudent2WebDev.Id,
                    ResearchMilestoneId = milestoneWebDev1.Id,
                    AttemptNumber = 1,
                    Status = SubmissionStatus.TurnedIn,
                    ContentText = "Wireframes for landing page across mobile and desktop breakpoints.",
                    FileUrl = "https://storage.oboxsteam.com/submissions/webdev-wireframes-std002.pdf",
                    SubmittedAt = seedTime.AddDays(-2),
                    CreatedAt = seedTime.AddDays(-4),
                    CreatedBy = mentor.Id,
                    IsDeleted = false
                });
                await _unitOfWork.SaveChangesAsync();
                createdCount++;
            }
        }

        _loggerService.LogInformation(
            "Finished seed extended research submissions — {Count} update(s)/record(s).",
            createdCount);
    }
}

