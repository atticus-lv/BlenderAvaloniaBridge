using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace BlenderAvaloniaBridge.Runtime.MacOS;

[UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Avalonia.Skia private render helper access is version-pinned by the bridge package.")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Avalonia.Skia private render helper access is version-pinned by the bridge package.")]
internal static class SkiaVisualRendererBridge
{
    private const string DrawingContextHelperTypeName = "Avalonia.Skia.Helpers.DrawingContextHelper, Avalonia.Skia";
    private const string DrawingContextImplTypeName = "Avalonia.Platform.IDrawingContextImpl, Avalonia.Base";
    private const string ImmediateRendererTypeName = "Avalonia.Rendering.ImmediateRenderer, Avalonia.Base";
    private const string PlatformDrawingContextTypeName = "Avalonia.Media.PlatformDrawingContext, Avalonia.Base";

    public static void RenderVisual(SKCanvas canvas, Visual visual, Rect clipRect, double scaling)
    {
        using var platformImpl = CreateDrawingContextImpl(canvas);
        using var context = CreateDrawingContext(platformImpl);
        using var clip = context.PushClip(clipRect);
        if (Math.Abs(scaling - 1.0) < 0.0001)
        {
            RenderImmediate(context, visual);
            return;
        }

        using var transform = context.PushTransform(Matrix.CreateScale(scaling, scaling));
        RenderImmediate(context, visual);
    }

    private static IDisposable CreateDrawingContextImpl(SKCanvas canvas)
    {
        var helperType = Type.GetType(DrawingContextHelperTypeName, throwOnError: true)
            ?? throw new MacGpuInteropUnavailableException($"Type '{DrawingContextHelperTypeName}' was not found.");
        var wrapMethod = helperType.GetMethod(
            "WrapSkiaCanvas",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(SKCanvas), typeof(Vector)],
            modifiers: null)
            ?? throw new MacGpuInteropUnavailableException("Avalonia.Skia.Helpers.DrawingContextHelper.WrapSkiaCanvas(SKCanvas, Vector) was not found.");

        return wrapMethod.Invoke(obj: null, [canvas, new Vector(96.0, 96.0)]) as IDisposable
            ?? throw new MacGpuInteropUnavailableException("Avalonia.Skia.Helpers.DrawingContextHelper.WrapSkiaCanvas did not return a disposable drawing context implementation.");
    }

    private static DrawingContext CreateDrawingContext(IDisposable platformImpl)
    {
        var contextType = Type.GetType(PlatformDrawingContextTypeName, throwOnError: true)
            ?? throw new MacGpuInteropUnavailableException($"Type '{PlatformDrawingContextTypeName}' was not found.");
        var implType = Type.GetType(DrawingContextImplTypeName, throwOnError: true)
            ?? throw new MacGpuInteropUnavailableException($"Type '{DrawingContextImplTypeName}' was not found.");
        var constructor = contextType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [implType, typeof(bool)],
            modifiers: null)
            ?? throw new MacGpuInteropUnavailableException("Avalonia.Media.PlatformDrawingContext(IDrawingContextImpl, bool) was not found.");

        return constructor.Invoke([platformImpl, false]) as DrawingContext
            ?? throw new MacGpuInteropUnavailableException("Avalonia.Media.PlatformDrawingContext could not be created.");
    }

    private static void RenderImmediate(DrawingContext context, Visual visual)
    {
        var rendererType = Type.GetType(ImmediateRendererTypeName, throwOnError: true)
            ?? throw new MacGpuInteropUnavailableException($"Type '{ImmediateRendererTypeName}' was not found.");
        var method = rendererType.GetMethod(
            "Render",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(DrawingContext), typeof(Visual)],
            modifiers: null)
            ?? throw new MacGpuInteropUnavailableException("Avalonia.Rendering.ImmediateRenderer.Render(DrawingContext, Visual) was not found.");

        method.Invoke(obj: null, [context, visual]);
    }
}
