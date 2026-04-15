using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class DesktopWindowUiSession : IBridgeUiSession
{
    private readonly Func<Window> _windowFactory;
    private readonly BlenderBridgeOptions _options;
    private Window? _window;
    private IBlenderBridgeStatusSink? _statusSink;
    private IBlenderBridgeMessageHost? _messageHost;
    private IBusinessEndpointSink? _businessEndpointSink;
    private IBlenderDataApiSink? _blenderDataApiSink;

    public DesktopWindowUiSession(Func<Window> windowFactory, BlenderBridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(windowFactory);
        _windowFactory = windowFactory;
        _options = options.Clone();
    }

    public bool SupportsFrames => false;

    public bool SupportsInput => false;

    public event Action? FrameRequested
    {
        add { }
        remove { }
    }

    public async Task InitializeAsync()
    {
        if (_window is not null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_window is not null)
            {
                return;
            }

            _window = _windowFactory();
            if (_window.Background is null)
            {
                _window.Background = new SolidColorBrush(Color.Parse("#0F172A"));
            }

            _statusSink = ResolveFromWindow<IBlenderBridgeStatusSink>(_window);
            _messageHost = ResolveFromWindow<IBlenderBridgeMessageHost>(_window);
            _businessEndpointSink = ResolveFromWindow<IBusinessEndpointSink>(_window);
            _blenderDataApiSink = ResolveFromWindow<IBlenderDataApiSink>(_window);

            if (!_window.IsVisible)
            {
                _window.Show();
            }
        });
    }

    public async Task AttachBusinessApiAsync(IBusinessEndpoint businessEndpoint, IBlenderDataApi blenderDataApi)
    {
        await InitializeAsync();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _messageHost?.AttachBlenderDataApi(blenderDataApi);
            _businessEndpointSink?.AttachBusinessEndpoint(businessEndpoint);
            _blenderDataApiSink?.AttachBlenderDataApi(blenderDataApi);
        });
    }

    public async Task DeliverBridgeMessageAsync(ProtocolEnvelope envelope)
    {
        await InitializeAsync();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _messageHost?.HandleBridgeMessageAsync(envelope).GetAwaiter().GetResult();
        });
    }

    public Task<FrameCaptureResult> CaptureFrameAsync(int seq)
    {
        throw new NotSupportedException($"Frame capture is not supported in {nameof(DesktopWindowUiSession)}.");
    }

    public ProtocolPacket CreateInitAck(int seq)
    {
        return ProtocolPacket.CreateControl(
            new ProtocolEnvelope
            {
                Type = "init",
                Seq = seq,
                Width = _options.Width,
                Height = _options.Height,
                WindowMode = "desktop",
                SupportsBusiness = _options.SupportsBusiness,
                SupportsFrames = false,
                SupportsInput = false,
                Message = "desktop-business-ready",
            });
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

    public void Dispose()
    {
    }
}
