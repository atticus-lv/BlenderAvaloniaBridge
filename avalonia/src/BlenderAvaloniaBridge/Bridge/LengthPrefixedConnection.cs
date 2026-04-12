using System.Buffers.Binary;
using System.Text.Json;

namespace BlenderAvaloniaBridge.Bridge;

public sealed class LengthPrefixedConnection
{
    private readonly Stream _stream;

    public LengthPrefixedConnection(Stream stream)
    {
        _stream = stream;
    }

    public async Task WriteAsync(ProtocolPacket packet, CancellationToken cancellationToken)
    {
        packet.Header.PayloadLength = packet.Payload.Length;

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(packet.Header, ProtocolJsonContext.Default.ProtocolEnvelope);
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

        var headerBytes = await ReadExactlyAsync((int)headerLength, cancellationToken)
            ?? throw new EndOfStreamException("Unexpected end of stream while reading header.");
        var payloadBytes = payloadLength == 0
            ? Array.Empty<byte>()
            : await ReadExactlyAsync((int)payloadLength, cancellationToken)
                ?? throw new EndOfStreamException("Unexpected end of stream while reading payload.");

        var header = JsonSerializer.Deserialize(headerBytes, ProtocolJsonContext.Default.ProtocolEnvelope)
            ?? throw new InvalidDataException("Unable to deserialize protocol header.");

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
}
