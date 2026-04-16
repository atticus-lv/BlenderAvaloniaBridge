using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using BlenderAvaloniaBridge.Runtime;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BridgeClientTests
{
    [Fact]
    public void IsTransportDisconnect_ReturnsTrueForConnectionResetIOException()
    {
        var exception = new IOException(
            "socket closed",
            new SocketException((int)SocketError.ConnectionReset));

        Assert.True(BridgeClient.IsTransportDisconnect(exception));
    }

    [Fact]
    public void IsTransportDisconnect_ReturnsTrueForEndOfStream()
    {
        Assert.True(BridgeClient.IsTransportDisconnect(new EndOfStreamException()));
    }

    [Fact]
    public void IsTransportDisconnect_ReturnsFalseForUnexpectedFailure()
    {
        Assert.False(BridgeClient.IsTransportDisconnect(new InvalidOperationException("boom")));
    }

    [Fact]
    public void IsRecoverableFrameFailure_ReturnsTrueForHeadlessFrameMiss()
    {
        Assert.True(BridgeClient.IsRecoverableFrameFailure(
            new InvalidOperationException("Headless renderer did not produce a frame.")));
    }

    [Fact]
    public void IsRecoverableFrameFailure_ReturnsFalseForUnexpectedFailure()
    {
        Assert.False(BridgeClient.IsRecoverableFrameFailure(new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task LatestWinsSignal_CoalescesConcurrentSignalsIntoASingleWake()
    {
        var signal = new LatestWinsSignal();

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(signal.Signal)));

        await signal.WaitAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await signal.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task LatestWinsSignal_AllowsAnotherWakeAfterConsumption()
    {
        var signal = new LatestWinsSignal();

        Assert.True(signal.Signal());
        await signal.WaitAsync(CancellationToken.None);

        Assert.True(signal.Signal());
        await signal.WaitAsync(CancellationToken.None);
    }
}
