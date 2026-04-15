using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace BlenderAvaloniaBridge.Sample.Helpers;

internal static class MaterialPreviewImageFactory
{
    internal static IImage? Create(int[]? size, BlenderArrayReadResult? pixels)
    {
        var data = CreateData(size, pixels);
        return data is null ? null : CreateImage(data);
    }

    internal static MaterialPreviewImageData? CreateData(int[]? size, BlenderArrayReadResult? pixels)
    {
        if (size is null || pixels is null || size.Length < 2)
        {
            return null;
        }

        var width = size[0];
        var height = size[1];
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        if (!string.Equals(pixels.ElementType, "float32", StringComparison.Ordinal))
        {
            return null;
        }

        var expectedComponentCount = checked(width * height * 4);
        var expectedLength = checked(expectedComponentCount * sizeof(float));
        if (pixels.RawBytes.Length < expectedLength)
        {
            return null;
        }

        var rgbaBytes = new byte[expectedComponentCount];
        var floatPixels = MemoryMarshal.Cast<byte, float>(pixels.RawBytes.AsSpan(0, expectedLength));
        var hasVisibleContent = false;

        for (var index = 0; index < floatPixels.Length; index++)
        {
            var value = FloatToByte(floatPixels[index]);
            rgbaBytes[index] = value;
            hasVisibleContent |= value != 0;
        }

        if (!hasVisibleContent)
        {
            return null;
        }

        return new MaterialPreviewImageData(width, height, rgbaBytes);
    }

    internal static IImage CreateImage(MaterialPreviewImageData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var bitmap = new WriteableBitmap(
            new PixelSize(data.Width, data.Height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);

        using var locked = bitmap.Lock();
        Marshal.Copy(data.RgbaBytes, 0, locked.Address, data.RgbaBytes.Length);
        return bitmap;
    }

    private static byte FloatToByte(float value)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        return (byte)Math.Round(clamped * 255f, MidpointRounding.AwayFromZero);
    }
}

internal sealed class MaterialPreviewImageData
{
    public MaterialPreviewImageData(int width, int height, byte[] rgbaBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(rgbaBytes);

        Width = width;
        Height = height;
        RgbaBytes = rgbaBytes;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] RgbaBytes { get; }
}
