using OboxSteam.Application.Commons;

namespace OboxSteam.Test.UnitTests;

public sealed class ModuleAbsencePolicyTests
{
    [Theory]
    [InlineData(0, 5, false)]
    [InlineData(1, 6, false)]
    [InlineData(1, 5, true)]
    [InlineData(2, 5, true)]
    [InlineData(0, 0, false)]
    public void ShouldFail_UsesTwentyPercentThreshold(int missed, int total, bool expected)
    {
        Assert.Equal(expected, ModuleAbsencePolicy.ShouldFail(missed, total));
    }
}
