using Avalonia.Media.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal static class FramePublisher
{
    private static readonly int[] Srgb8ToLinearFloatBits = BuildSrgb8ToLinearFloatBits();
    private static readonly int[] ByteToFloatBits = BuildByteToFloatBits();

    public static FrameCaptureResult ExtractFrame(WriteableBitmap bitmap, int seq, double? captureFrameMsOverride = null)
    {
        var captureStartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var extractStopwatch = Stopwatch.StartNew();
        using var locked = bitmap.Lock();
        extractStopwatch.Stop();

        var copyStopwatch = Stopwatch.StartNew();
        var payload = new byte[locked.RowBytes * locked.Size.Height];
        unsafe
        {
            var source = new Span<byte>(locked.Address.ToPointer(), payload.Length);
            source.CopyTo(payload);
        }

        var framePayload = payload.ToArray();
        ConvertRgbaToBgraInPlace(framePayload);
        copyStopwatch.Stop();

        var capturedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new FrameCaptureResult(
            ProtocolPacket.CreateFrame(
                new ProtocolEnvelope
                {
                    Type = "frame",
                    Seq = seq,
                    Width = locked.Size.Width,
                    Height = locked.Size.Height,
                    PixelFormat = "bgra8",
                    Stride = locked.RowBytes,
                    CapturedAtUnixMs = capturedAt,
                    CaptureStartedAtUnixMs = captureStartedAt,
                    CaptureFrameMs = captureFrameMsOverride ?? extractStopwatch.Elapsed.TotalMilliseconds,
                    CopyBgraMs = copyStopwatch.Elapsed.TotalMilliseconds,
                },
                framePayload),
            new FrameCaptureMetrics(
                captureStartedAt,
                capturedAt,
                captureFrameMsOverride ?? extractStopwatch.Elapsed.TotalMilliseconds,
                copyStopwatch.Elapsed.TotalMilliseconds),
            payload);
    }

    public static byte[] ConvertRgbaToLinearRgbaFloatBytes(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var result = new byte[payload.Length * sizeof(float)];
        ConvertRgbaToLinearRgbaFloatBytes(payload, result);
        return result;
    }

    internal static void ConvertRgbaToLinearRgbaFloatBytes(ReadOnlySpan<byte> payload, Span<byte> destination)
    {
        if ((payload.Length & 3) != 0)
        {
            throw new ArgumentException("Pixel payload length must be divisible by 4.", nameof(payload));
        }

        var expectedLength = payload.Length * sizeof(float);
        if (destination.Length < expectedLength)
        {
            throw new ArgumentException("Destination buffer is too small for RGBA float output.", nameof(destination));
        }

        var source = payload;
        var destinationBits = MemoryMarshal.Cast<byte, int>(destination[..expectedLength]);

        for (int src = 0, dst = 0; src < source.Length; src += 4, dst += 4)
        {
            destinationBits[dst] = Srgb8ToLinearFloatBits[source[src]];
            destinationBits[dst + 1] = Srgb8ToLinearFloatBits[source[src + 1]];
            destinationBits[dst + 2] = Srgb8ToLinearFloatBits[source[src + 2]];
            destinationBits[dst + 3] = ByteToFloatBits[source[src + 3]];
        }
    }

    internal static void ConvertRgbaToBgraInPlace(byte[] payload)
    {
        for (var index = 0; index + 3 < payload.Length; index += 4)
        {
            (payload[index], payload[index + 2]) = (payload[index + 2], payload[index]);
        }
    }

    private static int[] BuildSrgb8ToLinearFloatBits()
    {
        var table = new int[256];
        for (var index = 0; index < table.Length; index++)
        {
            var srgb = index / 255f;
            table[index] = BitConverter.SingleToInt32Bits(SrgbToLinear(srgb));
        }

        return table;
    }

    private static int[] BuildByteToFloatBits()
    {
        var table = new int[256];
        for (var index = 0; index < table.Length; index++)
        {
            table[index] = BitConverter.SingleToInt32Bits(index / 255f);
        }

        return table;
    }

    private static float SrgbToLinear(float value)
    {
        return value <= 0.04045f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}

internal sealed record FrameCaptureMetrics(
    long CaptureStartedAtUnixMs,
    long CapturedAtUnixMs,
    double CaptureFrameMs,
    double CopyBgraMs);

internal sealed record FrameCaptureResult(ProtocolPacket FramePacket, FrameCaptureMetrics Metrics, byte[] RawRgbaPayload);
