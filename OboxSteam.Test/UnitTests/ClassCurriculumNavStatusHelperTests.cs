using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassCurriculumNavStatusHelperTests
{
    private static readonly Guid Activity1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid Activity2 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    private static readonly Guid Activity3 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
    private static readonly Guid Session1 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
    private static readonly Guid Session2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private static ClassSession Session(Guid id, ClassSessionStatus status, DateTime? start = null)
        => new()
        {
            Id = id,
            Status = status,
            StartTime = start ?? DateTime.UtcNow,
            EndTime = (start ?? DateTime.UtcNow).AddHours(1),
            IsDeleted = false,
        };

    [Fact]
    public void LiveSessionCompleted_IsCompleted_EvenIfStudentsAbsent()
    {
        var inputs = new[]
        {
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity1,
                ActivityType.LiveOnline,
                CompletedCount: 8,
                Session(Session1, ClassSessionStatus.Completed)),
        };

        var (byId, currentId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(inputs, totalStudents: 12);

        Assert.Equal(CurriculumStatusHelper.StatusCompleted, byId[Activity1].Status);
        Assert.Equal(Session1, byId[Activity1].ClassSessionId);
        Assert.Equal(ClassSessionStatus.Completed, byId[Activity1].SessionStatus);
        Assert.Null(currentId);
    }

    [Fact]
    public void LiveSessionInProgress_IsCurrent()
    {
        var inputs = new[]
        {
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity1,
                ActivityType.LiveOnline,
                CompletedCount: 0,
                Session(Session1, ClassSessionStatus.InProgress)),
        };

        var (byId, currentId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(inputs, totalStudents: 10);

        Assert.Equal(CurriculumStatusHelper.StatusCurrent, byId[Activity1].Status);
        Assert.Equal(Activity1, currentId);
    }

    [Fact]
    public void SelfPacedPartial_NoLaterStarted_IsCurrent()
    {
        var inputs = new[]
        {
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity1,
                ActivityType.SelfPaced,
                CompletedCount: 3,
                PrimarySession: null),
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity2,
                ActivityType.SelfPaced,
                CompletedCount: 0,
                PrimarySession: null),
        };

        var (byId, currentId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(inputs, totalStudents: 10);

        Assert.Equal(CurriculumStatusHelper.StatusCurrent, byId[Activity1].Status);
        Assert.Equal(CurriculumStatusHelper.StatusAvailable, byId[Activity2].Status);
        Assert.Equal(Activity1, currentId);
    }

    [Fact]
    public void SelfPacedPartial_LaterLiveCurrent_BecomesCompleted()
    {
        var inputs = new[]
        {
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity1,
                ActivityType.SelfPaced,
                CompletedCount: 3,
                PrimarySession: null),
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity2,
                ActivityType.LiveOnline,
                CompletedCount: 0,
                Session(Session1, ClassSessionStatus.InProgress)),
        };

        var (byId, currentId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(inputs, totalStudents: 10);

        Assert.Equal(CurriculumStatusHelper.StatusCompleted, byId[Activity1].Status);
        Assert.Equal(CurriculumStatusHelper.StatusCurrent, byId[Activity2].Status);
        Assert.Equal(Activity2, currentId);
    }

    [Fact]
    public void SelfPacedPartial_LaterLiveCompleted_BecomesCompleted_AndNextIsCurrent()
    {
        var inputs = new[]
        {
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity1,
                ActivityType.SelfPaced,
                CompletedCount: 3,
                PrimarySession: null),
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity2,
                ActivityType.LiveOnline,
                CompletedCount: 0,
                Session(Session1, ClassSessionStatus.Completed)),
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity3,
                ActivityType.SelfPaced,
                CompletedCount: 0,
                PrimarySession: null),
        };

        var (byId, currentId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(inputs, totalStudents: 10);

        Assert.Equal(CurriculumStatusHelper.StatusCompleted, byId[Activity1].Status);
        Assert.Equal(CurriculumStatusHelper.StatusCompleted, byId[Activity2].Status);
        Assert.Equal(CurriculumStatusHelper.StatusCurrent, byId[Activity3].Status);
        Assert.Equal(Activity3, currentId);
    }

    [Fact]
    public void SelfPacedAllDone_IsCompleted()
    {
        var inputs = new[]
        {
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity1,
                ActivityType.SelfPaced,
                CompletedCount: 10,
                PrimarySession: null),
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity2,
                ActivityType.SelfPaced,
                CompletedCount: 0,
                PrimarySession: null),
        };

        var (byId, currentId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(inputs, totalStudents: 10);

        Assert.Equal(CurriculumStatusHelper.StatusCompleted, byId[Activity1].Status);
        Assert.Equal(CurriculumStatusHelper.StatusCurrent, byId[Activity2].Status);
        Assert.Equal(Activity2, currentId);
    }

    [Fact]
    public void LiveFullRosterDone_WithoutSessionCompleted_IsCompleted()
    {
        var inputs = new[]
        {
            new ClassCurriculumNavStatusHelper.ActivityNavInput(
                Activity1,
                ActivityType.Offline,
                CompletedCount: 5,
                Session(Session1, ClassSessionStatus.InProgress)),
        };

        var (byId, currentId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(inputs, totalStudents: 5);

        Assert.Equal(CurriculumStatusHelper.StatusCompleted, byId[Activity1].Status);
        Assert.Null(currentId);
    }

    [Fact]
    public void AssignmentStatus_AllGraded_Completed_SomePending_Submitted_ElseAvailable()
    {
        Assert.Equal(
            CurriculumStatusHelper.StatusCompleted,
            ClassCurriculumNavStatusHelper.ResolveAssignmentStatus(3, submittedCount: 3, gradedCount: 3));
        Assert.Equal(
            CurriculumStatusHelper.StatusSubmitted,
            ClassCurriculumNavStatusHelper.ResolveAssignmentStatus(3, submittedCount: 2, gradedCount: 1));
        Assert.Equal(
            CurriculumStatusHelper.StatusAvailable,
            ClassCurriculumNavStatusHelper.ResolveAssignmentStatus(3, submittedCount: 0, gradedCount: 0));
        Assert.Equal(
            CurriculumStatusHelper.StatusAvailable,
            ClassCurriculumNavStatusHelper.ResolveAssignmentStatus(0, submittedCount: 0, gradedCount: 0));
    }

    [Fact]
    public void SelectPrimarySession_PrefersCompletedThenInProgressThenEarliestScheduled()
    {
        var scheduledEarly = Session(Session1, ClassSessionStatus.Scheduled, DateTime.UtcNow.AddDays(1));
        var scheduledLate = Session(Session2, ClassSessionStatus.Scheduled, DateTime.UtcNow.AddDays(3));
        var inProgress = Session(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3"), ClassSessionStatus.InProgress);
        var completed = Session(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4"), ClassSessionStatus.Completed);

        Assert.Equal(completed.Id, ClassCurriculumNavStatusHelper.SelectPrimarySession(
            [scheduledEarly, completed, inProgress, scheduledLate])!.Id);
        Assert.Equal(inProgress.Id, ClassCurriculumNavStatusHelper.SelectPrimarySession(
            [scheduledEarly, inProgress, scheduledLate])!.Id);
        Assert.Equal(scheduledEarly.Id, ClassCurriculumNavStatusHelper.SelectPrimarySession(
            [scheduledLate, scheduledEarly])!.Id);
    }
}
