using OboxSteam.Infrastructure.Observability;
using System.Diagnostics;

namespace OboxSteam.Test.UnitTests;

public sealed class TelemetrySourcesTests : IDisposable
{
    private readonly List<Activity> _stopped = [];
    private readonly ActivityListener _listener;

    public TelemetrySourcesTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetrySources.BACKGROUND_JOBS_SOURCE,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_stopped)
                {
                    _stopped.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task RunBackgroundJob_EmitsOneConsumerSpanNamedAfterTheTask()
    {
        var taskName = $"test-job-{Guid.NewGuid():N}";

        await TelemetrySources.RunBackgroundJobAsync(taskName, activity =>
        {
            activity?.SetTag("job.items", 3);
            return Task.CompletedTask;
        });

        var span = Assert.Single(StoppedSpans(taskName));
        Assert.Equal(ActivityKind.Consumer, span.Kind);
        Assert.Equal(3, span.GetTagItem("job.items"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Empty(span.Events);
    }

    [Fact]
    public async Task RunBackgroundJob_Failure_RecordsExceptionEventAndRethrows()
    {
        var taskName = $"test-job-{Guid.NewGuid():N}";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TelemetrySources.RunBackgroundJobAsync(taskName, _ => throw new InvalidOperationException("boom")));

        var span = Assert.Single(StoppedSpans(taskName));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        var exceptionEvent = Assert.Single(span.Events);
        Assert.Equal("exception", exceptionEvent.Name);
        var tags = exceptionEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["exception.type"]);
        Assert.Equal("boom", tags["exception.message"]);
        Assert.Contains("boom", (string)tags["exception.stacktrace"]!);
    }

    [Fact]
    public async Task RunBackgroundJob_Cancellation_IsNotRecordedAsFailure()
    {
        var taskName = $"test-job-{Guid.NewGuid():N}";

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TelemetrySources.RunBackgroundJobAsync(taskName, _ => throw new OperationCanceledException()));

        var span = Assert.Single(StoppedSpans(taskName));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Empty(span.Events);
    }

    [Fact]
    public void RecordException_WithoutActivity_DoesNothing()
    {
        var exception = Record.Exception(() => TelemetrySources.RecordException(null, new InvalidOperationException()));

        Assert.Null(exception);
    }

    private List<Activity> StoppedSpans(string taskName)
    {
        lock (_stopped)
        {
            return _stopped.Where(a => a.OperationName == taskName).ToList();
        }
    }
}
