using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class SeedMentorLoadTests
{
    [Fact]
    public void SeedRoster_StaysWithinDefaultConcurrentClassLimit()
    {
        var usage = SeedService.CountSeedConcurrentMentorUsage();

        Assert.NotEmpty(usage);
        Assert.All(
            usage,
            pair => Assert.True(
                pair.Value <= MentorRequestConstants.DefaultMaxConcurrentClasses,
                $"{pair.Key} concurrent load {pair.Value} exceeds the default cap of {MentorRequestConstants.DefaultMaxConcurrentClasses}."));
        // Maker Open (MNT-006) + current fail/rebuy cohort.
        Assert.Equal(2, usage.GetValueOrDefault("MNT-006"));
        // Three Open rebuy classes (eligible / blocked / fresh).
        Assert.Equal(3, usage.GetValueOrDefault("MNT-007"));
    }

    [Fact]
    public void SeedRoster_IncludesClassesAwaitingMentor()
    {
        var unassignedCodes = SeedService.GetUnassignedAcademicYearClassCodes();

        Assert.Contains("CLS-WEBDEV-OPEN", unassignedCodes);
        Assert.Contains("CLS-IOT-OPEN", unassignedCodes);
        Assert.Contains("CLS-PYBASIC-OPEN", unassignedCodes);
        Assert.Equal(3, unassignedCodes.Count);
    }

    [Fact]
    public void SeedRoster_StudentInProgressPrograms_StayWithinCap()
    {
        var usage = SeedService.CountSeedInProgressProgramEnrollments();

        Assert.NotEmpty(usage);
        Assert.All(
            usage,
            pair => Assert.True(
                pair.Value <= SeedService.MaxInProgressProgramsPerStudent,
                $"{pair.Key} in-progress programs {pair.Value} exceeds cap of {SeedService.MaxInProgressProgramsPerStudent}."));

        // Hero students already on Robotics must not also hold demo programs.
        Assert.Equal(1, usage.GetValueOrDefault("STD-001"));
        Assert.Equal(1, usage.GetValueOrDefault("STD-002"));
        // Robotics Active + CERT-TEST Active.
        Assert.Equal(2, usage.GetValueOrDefault("STD-025"));

        foreach (var code in SeedService.FailRebuyActiveStudentCodes)
        {
            Assert.Equal(1, usage.GetValueOrDefault(code));
        }

        foreach (var code in SeedService.FailRebuyClosedStudentCodes)
        {
            Assert.Equal(0, usage.GetValueOrDefault(code));
        }

        foreach (var code in SeedService.RoboticsReadyToBuyStudentCodes)
        {
            Assert.Equal(0, usage.GetValueOrDefault(code));
        }

        foreach (var code in SeedService.FailRebuyRebuyActiveStudentCodes)
        {
            Assert.Equal(1, usage.GetValueOrDefault(code));
        }
    }

    [Fact]
    public void SeedRoster_StudentActiveClasses_StayWithinCap()
    {
        var usage = SeedService.CountSeedActiveClassEnrollments();

        Assert.NotEmpty(usage);
        Assert.All(
            usage,
            pair => Assert.True(
                pair.Value <= SeedService.MaxActiveClassesPerStudent,
                $"{pair.Key} active classes {pair.Value} exceeds cap of {SeedService.MaxActiveClassesPerStudent}."));

        Assert.Equal(1, usage.GetValueOrDefault("STD-001"));
        Assert.Equal(1, usage.GetValueOrDefault("STD-002"));

        foreach (var code in SeedService.FailRebuyActiveStudentCodes)
        {
            Assert.Equal(1, usage.GetValueOrDefault(code));
        }

        foreach (var code in SeedService.FailRebuyClosedStudentCodes)
        {
            Assert.Equal(0, usage.GetValueOrDefault(code));
        }

        foreach (var code in SeedService.RoboticsReadyToBuyStudentCodes)
        {
            Assert.Equal(0, usage.GetValueOrDefault(code));
        }

        foreach (var code in SeedService.FailRebuyRebuyActiveStudentCodes)
        {
            Assert.Equal(1, usage.GetValueOrDefault(code));
        }

        Assert.Equal(1, usage.GetValueOrDefault("STD-021"));
    }

    [Fact]
    public void SeedRoster_PendingPayment_NotStackedOnTwoInProgressPrograms()
    {
        var usage = SeedService.CountSeedInProgressProgramEnrollments();

        foreach (var pendingCode in new[] { "STD-006", "STD-018" })
        {
            Assert.True(
                usage.GetValueOrDefault(pendingCode) <= SeedService.MaxInProgressProgramsPerStudent,
                $"{pendingCode} has PendingPayment stacked beyond the in-progress cap.");
            Assert.Equal(1, usage.GetValueOrDefault(pendingCode));
        }
    }

    [Fact]
    public void ResolveSessionKind_MapsActivityTypeToMatchingSessionKind()
    {
        Assert.Equal(
            SessionKind.Offline,
            ClassSessionValidator.ResolveSessionKind(
                new OboxSteam.Domain.Entities.Activity { ActivityType = ActivityType.Offline },
                forAssignment: false));
        Assert.Equal(
            SessionKind.LiveOnline,
            ClassSessionValidator.ResolveSessionKind(
                new OboxSteam.Domain.Entities.Activity { ActivityType = ActivityType.LiveOnline },
                forAssignment: false));
    }
}
