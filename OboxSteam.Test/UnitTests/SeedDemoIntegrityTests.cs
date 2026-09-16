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
        var monday = InvokeResolveCurrentMonday(vietnam, seedNow);
        Assert.Equal(new DateOnly(2026, 9, 14), monday);
    }

    private static DateOnly InvokeResolveCurrentMonday(TimeZoneInfo vietnam, DateTime seedNowUtc)
    {
        var method = typeof(SeedService).GetMethod(
            "ResolveCurrentMonday",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null, [vietnam, seedNowUtc]);
        Assert.NotNull(result);
        return (DateOnly)result!;
    }
}
