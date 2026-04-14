using System.Buffers.Binary;
using BlenderAvaloniaBridge.Runtime.FrameTransport;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class MacSharedMemoryFrameWriterTests
{
    [Fact]
    public void WriteLinearRgbaFrameFromRgba_WritesConvertedFloatsIntoMappedFile()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var mappedFilePath = Path.Combine(Path.GetTempPath(), $"avb_{Guid.NewGuid():N}.bin");
        const int frameSize = 16;

        try
        {
            using (var fileStream = File.Open(mappedFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                fileStream.SetLength(frameSize * 2L);
            }

            using var region = PosixFileMappedRegion.OpenExisting(mappedFilePath, frameSize * 2L);
            using var writer = new MacSharedMemoryFrameWriter(mappedFilePath, frameSize, 2);

            var slot = writer.WriteLinearRgbaFrameFromRgba(new byte[] { 15, 23, 42, 255 }, 3);

            Assert.Equal(1, slot);

            var written = new byte[16];
            region.Read(frameSize, written);

            var red = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(0, 4)));
            var green = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(4, 4)));
            var blue = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(8, 4)));
            var alpha = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(12, 4)));

            Assert.InRange(red, 0.0047f, 0.0051f);
            Assert.InRange(green, 0.0083f, 0.0088f);
            Assert.InRange(blue, 0.0230f, 0.0240f);
            Assert.Equal(1.0f, alpha);
        }
        finally
        {
            if (File.Exists(mappedFilePath))
            {
                File.Delete(mappedFilePath);
            }
        }
    }

    [Fact]
    public void OpenExisting_RequiresPath()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var error = Assert.Throws<ArgumentException>(() => PosixFileMappedRegion.OpenExisting("", 16));
        Assert.Contains("must not be empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
