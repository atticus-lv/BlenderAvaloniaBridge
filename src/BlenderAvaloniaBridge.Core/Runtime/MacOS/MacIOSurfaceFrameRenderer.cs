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
    private const int TargetPoolSize = 4;
    private readonly List<MacIOSurfaceRenderTarget> _targetPool = [];
    private MacMetalContextLease? _metalContext;
    private PixelSize? _targetPoolSize;
    private int _nextTargetIndex;
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
        ClearTargetPool();

        _metalContext?.Dispose();
        _metalContext = null;
    }

    private MacIOSurfaceRenderTarget CaptureToTarget(Window window, PixelSize pixelSize, double scaling)
    {
        var renderRoot = GetRenderRoot(window);
        PrepareLayout(renderRoot, pixelSize, scaling);
        Dispatcher.UIThread.RunJobs();

        var metalContext = _metalContext ??= MacMetalContextProvider.Create();
        var target = RentTarget(pixelSize, metalContext);
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

    private MacIOSurfaceRenderTarget RentTarget(PixelSize pixelSize, MacMetalContextLease metalContext)
    {
        if (_targetPoolSize != pixelSize)
        {
            ClearTargetPool();
            _targetPoolSize = pixelSize;
            _nextTargetIndex = 0;
        }

        if (_targetPool.Count < TargetPoolSize)
        {
            var target = MacIOSurfaceRenderTarget.Create(pixelSize, metalContext);
            _targetPool.Add(target);
            _nextTargetIndex = _targetPool.Count % TargetPoolSize;
            return target;
        }

        var pooledTarget = _targetPool[_nextTargetIndex];
        _nextTargetIndex = (_nextTargetIndex + 1) % TargetPoolSize;
        return pooledTarget;
    }

    private void ClearTargetPool()
    {
        foreach (var target in _targetPool)
        {
            target.Dispose();
        }

        _targetPool.Clear();
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
