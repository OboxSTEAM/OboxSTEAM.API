using System.Diagnostics;

namespace OboxSteam.Infrastructure.Observability;

/// <summary>
/// Application-owned activity sources. Spans are exported only when the API registers
/// these source names with OpenTelemetry; otherwise <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>
/// returns null and every helper here is a no-op.
/// </summary>
public static class TelemetrySources
{
    public const string BACKGROUND_JOBS_SOURCE = "OboxSteam.BackgroundJobs";
    public const string AI_SOURCE = "OboxSteam.AI";

    public static readonly ActivitySource BackgroundJobs = new(BACKGROUND_JOBS_SOURCE);
    public static readonly ActivitySource Ai = new(AI_SOURCE);

    /// <summary>
    /// Runs one execution of a background job inside a CONSUMER span. Traceway only lists
    /// CONSUMER spans as Tasks and groups them by name, so <paramref name="taskName"/> must be
    /// a stable identifier without ids or timestamps.
    /// </summary>
    public static async Task RunBackgroundJobAsync(string taskName, Func<Activity?, Task> work)
    {
        using var activity = BackgroundJobs.StartActivity(taskName, ActivityKind.Consumer);
        try
        {
            await work(activity);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Runs one tick of a polling job inside a CONSUMER span that is exported only when
    /// <paramref name="work"/> returns <c>true</c> or throws. Idle ticks send nothing, which keeps
    /// minute-level pollers within the Traceway ingest quota. Recording stays off while the work
    /// runs, so child spans (database, HTTP) are never captured for polling jobs.
    /// </summary>
    public static async Task RunPollingJobAsync(string taskName, Func<Activity?, Task<bool>> work)
    {
        using var activity = BackgroundJobs.StartActivity(taskName, ActivityKind.Consumer);
        var sampled = activity?.Recorded ?? false;
        if (activity is not null)
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }

        try
        {
            if (await work(activity) && sampled)
            {
                activity!.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (sampled)
            {
                activity!.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Adds the OpenTelemetry <c>exception</c> event (which Traceway turns into an Issue)
    /// and marks the span as failed.
    /// </summary>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            ["exception.type"] = exception.GetType().FullName,
            ["exception.message"] = exception.Message,
            ["exception.stacktrace"] = exception.ToString(),
        }));
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}
