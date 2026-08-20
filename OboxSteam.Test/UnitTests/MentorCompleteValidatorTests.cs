using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class MentorCompleteValidatorTests
{
    private static readonly Guid StudentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");

    private static Activity QrActivity(bool requireQrCheckin) => new()
    {
        Code = "ACT-001",
        Name = "Field trip",
        ActivityType = ActivityType.Offline,
        RequireQrCheckin = requireQrCheckin,
    };

    [Fact]
    public void GetQrCheckinSkipReason_ReturnsNull_WhenActivityDoesNotRequireQr()
    {
        Assert.Null(MentorCompleteValidator.GetQrCheckinSkipReason(QrActivity(false), null));
    }

    [Fact]
    public void GetQrCheckinSkipReason_ReturnsReason_WhenNoAttendance()
    {
        var reason = MentorCompleteValidator.GetQrCheckinSkipReason(QrActivity(true), null);

        Assert.Equal(MentorCompleteValidator.QrCheckinRequiredMessage, reason);
    }

    [Fact]
    public void GetQrCheckinSkipReason_ReturnsReason_WhenAttendanceMarkedByMentor()
    {
        var attendance = new SessionAttendance
        {
            StudentId = StudentId,
            Status = AttendanceStatus.Present,
            CheckedInAt = DateTime.UtcNow,
            RecordedBy = MentorId,
        };

        var reason = MentorCompleteValidator.GetQrCheckinSkipReason(QrActivity(true), attendance);

        Assert.Equal(MentorCompleteValidator.QrCheckinRequiredMessage, reason);
    }

    [Fact]
    public void GetQrCheckinSkipReason_ReturnsReason_WhenSelfRecordedWithoutCheckInTime()
    {
        var attendance = new SessionAttendance
        {
            StudentId = StudentId,
            Status = AttendanceStatus.Present,
            CheckedInAt = null,
            RecordedBy = StudentId,
        };

        var reason = MentorCompleteValidator.GetQrCheckinSkipReason(QrActivity(true), attendance);

        Assert.Equal(MentorCompleteValidator.QrCheckinRequiredMessage, reason);
    }

    [Fact]
    public void GetQrCheckinSkipReason_ReturnsNull_WhenStudentCheckedInThemselves()
    {
        var attendance = new SessionAttendance
        {
            StudentId = StudentId,
            Status = AttendanceStatus.Present,
            CheckedInAt = DateTime.UtcNow,
            RecordedBy = StudentId,
        };

        Assert.Null(MentorCompleteValidator.GetQrCheckinSkipReason(QrActivity(true), attendance));
    }
}
