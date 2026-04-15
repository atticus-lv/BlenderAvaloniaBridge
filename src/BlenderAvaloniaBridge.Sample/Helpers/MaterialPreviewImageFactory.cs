using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace BlenderAvaloniaBridge.Sample.Helpers;

internal static class MaterialPreviewImageFactory
{
    internal static IImage? Create(int[]? size, byte[]? pixels)
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

        var expectedLength = checked(width * height * 4);
        if (pixels.Length < expectedLength)
        {
            return null;
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);

        using var locked = bitmap.Lock();
        Marshal.Copy(pixels, 0, locked.Address, expectedLength);
        return bitmap;
    }
}
