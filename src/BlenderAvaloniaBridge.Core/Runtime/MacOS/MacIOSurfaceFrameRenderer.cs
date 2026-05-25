using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using BlenderAvaloniaBridge.Protocol;
using SkiaSharp;
using System.Diagnostics;

namespace BlenderAvaloniaBridge.Runtime.MacOS;

internal sealed class MacIOSurfaceFrameRenderer : IDisposable
{
    private const int RetainedFrameCount = 4;
    private readonly Queue<MacIOSurfaceRenderTarget> _retainedFrames = new();
    private MacMetalContextLease? _metalContext;
    private bool _isDisposed;

    public FrameCaptureResult Capture(Window window, int seq, int width, int height, double scaling)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(window);

        var pixelSize = new PixelSize(width, height);
        var captureStartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var stopwatch = Stopwatch.StartNew();
        var target = CaptureToTarget(window, pixelSize, scaling);
        stopwatch.Stop();

        Retain(target);
        var capturedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new FrameCaptureResult(
            ProtocolPacket.CreateControl(
                new ProtocolEnvelope
                {
                    Type = "frame",
                    Seq = seq,
                    Width = width,
                    Height = height,
                    PixelFormat = "bgra8_unorm",
                    Stride = width * 4,
                    CapturedAtUnixMs = capturedAt,
                    CaptureStartedAtUnixMs = captureStartedAt,
                    CaptureFrameMs = stopwatch.Elapsed.TotalMilliseconds,
                    CopyBgraMs = 0.0,
                }),
            new FrameCaptureMetrics(
                captureStartedAt,
                capturedAt,
                stopwatch.Elapsed.TotalMilliseconds,
                0.0),
            Array.Empty<byte>(),
            new ExternalGpuFrameInfo(
                BridgeFrameTransportNames.MacOSIOSurface,
                "iosurface",
                target.SurfaceId));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        while (_retainedFrames.TryDequeue(out var frame))
        {
            frame.Dispose();
        }

        _metalContext?.Dispose();
        _metalContext = null;
    }

    private MacIOSurfaceRenderTarget CaptureToTarget(Window window, PixelSize pixelSize, double scaling)
    {
        var renderRoot = GetRenderRoot(window);
        PrepareLayout(renderRoot, pixelSize, scaling);
        Dispatcher.UIThread.RunJobs();

        var metalContext = _metalContext ??= MacMetalContextProvider.Create();
        var target = MacIOSurfaceRenderTarget.Create(pixelSize, metalContext);
        try
        {
            var canvas = target.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            RenderToPixelCanvas(canvas, renderRoot, pixelSize, scaling);

            canvas.Flush();
            metalContext.GrContext.Flush(submit: true, synchronous: true);
            metalContext.GrContext.Submit(synchronous: true);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    private void Retain(MacIOSurfaceRenderTarget target)
    {
        _retainedFrames.Enqueue(target);
        while (_retainedFrames.Count > RetainedFrameCount)
        {
            _retainedFrames.Dequeue().Dispose();
        }
    }

    private static Visual GetRenderRoot(Window window)
    {
        if (window.Content is Visual content)
        {
            return content;
        }

        return window;
    }

    private static void PrepareLayout(Visual visual, PixelSize pixelSize, double scaling)
    {
        if (visual is not Layoutable layoutable)
        {
            return;
        }

        var size = pixelSize.ToSize(scaling);
        layoutable.Measure(size);
        layoutable.Arrange(new Rect(size));
        layoutable.UpdateLayout();
    }

    private static void RenderToPixelCanvas(SKCanvas canvas, Visual visual, PixelSize pixelSize, double scaling)
    {
        var clipRect = new Rect(pixelSize.ToSize(1.0));
        SkiaVisualRendererBridge.RenderVisual(canvas, visual, clipRect, scaling);
    }
}
