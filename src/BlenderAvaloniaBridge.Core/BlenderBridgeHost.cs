using System.Net.Sockets;
using Avalonia.Controls;
using BlenderAvaloniaBridge.Runtime;
using BlenderAvaloniaBridge.Runtime.FrameTransport;
using BlenderAvaloniaBridge.Transport;

namespace BlenderAvaloniaBridge;

public sealed class BlenderBridgeHost
{
    private readonly BlenderBridgeOptions _options;
    private readonly Func<Window> _windowFactory;
    private Window? _window;

    public BlenderBridgeHost(Func<Window> windowFactory, BlenderBridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(windowFactory);
        ArgumentNullException.ThrowIfNull(options);

        _windowFactory = windowFactory;
        _options = options.Clone();
        _options.Validate();
    }

    public BlenderBridgeHost(Window window, BlenderBridgeOptions options)
        : this(() => window ?? throw new ArgumentNullException(nameof(window)), options)
    {
    }

    public BlenderBridgeDiagnosticsSnapshot DiagnosticsSnapshot { get; private set; } = new();

    public Window CreateWindowForTesting()
    {
        return GetOrCreateWindow();
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return RunCoreAsync(GetOrCreateWindow, _options, snapshot => DiagnosticsSnapshot = snapshot, cancellationToken);
    }

    public static Task RunAsync(Func<Window> windowFactory, BlenderBridgeOptions options, CancellationToken cancellationToken = default)
    {
        return new BlenderBridgeHost(windowFactory, options).RunAsync(cancellationToken);
    }

    public static Task RunAsync(Window window, BlenderBridgeOptions options, CancellationToken cancellationToken = default)
    {
        return new BlenderBridgeHost(window, options).RunAsync(cancellationToken);
    }

    private Window GetOrCreateWindow()
    {
        return _window ??= _windowFactory();
    }

    private static async Task RunCoreAsync(
        Func<Window> windowFactory,
        BlenderBridgeOptions options,
        Action<BlenderBridgeDiagnosticsSnapshot>? diagnosticsSink,
        CancellationToken cancellationToken)
    {
        if (options.UseSharedMemory && options.SupportsFrames)
        {
            SharedFrameWriterFactory.Instance.ValidatePlatformSupport();
        }

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(options.Host, options.Port, cancellationToken);

        using var networkStream = tcpClient.GetStream();
        var diagnostics = options.EnableDiagnostics && options.SupportsFrames ? new BridgeDiagnosticsCollector() : null;
        var connection = new LengthPrefixedConnection(networkStream);
        using var uiSession = CreateUiSession(windowFactory, options);
        var bridgeClient = new BridgeClient(connection, uiSession, options, diagnostics);

        await bridgeClient.RunAsync(cancellationToken);
        if (diagnostics is not null)
        {
            diagnosticsSink?.Invoke(diagnostics.CreateSnapshot());
        }
    }

    private static IBridgeUiSession CreateUiSession(Func<Window> windowFactory, BlenderBridgeOptions options)
    {
        return options.WindowMode == BridgeWindowMode.Desktop
            ? new DesktopWindowUiSession(windowFactory, options)
            : new HeadlessUiHost(HeadlessRuntimeThread.Shared, windowFactory, options);
    }
}
