using System.Net.Sockets;
using System.IO;
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
}
