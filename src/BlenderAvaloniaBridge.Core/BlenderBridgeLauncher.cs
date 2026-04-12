using Avalonia.Controls;

namespace BlenderAvaloniaBridge;

public static class BlenderBridgeLauncher
{
    public static BlenderBridgeLaunch TryParse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = CommandLineOptions.Parse(args);
        return options.Mode == LaunchMode.BlenderBridge
            ? new BlenderBridgeLaunch(options.AppArgs, options)
            : new BlenderBridgeLaunch(options.AppArgs, null);
    }

    public static Task RunBridgeAsync(
        BlenderBridgeLaunch launch,
        Func<Window> createBridgeWindow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launch);

        return RunBridgeAsync(launch.GetRequiredBridgeOptions(), createBridgeWindow, cancellationToken);
    }

    public static Task RunBridgeAsync(
        BlenderBridgeOptions options,
        Func<Window> createBridgeWindow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(createBridgeWindow);

        return BlenderBridgeHost.RunAsync(createBridgeWindow, options, cancellationToken);
    }
}
