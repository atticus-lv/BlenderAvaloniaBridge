using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using BlenderAvaloniaBridge.ViewModels;

namespace BlenderAvaloniaBridge.Bridge;

public sealed class HeadlessUiHost
{
    private readonly HeadlessRuntimeThread _runtimeThread;
    private readonly MainViewModel _viewModel = new();
    private readonly InputDispatcher _inputDispatcher;
    private Window? _window;
    private int _width;
    private int _height;

    public HeadlessUiHost(HeadlessRuntimeThread runtimeThread, int width, int height)
    {
        _runtimeThread = runtimeThread;
        _width = width;
        _height = height;
        _inputDispatcher = new InputDispatcher(_viewModel);
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

            var mainView = Views.MainViewFactory.CreateMainView();
            mainView.DataContext = _viewModel;

            _window = new Window
            {
                Width = _width,
                Height = _height,
                Background = new SolidColorBrush(Color.Parse("#0F172A")),
                CanResize = false,
                Content = mainView
            };
            _window.Show();
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
                    _viewModel.SetStatus($"Resize {_width}x{_height}");
                }

                return true;
            });
            return;
        }

        await _runtimeThread.InvokeAsync(() =>
        {
            _inputDispatcher.DispatchAsync(_window!, envelope).GetAwaiter().GetResult();
            return true;
        });
    }

    public async Task<ProtocolPacket> CaptureFrameAsync(int seq)
    {
        await InitializeAsync();
        return await _runtimeThread.InvokeAsync(() =>
        {
            var bitmap = _window!.CaptureRenderedFrame() ?? throw new InvalidOperationException("Headless renderer did not produce a frame.");
            return FramePublisher.ExtractFrame(bitmap, seq);
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
}
