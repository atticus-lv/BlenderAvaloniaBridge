using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Buffers.Binary;
using BlenderAvaloniaBridge.Runtime.FrameTransport;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

[SupportedOSPlatform("windows")]
public sealed class SharedMemoryFrameWriterTests
{
    [Fact]
    public void WriteFrame_RotatesAcrossDoubleBufferSlots()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var mappingName = $"RenderBuilderTest_{Guid.NewGuid():N}";
        const int frameSize = 8;
        using var writer = new WindowsSharedMemoryFrameWriter(mappingName, frameSize, 2);

        var slot0 = writer.WriteFrame(new byte[] { 1, 2, 3, 4 }, 10);
        var slot1 = writer.WriteFrame(new byte[] { 5, 6, 7, 8 }, 11);

        Assert.Equal(0, slot0);
        Assert.Equal(1, slot1);

        using var mmf = MemoryMappedFile.OpenExisting(mappingName, MemoryMappedFileRights.Read);
        using var accessor = mmf.CreateViewAccessor(0, frameSize * 2, MemoryMappedFileAccess.Read);
        var first = new byte[4];
        var second = new byte[4];
        accessor.ReadArray(0, first, 0, 4);
        accessor.ReadArray(frameSize, second, 0, 4);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, first);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, second);
    }

    [Fact]
    public void WriteFrame_ThrowsWhenPayloadExceedsSlotSize()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var mappingName = $"RenderBuilderTest_{Guid.NewGuid():N}";
        using var writer = new WindowsSharedMemoryFrameWriter(mappingName, 4, 2);

        var error = Assert.Throws<ArgumentException>(() => writer.WriteFrame(new byte[] { 1, 2, 3, 4, 5 }, 1));

        Assert.Contains("slot size", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteLinearRgbaFrameFromRgba_WritesConvertedFloatsIntoSharedMemory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var mappingName = $"RenderBuilderTest_{Guid.NewGuid():N}";
        const int frameSize = 16;
        using var writer = new WindowsSharedMemoryFrameWriter(mappingName, frameSize, 2);

        var slot = writer.WriteLinearRgbaFrameFromRgba(new byte[] { 15, 23, 42, 255 }, 3);

        Assert.Equal(1, slot);

        using var mmf = MemoryMappedFile.OpenExisting(mappingName, MemoryMappedFileRights.Read);
        using var accessor = mmf.CreateViewAccessor(0, frameSize * 2L, MemoryMappedFileAccess.Read);
        var written = new byte[16];
        accessor.ReadArray(frameSize, written, 0, written.Length);

        var red = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(0, 4)));
        var green = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(4, 4)));
        var blue = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(8, 4)));
        var alpha = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(12, 4)));

        Assert.InRange(red, 0.0047f, 0.0051f);
        Assert.InRange(green, 0.0083f, 0.0088f);
        Assert.InRange(blue, 0.0230f, 0.0240f);
        Assert.Equal(1.0f, alpha);
    }
}
