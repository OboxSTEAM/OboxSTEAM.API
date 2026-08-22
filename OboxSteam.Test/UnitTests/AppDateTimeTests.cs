using OboxSteam.Application.Utils;

namespace OboxSteam.Test.UnitTests;

public sealed class AppDateTimeTests
{
    [Fact]
    public void TryParseFlexible_LegacyWithSeconds_TreatsAsVietnamWallClock()
    {
        var ok = AppDateTime.TryParseFlexible("22/08/2026 09:00:00", out var result);

        Assert.True(ok);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        // 09:00 Asia/Ho_Chi_Minh == 02:00 UTC
        Assert.Equal(new DateTime(2026, 8, 22, 2, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void TryParseFlexible_LegacyDateOnly_TreatsMidnightVietnamAsUtc()
    {
        var ok = AppDateTime.TryParseFlexible("22/08/2026", out var result);

        Assert.True(ok);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        // 00:00 Asia/Ho_Chi_Minh == 17:00 UTC previous calendar day
        Assert.Equal(new DateTime(2026, 8, 21, 17, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void TryParseFlexible_IsoWithOffset_ConvertsToUtc()
    {
        var ok = AppDateTime.TryParseFlexible("2026-08-22T09:00:00+07:00", out var result);

        Assert.True(ok);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2026, 8, 22, 2, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void TryParseFlexible_IsoZ_KeepsUtcInstant()
    {
        var ok = AppDateTime.TryParseFlexible("2026-08-22T02:00:00Z", out var result);

        Assert.True(ok);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2026, 8, 22, 2, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void TryParseFlexible_IsoWithoutOffset_TreatsAsVietnamWallClock()
    {
        var ok = AppDateTime.TryParseFlexible("2026-08-22T09:00:00", out var result);

        Assert.True(ok);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2026, 8, 22, 2, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void VietnamWallClockToUtc_MatchesSeedSessionConvention()
    {
        var wall = new DateTime(2026, 8, 22, 9, 0, 0);
        var utc = AppDateTime.VietnamWallClockToUtc(wall);

        Assert.Equal(new DateTime(2026, 8, 22, 2, 0, 0, DateTimeKind.Utc), utc);
    }
}
