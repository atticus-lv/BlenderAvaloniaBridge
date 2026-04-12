using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using System.Diagnostics;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class HeadlessUiHost
{
    private readonly Func<Window> _windowFactory;
    private readonly BlenderBridgeOptions _options;
    private readonly HeadlessRuntimeThread _runtimeThread;
    private IBlenderBridgeStatusSink? _statusSink;
    private IBlenderBridgeMessageHost? _messageHost;
    private IBusinessEndpointSink? _businessEndpointSink;
    private InputDispatcher? _inputDispatcher;
    private Window? _window;
    private int _width;
    private int _height;
    private DateTimeOffset _continuousFramesUntil = DateTimeOffset.MinValue;
    private bool _animationFrameQueued;

    public event Action? FrameRequested;

    public HeadlessUiHost(HeadlessRuntimeThread runtimeThread, Func<Window> windowFactory, BlenderBridgeOptions options)
    {
        _runtimeThread = runtimeThread;
        _windowFactory = windowFactory;
        _options = options.Clone();
        _width = _options.Width;
        _height = _options.Height;
    }

    public async Task InitializeAsync()
    {
        if (_window is not null)
        {
            return;
        }

        await _runtimeThread.InvokeAsync(() =>
        {
            if (_window is not null)
            {
                return true;
            }

            _window = _windowFactory();
            _window.Width = _width;
            _window.Height = _height;
            _window.CanResize = false;
            if (_window.Background is null)
            {
                _window.Background = new SolidColorBrush(Color.Parse("#0F172A"));
            }
            _window.LayoutUpdated += OnWindowLayoutUpdated;
            _statusSink = ResolveStatusSink(_window);
            _messageHost = ResolveMessageHost(_window);
            _businessEndpointSink = ResolveBusinessEndpointSink(_window);
            _inputDispatcher = new InputDispatcher(_statusSink);
            _window.Show();
            ExtendContinuousFrames(_options.ContinuousFrameWindow);
            return true;
        });
    }

    public async Task ApplyAsync(ProtocolEnvelope envelope)
    {
        await InitializeAsync();

        if (envelope.Type == "resize" && envelope.Width.HasValue && envelope.Height.HasValue)
        {
            _width = envelope.Width.Value;
            _height = envelope.Height.Value;
            await _runtimeThread.InvokeAsync(() =>
            {
                if (_window is not null)
                {
                    _window.Width = _width;
                    _window.Height = _height;
                    _statusSink?.SetBridgeStatus($"Resize {_width}x{_height}");
                    ExtendContinuousFrames(_options.ContinuousFrameWindow);
                }

                return true;
            });
            return;
        }

        await _runtimeThread.InvokeAsync(() =>
        {
            _inputDispatcher!.DispatchAsync(_window!, envelope).GetAwaiter().GetResult();
            ExtendContinuousFrames(_options.ContinuousFrameWindow);
            return true;
        });
    }

    public async Task<FrameCaptureResult> CaptureFrameAsync(int seq)
    {
        await InitializeAsync();
        return await _runtimeThread.InvokeAsync(() =>
        {
            var captureStopwatch = Stopwatch.StartNew();
            var bitmap = _window!.CaptureRenderedFrame() ?? throw new InvalidOperationException("Headless renderer did not produce a frame.");
            captureStopwatch.Stop();
            return FramePublisher.ExtractFrame(bitmap, seq, captureStopwatch.Elapsed.TotalMilliseconds);
        });
    }

    public ProtocolPacket CreateInitAck(int seq)
    {
        return ProtocolPacket.CreateControl(
            new ProtocolEnvelope
            {
                Type = "init",
                Seq = seq,
                Width = _width,
                Height = _height,
                PixelFormat = "bgra8",
                Stride = _width * 4,
                Message = "headless-ready"
            });
    }

    private void OnWindowLayoutUpdated(object? sender, EventArgs e)
    {
        FrameRequested?.Invoke();
        ExtendContinuousFrames(_options.ContinuousFrameWindow);
    }

    public Task AttachBusinessEndpointAsync(IBusinessEndpoint businessEndpoint)
    {
        return _runtimeThread.InvokeAsync(() =>
        {
            _messageHost?.AttachBridgeClient(businessEndpoint as IBlenderBridgeClient);
            _businessEndpointSink?.AttachBusinessEndpoint(businessEndpoint);
            return true;
        });
    }

    public Task AttachBridgeClientAsync(IBlenderBridgeClient bridgeClient)
    {
        return _runtimeThread.InvokeAsync(() =>
        {
            _messageHost?.AttachBridgeClient(bridgeClient);
            return true;
        });
    }

    public Task DeliverBridgeMessageAsync(ProtocolEnvelope envelope)
    {
        return _runtimeThread.InvokeAsync(() =>
        {
            _messageHost?.HandleBridgeMessageAsync(envelope).GetAwaiter().GetResult();
            return true;
        });
    }

    private void ExtendContinuousFrames(TimeSpan duration)
    {
        var until = DateTimeOffset.UtcNow + duration;
        if (until > _continuousFramesUntil)
        {
            _continuousFramesUntil = until;
        }

        QueueAnimationFrame();
    }

    private void QueueAnimationFrame()
    {
        if (_window is null || _animationFrameQueued)
        {
            return;
        }

        _animationFrameQueued = true;
        _window.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _animationFrameQueued = false;
        FrameRequested?.Invoke();

        if (DateTimeOffset.UtcNow < _continuousFramesUntil)
        {
            QueueAnimationFrame();
        }
    }

    private static IBlenderBridgeStatusSink? ResolveStatusSink(Window window)
    {
        return ResolveFromWindow<IBlenderBridgeStatusSink>(window);
    }

    private static IBusinessEndpointSink? ResolveBusinessEndpointSink(Window window)
    {
        return ResolveFromWindow<IBusinessEndpointSink>(window);
    }

    private static IBlenderBridgeMessageHost? ResolveMessageHost(Window window)
    {
        return ResolveFromWindow<IBlenderBridgeMessageHost>(window);
    }

    private static TInterface? ResolveFromWindow<TInterface>(Window window)
        where TInterface : class
    {
        if (window.DataContext is TInterface windowSink)
        {
            return windowSink;
        }

        if (window.Content is StyledElement element)
        {
            return FindInterface<TInterface>(element);
        }

        return null;
    }

    private static TInterface? FindInterface<TInterface>(StyledElement element)
        where TInterface : class
    {
        if (element.DataContext is TInterface sink)
        {
            return sink;
        }

        if (element is ContentControl contentControl && contentControl.Content is StyledElement contentElement)
        {
            var contentSink = FindInterface<TInterface>(contentElement);
            if (contentSink is not null)
            {
                return contentSink;
            }
        }

        if (element is Panel panel)
        {
            foreach (var child in panel.Children.OfType<StyledElement>())
            {
                var childSink = FindInterface<TInterface>(child);
                if (childSink is not null)
                {
                    return childSink;
                }
            }
        }

        if (element is Decorator decorator && decorator.Child is StyledElement decoratorChild)
        {
            return FindInterface<TInterface>(decoratorChild);
        }

        return null;
    }
}
