using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class MentorCompleteValidatorTests
{
    [Fact]
    public void EnsureMediaEvidencePresent_Throws_WhenRequiredWithoutEvidence()
    {
        var activity = new Activity
        {
            Code = "ACT-001",
            Name = "Field trip",
            ActivityType = ActivityType.Offline,
            RequireMediaEvidence = true,
        };

        var ex = Assert.Throws<OboxSteam.Application.Exceptions.BadRequestException>(
            () => MentorCompleteValidator.EnsureMediaEvidencePresent(activity, false));

        Assert.Equal(MentorCompleteValidator.MediaEvidenceRequiredMessage, ex.Message);
    }
}
