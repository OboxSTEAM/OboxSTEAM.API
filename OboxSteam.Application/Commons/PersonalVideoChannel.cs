using System.Threading.Channels;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Unbounded channel that carries HighlightVideo IDs for asynchronous personal video generation.
/// Produced by PersonalVideoService; consumed by PersonalVideoWorker.
/// </summary>
public class PersonalVideoChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ChannelWriter<Guid> Writer => _channel.Writer;
    public ChannelReader<Guid> Reader => _channel.Reader;
}
