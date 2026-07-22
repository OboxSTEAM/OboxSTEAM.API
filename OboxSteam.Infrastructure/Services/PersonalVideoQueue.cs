using System.Threading.Channels;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Unbounded in-memory implementation of <see cref="IPersonalVideoQueue"/> backed by a
/// <see cref="Channel{T}"/>. Registered as a singleton so the trigger (scoped) and the
/// background worker (singleton) share the same channel.
///
/// Note: jobs live only in process memory — if the app restarts while a job is queued or
/// mid-flight, that job is lost and the HighlightVideoItem stays in <c>Processing</c> until the
/// stale-threshold guard allows a re-trigger. This is acceptable for the current scale; a
/// durable queue (e.g. SQS) would be the next step if stronger guarantees are needed.
/// </summary>
public class PersonalVideoQueue : IPersonalVideoQueue
{
    private readonly Channel<PersonalVideoJob> _channel =
        Channel.CreateUnbounded<PersonalVideoJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(PersonalVideoJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        _channel.Writer.TryWrite(job);
    }

    public ValueTask<PersonalVideoJob> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
