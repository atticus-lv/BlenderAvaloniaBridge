# Avalonia-side integration

## 1. Build and reference the SDK

The repository does not publish a NuGet package yet, so you need to build `BlenderAvaloniaBridge.Core` locally first.

Run this from the repository root:

```powershell
dotnet build .\src\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj -c Release --configfile .\NuGet.Config
```

The build output is placed at:

```text
src\BlenderAvaloniaBridge.Core\bin\Release\net10.0\BlenderAvaloniaBridge.Core.dll
```

Project reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj" />
</ItemGroup>
```

You can also reference the built DLL directly.

## 2. Update the app entry point

Use one shared `Program.cs` entry point and branch between normal desktop startup and bridge startup.

```csharp
using Avalonia;
using BlenderAvaloniaBridge;

internal static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        var launch = BlenderBridgeLauncher.TryParse(args);

        if (launch.IsBridgeMode)
        {
            var options = launch.GetRequiredBridgeOptions();

            if (options.WindowMode == BridgeWindowMode.Desktop)
            {
                DesktopBridgeLaunchContext.Configure(options);
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

public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

## 3. C# business-side usage

Once the bridge is connected, your Avalonia app can use the built-in `BlenderApi` root for Blender data, operators, and watch subscriptions through `Rna`, `Ops`, and `Observe`.

See the [API section](../api/index.md).

## Next step

- Back to [Integration Overview](./index.md)
- Continue with [Blender-side integration](./blender.md)
