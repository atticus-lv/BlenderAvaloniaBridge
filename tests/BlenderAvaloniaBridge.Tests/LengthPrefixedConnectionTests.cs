using System.Buffers.Binary;
using System.Text.Json;
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

    [Fact]
    public async Task ReadAsync_RejectsOversizedHeaderLength()
    {
        await using var stream = new MemoryStream();
        Span<byte> prefix = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[..4], (uint)(LengthPrefixedConnection.MaxHeaderBytes + 1));
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[4..], 0);
        await stream.WriteAsync(prefix.ToArray());
        stream.Position = 0;

        var connection = new LengthPrefixedConnection(stream);

        await Assert.ThrowsAsync<InvalidDataException>(() => connection.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsOversizedControlPayload()
    {
        var packetBytes = BuildPrefixAndHeaderOnly(
            new ProtocolEnvelope
            {
                Type = "init",
                Seq = 1,
            },
            (uint)(LengthPrefixedConnection.MaxControlPayloadBytes + 1));

        await using var stream = new MemoryStream(packetBytes);
        var connection = new LengthPrefixedConnection(stream);

        await Assert.ThrowsAsync<InvalidDataException>(() => connection.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsFrameReadyPayload()
    {
        var packetBytes = BuildPrefixAndHeaderOnly(
            new ProtocolEnvelope
            {
                Type = "frame_ready",
                Seq = 1,
                Width = 64,
                Height = 64,
                Stride = 256,
            },
            payloadLength: 4);

        await using var stream = new MemoryStream(packetBytes);
        var connection = new LengthPrefixedConnection(stream);

        await Assert.ThrowsAsync<InvalidDataException>(() => connection.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsFramePayloadStrideMismatch()
    {
        var packetBytes = BuildPrefixAndHeaderOnly(
            new ProtocolEnvelope
            {
                Type = "frame",
                Seq = 1,
                Width = 2,
                Height = 1,
                PixelFormat = "bgra8",
                Stride = 8,
            },
            payloadLength: 4);

        await using var stream = new MemoryStream(packetBytes);
        var connection = new LengthPrefixedConnection(stream);

        await Assert.ThrowsAsync<InvalidDataException>(() => connection.ReadAsync(CancellationToken.None));
    }

    private static byte[] BuildPrefixAndHeaderOnly(ProtocolEnvelope header, uint payloadLength)
    {
        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header, ProtocolJsonContext.Default.ProtocolEnvelope);
        var result = new byte[8 + headerBytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), (uint)headerBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), payloadLength);
        headerBytes.CopyTo(result.AsSpan(8));
        return result;
    }
}
