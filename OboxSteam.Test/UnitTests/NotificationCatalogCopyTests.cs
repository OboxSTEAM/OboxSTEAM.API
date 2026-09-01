using System.Reflection;
using System.Text.RegularExpressions;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class NotificationCatalogCopyTests
{
    private static readonly Guid SampleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SampleId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Regex EnglishUserCopyPattern = new(
        @"\b(You |Your |has been |successfully|Please complete|Complete your)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IEnumerable<object[]> CatalogFactoryMethods()
        => typeof(NotificationCatalog)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(NotificationCommand) && m.Name != nameof(NotificationCatalog.AttendanceMarked))
            .Select(m => new object[] { m.Name, m });

    [Theory]
    [MemberData(nameof(CatalogFactoryMethods))]
    public void CatalogFactory_ProducesNonEmptyTitle(string name, MethodInfo method)
    {
        var command = InvokeFactory(method);

        Assert.False(string.IsNullOrWhiteSpace(command.Title), $"{name} title is empty.");
        Assert.False(string.IsNullOrWhiteSpace(command.Templates.Default.Title), $"{name} default template title is empty.");
    }

    [Theory]
    [MemberData(nameof(CatalogFactoryMethods))]
    public void CatalogFactory_AvoidsEnglishUserCopy(string name, MethodInfo method)
    {
        var command = InvokeFactory(method);
        var texts = CollectCopy(command);

        foreach (var (role, title, body) in texts)
        {
            Assert.False(
                EnglishUserCopyPattern.IsMatch(title),
                $"{name} {role} title contains English user copy: {title}");
            if (body is not null)
            {
                Assert.False(
                    EnglishUserCopyPattern.IsMatch(body),
                    $"{name} {role} body contains English user copy: {body}");
            }
        }
    }

    [Fact]
    public void CurriculumReviewChangesRequested_IncludesProgramDeeplinkAndExpertComment()
    {
        var command = NotificationCatalog.CurriculumReviewChangesRequested(
            SampleId,
            "Thiếu buổi Offline.",
            SampleId2,
            SampleId,
            "Robotics",
            "TS. Lan");

        Assert.Equal(NotificationType.CurriculumReviewChangesRequested, command.Type);
        Assert.Equal(NotificationAudienceKind.Managers, command.Audience.Kind);
        Assert.Equal(SampleId, command.Payload!.ProgramId);
        Assert.Equal("Thiếu buổi Offline.", command.Payload.Extra);
        Assert.Contains("{comment}", command.Templates.Default.Body!, StringComparison.Ordinal);
        Assert.Contains("không duyệt chương trình", command.Templates.Default.Body!, StringComparison.Ordinal);
        Assert.Contains("Thiếu buổi Offline.", command.Body!);
        Assert.Contains("Robotics", command.Body!);
    }

    [Fact]
    public void CurriculumReviewSubmitted_TargetsExpertUserWithFrameworkCopy()
    {
        var command = NotificationCatalog.CurriculumReviewSubmitted(
            SampleId,
            SampleId2,
            SampleId,
            "Robotics",
            "Robotics blueprint",
            "USR-MGR");

        Assert.Equal(NotificationType.CurriculumReviewSubmitted, command.Type);
        Assert.Equal(NotificationAudienceKind.User, command.Audience.Kind);
        Assert.Equal(SampleId, command.Audience.UserId);
        Assert.Equal(SampleId2, command.Payload!.ProgramId);
        Assert.Contains("{frameworkName}", command.Templates.Default.Body!, StringComparison.Ordinal);
        Assert.Contains("Robotics blueprint", command.Body!);
    }

    [Theory]
    [MemberData(nameof(CatalogFactoryMethods))]
    public void CatalogFactory_StudentParentVariants_FollowAddressingRules(string name, MethodInfo method)
    {
        var command = InvokeFactory(method);
        var student = command.Templates.Student;
        var parent = command.Templates.Parent;

        if (student is null || parent is null || ReferenceEquals(student, parent))
        {
            return;
        }

        Assert.True(
            parent.Body!.Contains("con bạn", StringComparison.Ordinal)
            || parent.Body.Contains("{studentName}", StringComparison.Ordinal),
            $"{name} parent copy should address the child: {parent.Body}");

        if (student.Body!.Contains("con bạn", StringComparison.Ordinal))
        {
            Assert.Contains("{studentName}", student.Body, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(CatalogFactoryMethods))]
    public void CatalogFactory_StudentScopedAudience_SetsPayloadStudentId(string name, MethodInfo method)
    {
        var command = InvokeFactory(method);

        if (command.Audience.Kind is not (
            NotificationAudienceKind.StudentAndParents
            or NotificationAudienceKind.ParentsOfStudent))
        {
            return;
        }

        Assert.NotNull(command.Payload);
        Assert.True(
            command.Payload!.StudentId is Guid studentId && studentId != Guid.Empty,
            $"{name} should set payload.StudentId for student-scoped audience.");
    }

    [Theory]
    [InlineData(AttendanceStatus.Present)]
    [InlineData(AttendanceStatus.Late)]
    [InlineData(AttendanceStatus.Absent)]
    [InlineData(AttendanceStatus.Excused)]
    [InlineData(AttendanceStatus.Expected)]
    public void AttendanceMarked_AllStatuses_ProduceVietnameseCopyWithActorToken(AttendanceStatus status)
    {
        var command = NotificationCatalog.AttendanceMarked(
            status,
            SampleId,
            SampleId2,
            SampleId,
            SampleId2);

        Assert.False(string.IsNullOrWhiteSpace(command.Title));
        Assert.Contains("{actorName}", command.Templates.Student!.Body!, StringComparison.Ordinal);
        Assert.Contains("{actorName}", command.Templates.Parent!.Body!, StringComparison.Ordinal);
        Assert.DoesNotMatch(EnglishUserCopyPattern, command.Templates.Student.Body!);
        Assert.DoesNotMatch(EnglishUserCopyPattern, command.Templates.Parent.Body!);
        Assert.Equal(SampleId, command.Payload!.StudentId);
    }

    [Fact]
    public void CatalogFactory_Count_MatchesNotificationSurfaceArea()
    {
        var count = typeof(NotificationCatalog)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Count(m => m.ReturnType == typeof(NotificationCommand));

        Assert.Equal(70, count);
    }

    private static NotificationCommand InvokeFactory(MethodInfo method)
    {
        var args = method.GetParameters()
            .Select(CreateArgument)
            .ToArray();

        return (NotificationCommand)method.Invoke(null, args)!;
    }

    private static object? CreateArgument(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        if (parameter.ParameterType == typeof(Guid))
        {
            return SampleId;
        }

        if (parameter.ParameterType == typeof(Guid?))
        {
            return SampleId;
        }

        if (parameter.ParameterType == typeof(string))
        {
            return "Sample";
        }

        if (parameter.ParameterType == typeof(AttendanceStatus))
        {
            return AttendanceStatus.Present;
        }

        if (parameter.ParameterType == typeof(int))
        {
            return 1;
        }

        if (parameter.ParameterType == typeof(bool))
        {
            return true;
        }

        if (parameter.ParameterType.IsEnum)
        {
            return Enum.GetValues(parameter.ParameterType).GetValue(0)!;
        }

        throw new NotSupportedException($"No default argument for {parameter.Name} ({parameter.ParameterType.Name}).");
    }

    private static IEnumerable<(string Role, string Title, string? Body)> CollectCopy(NotificationCommand command)
    {
        yield return ("Default", command.Templates.Default.Title, command.Templates.Default.Body);

        if (command.Templates.Student is { } student)
        {
            yield return ("Student", student.Title, student.Body);
        }

        if (command.Templates.Parent is { } parent)
        {
            yield return ("Parent", parent.Title, parent.Body);
        }

        if (command.Templates.Mentor is { } mentor)
        {
            yield return ("Mentor", mentor.Title, mentor.Body);
        }

        if (command.Templates.Manager is { } manager)
        {
            yield return ("Manager", manager.Title, manager.Body);
        }
    }
}
