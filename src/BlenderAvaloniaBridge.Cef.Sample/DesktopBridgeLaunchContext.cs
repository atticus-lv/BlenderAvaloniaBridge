using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace BlenderAvaloniaBridge.Cef.Sample;

internal static class DesktopBridgeLaunchContext
{
    private static BlenderBridgeOptions? _options;

    public static void Configure(BlenderBridgeOptions options)
    {
        _options = options.Clone();
    }

    public static bool IsConfigured => _options is not null;

    public static void StartBridge(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var options = _options?.Clone() ?? throw new InvalidOperationException("Desktop bridge options are not configured.");
        var cancellation = new CancellationTokenSource();
        desktop.Exit += (_, _) => cancellation.Cancel();
        _ = Task.Run(async () =>
        {
            try
            {
                await BlenderBridgeHost.RunAsync(window, options, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        });
    }
}
