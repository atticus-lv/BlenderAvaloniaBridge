using System.Threading.Channels;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class LatestWinsSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

    public bool Signal()
    {
        return _channel.Writer.TryWrite(true);
    }

    public ValueTask<bool> WaitAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
