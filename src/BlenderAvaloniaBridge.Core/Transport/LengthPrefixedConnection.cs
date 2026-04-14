using System.Buffers.Binary;
using System.Text.Json;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Transport;

public sealed class LengthPrefixedConnection
{
    internal const int MaxHeaderBytes = 64 * 1024;
    internal const int MaxControlPayloadBytes = 1 * 1024 * 1024;
    internal const int MaxFramePayloadBytes = 64 * 1024 * 1024;
    internal const int MaxPayloadBytesHard = MaxFramePayloadBytes;

    private readonly Stream _stream;

    public LengthPrefixedConnection(Stream stream)
    {
        _stream = stream;
    }

    public async Task WriteAsync(ProtocolPacket packet, CancellationToken cancellationToken)
    {
        packet.Header.PayloadLength = packet.Payload.Length;

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(packet.Header, ProtocolJsonContext.Default.ProtocolEnvelope);
        ValidatePrefixLengths((uint)headerBytes.Length, (uint)packet.Payload.Length);
        ValidatePayloadLength(packet.Header, (uint)packet.Payload.Length);

        Span<byte> prefix = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[..4], (uint)headerBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[4..], (uint)packet.Payload.Length);

        await _stream.WriteAsync(prefix.ToArray(), cancellationToken);
        await _stream.WriteAsync(headerBytes, cancellationToken);
        if (packet.Payload.Length > 0)
        {
            await _stream.WriteAsync(packet.Payload, cancellationToken);
        }

        await _stream.FlushAsync(cancellationToken);
    }

    public async Task<ProtocolPacket?> ReadAsync(CancellationToken cancellationToken)
    {
        var prefixBuffer = await ReadExactlyAsync(8, cancellationToken);
        if (prefixBuffer is null)
        {
            return null;
        }

        var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(prefixBuffer.AsSpan(0, 4));
        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(prefixBuffer.AsSpan(4, 4));
        ValidatePrefixLengths(headerLength, payloadLength);

        var headerBytes = await ReadExactlyAsync(checked((int)headerLength), cancellationToken)
            ?? throw new EndOfStreamException("Unexpected end of stream while reading header.");
        var header = JsonSerializer.Deserialize(headerBytes, ProtocolJsonContext.Default.ProtocolEnvelope)
            ?? throw new InvalidDataException("Unable to deserialize protocol header.");
        ValidatePayloadLength(header, payloadLength);

        var payloadBytes = payloadLength == 0
            ? Array.Empty<byte>()
            : await ReadExactlyAsync(checked((int)payloadLength), cancellationToken)
                ?? throw new EndOfStreamException("Unexpected end of stream while reading payload.");

        header.PayloadLength = payloadBytes.Length;
        return new ProtocolPacket(header, payloadBytes);
    }

    private async Task<byte[]?> ReadExactlyAsync(int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var read = 0;

        while (read < length)
        {
            var bytesRead = await _stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (bytesRead == 0)
            {
                return read == 0 ? null : throw new EndOfStreamException("Unexpected end of stream.");
            }

            read += bytesRead;
        }

        return buffer;
    }

    private static void ValidatePrefixLengths(uint headerLength, uint payloadLength)
    {
        if (headerLength == 0 || headerLength > MaxHeaderBytes)
        {
            throw new InvalidDataException($"Header length {headerLength} exceeds protocol limit ({MaxHeaderBytes} bytes).");
        }

        if (payloadLength > MaxPayloadBytesHard)
        {
            throw new InvalidDataException($"Payload length {payloadLength} exceeds protocol hard limit ({MaxPayloadBytesHard} bytes).");
        }
    }

    private static void ValidatePayloadLength(ProtocolEnvelope header, uint payloadLength)
    {
        var type = header.Type ?? string.Empty;
        var payloadLimit = GetPayloadLimit(type);
        if (payloadLength > payloadLimit)
        {
            throw new InvalidDataException(
                $"Payload length {payloadLength} exceeds limit ({payloadLimit} bytes) for message type '{type}'.");
        }

        if (string.Equals(type, "frame", StringComparison.OrdinalIgnoreCase))
        {
            ValidateFramePayloadShape(header, payloadLength);
        }
    }

    private static uint GetPayloadLimit(string type)
    {
        if (string.Equals(type, "frame", StringComparison.OrdinalIgnoreCase))
        {
            return MaxFramePayloadBytes;
        }

        if (string.Equals(type, "frame_ready", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return MaxControlPayloadBytes;
    }

    private static void ValidateFramePayloadShape(ProtocolEnvelope header, uint payloadLength)
    {
        if (header.Stride is not > 0 || header.Height is not > 0)
        {
            return;
        }

        var expected = (long)header.Stride.Value * header.Height.Value;
        if (expected <= 0 || expected > MaxFramePayloadBytes)
        {
            throw new InvalidDataException(
                $"Frame metadata implies payload size {expected}, outside supported range for frame messages.");
        }

        if (payloadLength != (uint)expected)
        {
            throw new InvalidDataException(
                $"Frame payload length mismatch: got {payloadLength}, expected {expected} from stride*height.");
        }
    }
}
