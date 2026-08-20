using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassSessionValidatorTests
{
    [Fact]
    public void ValidateCoordinates_AllowsMissingPair()
    {
        ClassSessionValidator.ValidateCoordinates(null, null);
    }

    [Fact]
    public void ValidateCoordinates_AllowsValidPair()
    {
        ClassSessionValidator.ValidateCoordinates(10.762622, 106.660172);
    }

    [Theory]
    [InlineData(10.0, null)]
    [InlineData(null, 106.0)]
    public void ValidateCoordinates_RejectsHalfPair(double? latitude, double? longitude)
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            ClassSessionValidator.ValidateCoordinates(latitude, longitude));

        Assert.Equal("Latitude and Longitude must be provided together.", ex.Message);
    }

    [Theory]
    [InlineData(-90.1, 0.0)]
    [InlineData(90.1, 0.0)]
    [InlineData(0.0, -180.1)]
    [InlineData(0.0, 180.1)]
    public void ValidateCoordinates_RejectsOutOfRange(double latitude, double longitude)
    {
        Assert.Throws<BadRequestException>(() =>
            ClassSessionValidator.ValidateCoordinates(latitude, longitude));
    }

    [Fact]
    public void ValidateGenerateRequest_RejectsEmptyDaysOfWeek()
    {
        var request = new GenerateClassSessionsRequestDto
        {
            DaysOfWeek = new List<DayOfWeek>(),
            SessionStartTime = new TimeOnly(9, 0),
            SessionEndTime = new TimeOnly(11, 0),
        };

        Assert.Throws<BadRequestException>(() => ClassSessionValidator.ValidateGenerateRequest(request));
    }

    [Fact]
    public void ValidateGenerateRequest_RejectsInvalidDayValue()
    {
        var request = new GenerateClassSessionsRequestDto
        {
            DaysOfWeek = new List<DayOfWeek> { (DayOfWeek)99 },
            SessionStartTime = new TimeOnly(9, 0),
            SessionEndTime = new TimeOnly(11, 0),
        };

        Assert.Throws<BadRequestException>(() => ClassSessionValidator.ValidateGenerateRequest(request));
    }

    [Fact]
    public void ValidateGenerateRequest_RejectsEndBeforeStart()
    {
        var request = new GenerateClassSessionsRequestDto
        {
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Saturday },
            SessionStartTime = new TimeOnly(11, 0),
            SessionEndTime = new TimeOnly(9, 0),
        };

        Assert.Throws<BadRequestException>(() => ClassSessionValidator.ValidateGenerateRequest(request));
    }

    [Fact]
    public void ValidateGenerateRequest_AcceptsValidPattern()
    {
        var request = new GenerateClassSessionsRequestDto
        {
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday },
            SessionStartTime = new TimeOnly(9, 0),
            SessionEndTime = new TimeOnly(11, 0),
        };

        ClassSessionValidator.ValidateGenerateRequest(request);
    }
}
