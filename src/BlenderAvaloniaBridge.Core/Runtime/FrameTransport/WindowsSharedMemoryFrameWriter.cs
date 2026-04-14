using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

namespace BlenderAvaloniaBridge.Runtime.FrameTransport;

[SupportedOSPlatform("windows")]
internal sealed class WindowsSharedMemoryFrameWriter : ISharedFrameWriter
{
    private readonly int _slotSize;
    private readonly int _slotCount;
    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _accessor;

    public WindowsSharedMemoryFrameWriter(string mappingName, int slotSize, int slotCount)
    {
        if (string.IsNullOrWhiteSpace(mappingName))
        {
            throw new ArgumentException("Mapping name must not be empty.", nameof(mappingName));
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
        _mapping = MemoryMappedFile.CreateOrOpen(mappingName, slotSize * (long)slotCount, MemoryMappedFileAccess.ReadWrite);
        _accessor = _mapping.CreateViewAccessor(0, slotSize * (long)slotCount, MemoryMappedFileAccess.Write);
    }

    public int WriteFrame(byte[] payload, int sequence)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Length > _slotSize)
        {
            throw new ArgumentException("Payload exceeds the shared-memory slot size.", nameof(payload));
        }

        var slot = Math.Abs(sequence) % _slotCount;
        var offset = slot * (long)_slotSize;
        _accessor.WriteArray(offset, payload, 0, payload.Length);
        return slot;
    }

    public unsafe int WriteLinearRgbaFrameFromRgba(ReadOnlySpan<byte> payload, int sequence)
    {
        var convertedSize = checked(payload.Length * sizeof(float));
        if (convertedSize > _slotSize)
        {
            throw new ArgumentException("Converted payload exceeds the shared-memory slot size.", nameof(payload));
        }

        var slot = Math.Abs(sequence) % _slotCount;
        var offset = slot * (long)_slotSize;

        byte* pointer = null;
        try
        {
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            var basePointer = pointer + _accessor.PointerOffset + offset;
            var destination = new Span<byte>(basePointer, convertedSize);
            FramePublisher.ConvertRgbaToLinearRgbaFloatBytes(payload, destination);
        }
        finally
        {
            if (pointer is not null)
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }

        return slot;
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _mapping.Dispose();
    }
}
