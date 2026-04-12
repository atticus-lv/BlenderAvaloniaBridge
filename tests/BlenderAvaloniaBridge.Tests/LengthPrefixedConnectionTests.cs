using System.Buffers.Binary;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Transport;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class LengthPrefixedConnectionTests
{
    [Fact]
    public async Task WriteAsync_UsesExpectedWireFormat()
    {
        await using var stream = new MemoryStream();
        var connection = new LengthPrefixedConnection(stream);
        var packet = ProtocolPacket.CreateControl(
            header: new ProtocolEnvelope
            {
                Type = "init",
                Seq = 1,
                Width = 800,
                Height = 600,
                PixelFormat = "bgra8",
                Stride = 3200,
                Message = "hello"
            });

        await connection.WriteAsync(packet, CancellationToken.None);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 8);

        var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4));
        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));

        Assert.Equal(0u, payloadLength);
        Assert.Equal((uint)(bytes.Length - 8), headerLength);
    }

    [Fact]
    public async Task ReadAsync_RoundTripsHeaderAndPayload()
    {
        await using var a = new MemoryStream();
        var writer = new LengthPrefixedConnection(a);
        var expected = ProtocolPacket.CreateFrame(
            new ProtocolEnvelope
            {
                Type = "frame",
                Seq = 3,
                Width = 2,
                Height = 1,
                PixelFormat = "bgra8",
                Stride = 8
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        await writer.WriteAsync(expected, CancellationToken.None);
        a.Position = 0;

        var reader = new LengthPrefixedConnection(a);
        var actual = await reader.ReadAsync(CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal("frame", actual!.Header.Type);
        Assert.Equal(3, actual.Header.Seq);
        Assert.Equal(2, actual.Header.Width);
        Assert.Equal(8, actual.Payload.Length);
        Assert.Equal(expected.Payload, actual.Payload);
    }
}
