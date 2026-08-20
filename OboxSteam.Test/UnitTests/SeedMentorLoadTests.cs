using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;

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
        Assert.Equal(0, usage.GetValueOrDefault("MNT-007"));
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
}
