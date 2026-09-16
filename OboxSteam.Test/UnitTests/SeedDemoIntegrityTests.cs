using OboxSteam.Application.Services;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class SeedDemoIntegrityTests
{
    [Fact]
    public void HasRequiredCurrentClassLiveMix_RequiresCompletedAndUpcoming()
    {
        Assert.False(SeedService.HasRequiredCurrentClassLiveMix([]));
        Assert.False(SeedService.HasRequiredCurrentClassLiveMix(
        [
            (ClassSessionStatus.Completed, SessionKind.LiveOnline),
        ]));
        Assert.True(SeedService.HasRequiredCurrentClassLiveMix(
        [
            (ClassSessionStatus.Completed, SessionKind.LiveOnline),
            (ClassSessionStatus.Scheduled, SessionKind.Offline),
        ]));
        Assert.True(SeedService.HasRequiredCurrentClassLiveMix(
        [
            (ClassSessionStatus.Completed, SessionKind.Offline),
            (ClassSessionStatus.InProgress, SessionKind.LiveOnline),
        ]));
        Assert.False(SeedService.HasRequiredCurrentClassLiveMix(
        [
            (ClassSessionStatus.Completed, SessionKind.AssignmentWindow),
            (ClassSessionStatus.Scheduled, SessionKind.AssignmentWindow),
        ]));
    }

    [Fact]
    public void ResolveCurrentMonday_UsesSeedNowNotWallClock()
    {
        var vietnam = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        // Wednesday 2026-09-16 12:00 UTC → VN Thursday morning; Monday is 2026-09-14.
        var seedNow = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        var monday = SeedService.ResolveCurrentMonday(vietnam, seedNow);
        Assert.Equal(new DateOnly(2026, 9, 14), monday);
    }

    [Fact]
    public void ResolveExpertLoginUserCode_MapsProfileCodeToLoginUserCode()
    {
        Assert.Equal("EXP-U001", SeedService.ResolveExpertLoginUserCode("EXP-001"));
        Assert.Equal("EXP-U002", SeedService.ResolveExpertLoginUserCode("EXP-002"));
        Assert.Null(SeedService.ResolveExpertLoginUserCode("EXP-999"));
    }

    [Fact]
    public void HeroExpertAccounts_UseDistinctProfileAndLoginCodes()
    {
        foreach (var (expertCode, userCode) in SeedService.HeroExpertAccounts)
        {
            Assert.StartsWith("EXP-00", expertCode);
            Assert.StartsWith("EXP-U", userCode);
            Assert.NotEqual(expertCode, userCode);
        }
    }
}
