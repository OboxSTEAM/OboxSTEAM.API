using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassSessionCheckInValidatorTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Token = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static ClassSession OpenSession() => new()
    {
        Title = "Field trip",
        Status = ClassSessionStatus.InProgress,
        StartTime = Now.AddHours(-1),
        EndTime = Now.AddHours(1),
        CheckInToken = Token,
        CheckInCode = "123456",
        CheckInTokenExpiresAt = Now.AddSeconds(30),
    };

    [Theory]
    [InlineData(ClassSessionStatus.Scheduled)]
    [InlineData(ClassSessionStatus.InProgress)]
    public void ValidateSessionOpenForCheckIn_AllowsScheduledAndInProgress(ClassSessionStatus status)
    {
        var session = OpenSession();
        session.Status = status;

        ClassSessionCheckInValidator.ValidateSessionOpenForCheckIn(session);
    }

    [Theory]
    [InlineData(ClassSessionStatus.Completed)]
    [InlineData(ClassSessionStatus.Cancelled)]
    public void ValidateSessionOpenForCheckIn_RejectsClosedSessions(ClassSessionStatus status)
    {
        var session = OpenSession();
        session.Status = status;

        var ex = Assert.Throws<BadRequestException>(() =>
            ClassSessionCheckInValidator.ValidateSessionOpenForCheckIn(session));

        Assert.Equal(ClassSessionCheckInValidator.SessionNotOpenMessage, ex.Message);
    }

    [Fact]
    public void ValidateTokenOrCode_RejectsSessionWithoutActiveToken()
    {
        var session = OpenSession();
        session.CheckInToken = null;
        session.CheckInCode = null;
        session.CheckInTokenExpiresAt = null;

        var ex = Assert.Throws<BadRequestException>(() =>
            ClassSessionCheckInValidator.ValidateTokenOrCode(session, Token, "123456", Now));

        Assert.Equal(ClassSessionCheckInValidator.NoActiveTokenMessage, ex.Message);
    }

    [Fact]
    public void ValidateTokenOrCode_RejectsExpiredToken()
    {
        var session = OpenSession();
        session.CheckInTokenExpiresAt = Now.AddSeconds(-1);

        var ex = Assert.Throws<BadRequestException>(() =>
            ClassSessionCheckInValidator.ValidateTokenOrCode(session, Token, "123456", Now));

        Assert.Equal(ClassSessionCheckInValidator.TokenExpiredMessage, ex.Message);
    }

    [Fact]
    public void ValidateTokenOrCode_RejectsWrongTokenAndCode()
    {
        var session = OpenSession();

        var ex = Assert.Throws<BadRequestException>(() =>
            ClassSessionCheckInValidator.ValidateTokenOrCode(session, Guid.NewGuid(), "000000", Now));

        Assert.Equal(ClassSessionCheckInValidator.TokenInvalidMessage, ex.Message);
    }

    [Fact]
    public void ValidateTokenOrCode_RejectsWhenNeitherProvided()
    {
        var session = OpenSession();

        Assert.Throws<BadRequestException>(() =>
            ClassSessionCheckInValidator.ValidateTokenOrCode(session, null, null, Now));
    }

    [Fact]
    public void ValidateTokenOrCode_AcceptsMatchingToken()
    {
        ClassSessionCheckInValidator.ValidateTokenOrCode(OpenSession(), Token, null, Now);
    }

    [Fact]
    public void ValidateTokenOrCode_AcceptsMatchingCodeWithWhitespace()
    {
        ClassSessionCheckInValidator.ValidateTokenOrCode(OpenSession(), null, " 123456 ", Now);
    }
}
