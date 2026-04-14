using Avalonia;
using BlenderAvaloniaBridge.Sample;
using BlenderAvaloniaBridge.Sample.Views;

namespace BlenderAvaloniaBridge;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var launch = BlenderBridgeLauncher.TryParse(args);

        try
        {
            if (launch.IsBridgeMode)
            {
                if (launch.GetRequiredBridgeOptions().WindowMode == BridgeWindowMode.Desktop)
                {
                    DesktopBridgeLaunchContext.Configure(launch.GetRequiredBridgeOptions());
                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(launch.AppArgs);
                    return 0;
                }

                await BlenderBridgeLauncher.RunBridgeAsync(
                    launch,
                    createBridgeWindow: () => new MainWindow());
                return 0;
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(launch.AppArgs);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
