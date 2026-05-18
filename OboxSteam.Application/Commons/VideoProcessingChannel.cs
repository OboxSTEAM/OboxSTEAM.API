using System.Threading.Channels;

namespace OboxSteam.Application.Commons;

public class VideoProcessingChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ChannelWriter<Guid> Writer => _channel.Writer;
    public ChannelReader<Guid> Reader => _channel.Reader;
}
