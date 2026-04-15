using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Diagnostics;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class HeadlessUiHost
    : IBridgeUiSession
{
    private readonly Func<Window> _windowFactory;
    private readonly BlenderBridgeOptions _options;
    private readonly HeadlessRuntimeThread _runtimeThread;
    private readonly bool _ownsRuntimeThread;
    private IBlenderBridgeStatusSink? _statusSink;
    private IBlenderBridgeMessageHost? _messageHost;
    private IBusinessEndpointSink? _businessEndpointSink;
    private IBlenderDataApiSink? _blenderDataApiSink;
    private InputDispatcher? _inputDispatcher;
    private Window? _window;
    private int _width;
    private int _height;
    private double _renderScaling;
    private DateTimeOffset _continuousFramesUntil = DateTimeOffset.MinValue;
    private bool _animationFrameQueued;

    public bool SupportsFrames => _options.SupportsFrames;

    public bool SupportsInput => _options.SupportsInput;

    public event Action? FrameRequested;

    public HeadlessUiHost(HeadlessRuntimeThread runtimeThread, Func<Window> windowFactory, BlenderBridgeOptions options, bool ownsRuntimeThread = false)
    {
        _runtimeThread = runtimeThread;
        _windowFactory = windowFactory;
        _options = options.Clone();
        _ownsRuntimeThread = ownsRuntimeThread;
        _width = _options.Width;
        _height = _options.Height;
        _renderScaling = _options.RenderScaling;
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
            _blenderDataApiSink = ResolveBlenderDataApiSink(_window);
            _inputDispatcher = new InputDispatcher(_statusSink);
            _window.Show();
            _window.SetRenderScaling(_renderScaling);
            ExtendContinuousFrames(_options.ContinuousFrameWindow);
            return true;
        });
    }

    public async Task DeliverBridgeMessageAsync(ProtocolEnvelope envelope)
    {
        await InitializeAsync();

        await _runtimeThread.InvokeAsync(() =>
        {
            switch (envelope.Type)
            {
                case "resize" when envelope.Width.HasValue && envelope.Height.HasValue:
                    _width = envelope.Width.Value;
                    _height = envelope.Height.Value;
                    if (_window is not null)
                    {
                        _window.Width = _width;
                        _window.Height = _height;
                        _statusSink?.SetBridgeStatus($"Resize {_width}x{_height}");
                    }

                    ExtendContinuousFrames(_options.ContinuousFrameWindow);
                    break;
                case "pointer_move":
                case "pointer_down":
                case "pointer_up":
                case "wheel":
                case "key_down":
                case "key_up":
                case "text":
                case "focus":
                    _inputDispatcher!.DispatchAsync(_window!, envelope).GetAwaiter().GetResult();
                    ExtendContinuousFrames(_options.ContinuousFrameWindow);
                    break;
                default:
                    _messageHost?.HandleBridgeMessageAsync(envelope).GetAwaiter().GetResult();
                    break;
            }

            return true;
        });
    }

    public async Task<FrameCaptureResult> CaptureFrameAsync(int seq)
    {
        await InitializeAsync();
        return await _runtimeThread.InvokeAsync(() =>
        {
            var captureStopwatch = Stopwatch.StartNew();
            var window = _window ?? throw new InvalidOperationException("Headless window is not initialized.");
            WriteableBitmap? bitmap = null;
            for (var i = 0; i < 3 && bitmap is null; i++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                bitmap = window.GetLastRenderedFrame();
            }

            // Headless runtime can occasionally report null for the very first frame.
            // Fall back once to the synchronous capture API to avoid dropping the session.
            if (bitmap is null)
            {
                bitmap = window.CaptureRenderedFrame();
            }

            captureStopwatch.Stop();
            return FramePublisher.ExtractFrame(bitmap ?? throw new InvalidOperationException("Headless renderer did not produce a frame."), seq, captureStopwatch.Elapsed.TotalMilliseconds);
        });
    }

    public ProtocolPacket CreateInitAck(int seq)
    {
        return ProtocolPacket.CreateControl(
            new ProtocolEnvelope
            {
                Type = "init",
                Seq = seq,
                Width = ScaleDimension(_width, _renderScaling),
                Height = ScaleDimension(_height, _renderScaling),
                PixelFormat = "bgra8",
                Stride = ScaleDimension(_width, _renderScaling) * 4,
                WindowMode = "headless",
                SupportsBusiness = _options.SupportsBusiness,
                SupportsFrames = _options.SupportsFrames,
                SupportsInput = _options.SupportsInput,
                Message = "headless-ready"
            });
    }

    private void OnWindowLayoutUpdated(object? sender, EventArgs e)
    {
        FrameRequested?.Invoke();
        ExtendContinuousFrames(_options.ContinuousFrameWindow);
    }

    public Task AttachBusinessApiAsync(IBusinessEndpoint businessEndpoint, IBlenderDataApi blenderDataApi)
    {
        return _runtimeThread.InvokeAsync(() =>
        {
            _messageHost?.AttachBlenderDataApi(blenderDataApi);
            _businessEndpointSink?.AttachBusinessEndpoint(businessEndpoint);
            _blenderDataApiSink?.AttachBlenderDataApi(blenderDataApi);
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

    private static IBlenderDataApiSink? ResolveBlenderDataApiSink(Window window)
    {
        return ResolveFromWindow<IBlenderDataApiSink>(window);
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

    private static int ScaleDimension(int logicalSize, double renderScaling)
    {
        return Math.Max(1, (int)Math.Round(logicalSize * renderScaling, MidpointRounding.AwayFromZero));
    }

    public void Dispose()
    {
        if (_ownsRuntimeThread)
        {
            _runtimeThread.Dispose();
        }
    }
}
