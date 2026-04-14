using System.Runtime.Versioning;

namespace BlenderAvaloniaBridge.Runtime.FrameTransport;

[SupportedOSPlatform("macos")]
internal sealed class MacSharedMemoryFrameWriter : ISharedFrameWriter
{
    private readonly int _slotSize;
    private readonly int _slotCount;
    private readonly PosixFileMappedRegion _region;

    public MacSharedMemoryFrameWriter(string mappedFilePath, int slotSize, int slotCount)
    {
        if (string.IsNullOrWhiteSpace(mappedFilePath))
        {
            throw new ArgumentException("Mapped file path must not be empty.", nameof(mappedFilePath));
        }

        if (slotSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotSize));
        }

        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        _slotSize = slotSize;
        _slotCount = slotCount;
        _region = PosixFileMappedRegion.OpenExisting(mappedFilePath, slotSize * (long)slotCount);
    }

    public int WriteFrame(byte[] payload, int sequence)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > _slotSize)
        {
            throw new ArgumentException("Payload exceeds the shared-memory slot size.", nameof(payload));
        }

        var slot = Math.Abs(sequence) % _slotCount;
        var destination = _region.GetWritableSpan(slot * (long)_slotSize, payload.Length);
        payload.AsSpan().CopyTo(destination);
        return slot;
    }

    public int WriteLinearRgbaFrameFromRgba(ReadOnlySpan<byte> payload, int sequence)
    {
        var convertedSize = checked(payload.Length * sizeof(float));
        if (convertedSize > _slotSize)
        {
            throw new ArgumentException("Converted payload exceeds the shared-memory slot size.", nameof(payload));
        }

        var slot = Math.Abs(sequence) % _slotCount;
        var destination = _region.GetWritableSpan(slot * (long)_slotSize, convertedSize);
        FramePublisher.ConvertRgbaToLinearRgbaFloatBytes(payload, destination);
        return slot;
    }

    public void Dispose()
    {
        _region.Dispose();
    }
}
