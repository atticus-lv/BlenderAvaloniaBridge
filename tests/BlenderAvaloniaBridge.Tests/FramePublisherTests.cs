using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BlenderAvaloniaBridge.Runtime;
using System.Buffers.Binary;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class FramePublisherTests
{
    [Fact]
    public void ConvertRgbaToBgraInPlace_SwapsRedAndBlueChannels()
    {
        var payload = new byte[]
        {
            0x0F, 0x17, 0x2A, 0xFF,
            0xAA, 0xBB, 0xCC, 0xDD,
        };

        FramePublisher.ConvertRgbaToBgraInPlace(payload);

        Assert.Equal(new byte[] { 0x2A, 0x17, 0x0F, 0xFF, 0xCC, 0xBB, 0xAA, 0xDD }, payload);
    }

    [Fact]
    public async Task ExtractBgraBytes_ReturnsExpectedByteLength()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;
        var frame = await runtimeThread.InvokeAsync(() =>
        {
            var window = new Window
            {
                Width = 40,
                Height = 30,
                Background = Brushes.CornflowerBlue,
                Content = new Border
                {
                    Width = 40,
                    Height = 30,
                    Background = Brushes.Crimson
                }
            };

            window.Show();
            var bitmap = HeadlessFrameCapture.Capture(window);
            return FramePublisher.ExtractFrame(bitmap, 11);
        });

        Assert.Equal("frame", frame.FramePacket.Header.Type);
        Assert.Equal(11, frame.FramePacket.Header.Seq);
        Assert.Equal(40, frame.FramePacket.Header.Width);
        Assert.Equal(30, frame.FramePacket.Header.Height);
        Assert.Equal(160, frame.FramePacket.Header.Stride);
        Assert.Equal(4800, frame.FramePacket.Payload.Length);
        Assert.Equal(new byte[] { 60, 20, 220, 255 }, frame.FramePacket.Payload.Take(4).ToArray());
        Assert.Equal(new byte[] { 220, 20, 60, 255 }, frame.RawRgbaPayload.Take(4).ToArray());
    }

    [Fact]
    public async Task ExtractFrame_PreservesReportedRenderCaptureDuration()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;
        var frame = await runtimeThread.InvokeAsync(() =>
        {
            var window = new Window
            {
                Width = 16,
                Height = 12,
                Background = Brushes.CornflowerBlue
            };

            window.Show();
            var bitmap = HeadlessFrameCapture.Capture(window);
            return FramePublisher.ExtractFrame(bitmap, 21, 7.5);
        });

        Assert.Equal(7.5, frame.Metrics.CaptureFrameMs, 3);
        Assert.True(frame.FramePacket.Header.CaptureFrameMs.HasValue);
        Assert.Equal(7.5, frame.FramePacket.Header.CaptureFrameMs.Value, 3);
    }

    [Fact]
    public void ConvertRgbaToLinearRgbaFloatBytes_ProducesExpectedFirstPixel()
    {
        var payload = new byte[] { 15, 23, 42, 255 };

        var converted = FramePublisher.ConvertRgbaToLinearRgbaFloatBytes(payload);

        Assert.Equal(16, converted.Length);
        var red = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(converted.AsSpan(0, 4)));
        var green = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(converted.AsSpan(4, 4)));
        var blue = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(converted.AsSpan(8, 4)));
        var alpha = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(converted.AsSpan(12, 4)));

        Assert.InRange(red, 0.0047f, 0.0051f);
        Assert.InRange(green, 0.0083f, 0.0088f);
        Assert.InRange(blue, 0.0230f, 0.0240f);
        Assert.Equal(1.0f, alpha);
    }
}
