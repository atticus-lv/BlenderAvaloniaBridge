using Avalonia;
using SkiaSharp;

namespace BlenderAvaloniaBridge.Runtime.MacOS;

internal sealed class MacIOSurfaceRenderTarget : IDisposable
{
    private const int MTLPixelFormatBGRA8Unorm = 80;
    private const ulong MTLTextureUsageShaderRead = 1;
    private const ulong MTLTextureUsageRenderTarget = 4;
    private const int PixelFormatBGRA = 0x42475241;
    private bool _isDisposed;

    private MacIOSurfaceRenderTarget(
        PixelSize pixelSize,
        IntPtr ioSurface,
        IntPtr texture,
        GRBackendRenderTarget backendRenderTarget,
        SKSurface surface,
        uint surfaceId)
    {
        PixelSize = pixelSize;
        IOSurface = ioSurface;
        Texture = texture;
        BackendRenderTarget = backendRenderTarget;
        Surface = surface;
        SurfaceId = surfaceId;
    }

    public PixelSize PixelSize { get; }

    public IntPtr IOSurface { get; }

    public IntPtr Texture { get; }

    public GRBackendRenderTarget BackendRenderTarget { get; }

    public SKSurface Surface { get; }

    public uint SurfaceId { get; }

    public static MacIOSurfaceRenderTarget Create(PixelSize pixelSize, MacMetalContextLease metalContext)
    {
        var ioSurface = IntPtr.Zero;
        var texture = IntPtr.Zero;
        GRBackendRenderTarget? backendRenderTarget = null;
        SKSurface? surface = null;

        try
        {
            ioSurface = CreateIOSurface(pixelSize);
            texture = CreateTexture(metalContext.Device, ioSurface, pixelSize);
            backendRenderTarget = new GRBackendRenderTarget(
                pixelSize.Width,
                pixelSize.Height,
                new GRMtlTextureInfo(texture));
            surface = SKSurface.Create(
                metalContext.GrContext,
                backendRenderTarget,
                GRSurfaceOrigin.TopLeft,
                SKColorType.Bgra8888);

            if (surface is null)
            {
                throw new MacGpuInteropUnavailableException("Could not create a Skia surface for the IOSurface-backed Metal texture.");
            }

            var target = new MacIOSurfaceRenderTarget(
                pixelSize,
                ioSurface,
                texture,
                backendRenderTarget,
                surface,
                MacMetalNative.IOSurfaceGetID(ioSurface));
            ioSurface = IntPtr.Zero;
            texture = IntPtr.Zero;
            backendRenderTarget = null;
            surface = null;
            return target;
        }
        finally
        {
            surface?.Dispose();
            backendRenderTarget?.Dispose();

            if (texture != IntPtr.Zero)
            {
                MacMetalNative.objc_msgSend(texture, MacMetalNative.GetSelector("release"));
            }

            if (ioSurface != IntPtr.Zero)
            {
                MacMetalNative.CFRelease(ioSurface);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Surface.Dispose();
        BackendRenderTarget.Dispose();
        MacMetalNative.objc_msgSend(Texture, MacMetalNative.GetSelector("release"));
        MacMetalNative.CFRelease(IOSurface);
    }

    private static IntPtr CreateIOSurface(PixelSize pixelSize)
    {
        var dictionary = MacMetalNative.CFDictionaryCreateMutable(
            IntPtr.Zero,
            capacity: 4,
            MacMetalNative.GetCoreFoundationExport("kCFTypeDictionaryKeyCallBacks"),
            MacMetalNative.GetCoreFoundationExport("kCFTypeDictionaryValueCallBacks"));
        if (dictionary == IntPtr.Zero)
        {
            throw new MacGpuInteropUnavailableException("Could not create an IOSurface properties dictionary.");
        }

        try
        {
            SetNumber(dictionary, "kIOSurfaceWidth", pixelSize.Width);
            SetNumber(dictionary, "kIOSurfaceHeight", pixelSize.Height);
            SetNumber(dictionary, "kIOSurfaceBytesPerElement", 4);
            SetNumber(dictionary, "kIOSurfacePixelFormat", PixelFormatBGRA);
            SetBoolean(dictionary, "kIOSurfaceIsGlobal", value: true);

            var surface = MacMetalNative.IOSurfaceCreate(dictionary);
            if (surface == IntPtr.Zero)
            {
                throw new MacGpuInteropUnavailableException("IOSurfaceCreate returned null.");
            }

            return surface;
        }
        finally
        {
            MacMetalNative.CFRelease(dictionary);
        }
    }

    private static void SetNumber(IntPtr dictionary, string keyName, int value)
    {
        var number = MacMetalNative.CFNumberCreate(IntPtr.Zero, CFNumberType.SInt32, ref value);
        if (number == IntPtr.Zero)
        {
            throw new MacGpuInteropUnavailableException($"Could not create CFNumber for {keyName}.");
        }

        try
        {
            MacMetalNative.CFDictionarySetValue(dictionary, MacMetalNative.GetIOSurfaceConstant(keyName), number);
        }
        finally
        {
            MacMetalNative.CFRelease(number);
        }
    }

    private static void SetBoolean(IntPtr dictionary, string keyName, bool value)
    {
        try
        {
            var boolean = MacMetalNative.GetCoreFoundationConstant(value ? "kCFBooleanTrue" : "kCFBooleanFalse");
            MacMetalNative.CFDictionarySetValue(dictionary, MacMetalNative.GetIOSurfaceConstant(keyName), boolean);
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static IntPtr CreateTexture(IntPtr device, IntPtr ioSurface, PixelSize pixelSize)
    {
        var descriptorClass = MacMetalNative.GetClass("MTLTextureDescriptor");
        if (descriptorClass == IntPtr.Zero)
        {
            throw new MacGpuInteropUnavailableException("MTLTextureDescriptor class was not found.");
        }

        var descriptor = MacMetalNative.IntPtr_objc_msgSend_UInt64_UInt64_UInt64_Bool(
            descriptorClass,
            MacMetalNative.GetSelector("texture2DDescriptorWithPixelFormat:width:height:mipmapped:"),
            MTLPixelFormatBGRA8Unorm,
            (ulong)pixelSize.Width,
            (ulong)pixelSize.Height,
            false);
        if (descriptor == IntPtr.Zero)
        {
            throw new MacGpuInteropUnavailableException("Could not create MTLTextureDescriptor.");
        }

        MacMetalNative.void_objc_msgSend_UInt64(
            descriptor,
            MacMetalNative.GetSelector("setUsage:"),
            MTLTextureUsageShaderRead | MTLTextureUsageRenderTarget);

        var texture = MacMetalNative.IntPtr_objc_msgSend_IntPtr_IntPtr_UInt64(
            device,
            MacMetalNative.GetSelector("newTextureWithDescriptor:iosurface:plane:"),
            descriptor,
            ioSurface,
            0);
        if (texture == IntPtr.Zero)
        {
            throw new MacGpuInteropUnavailableException("Could not create an IOSurface-backed Metal texture.");
        }

        return texture;
    }
}
