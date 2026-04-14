# Avalonia Integration

Use this path when you already have your own Avalonia application and want to add Blender bridge support.

The bridge SDK supports two runtime modes:

- `headless`: `frames + input + business`
- `desktop-business`: `business` only, hosted by a real Avalonia desktop window

## Current distribution model

The repository does not publish a NuGet package yet, so you need to build `BlenderAvaloniaBridge.Core` locally first.

## 1. Build the SDK

Run this from the repository root:

```powershell
dotnet build .\src\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj -c Release --configfile .\NuGet.Config
```

The build output is placed at:

```text
src\BlenderAvaloniaBridge.Core\bin\Release\net10.0\BlenderAvaloniaBridge.Core.dll
```

## 2. Choose an integration style

The recommended option is a project reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj" />
</ItemGroup>
```

If you only need a temporary integration, you can also reference the built DLL directly.

## 3. Choose a bridge mode

| Mode | Window Host | Frames | Input | Business | Use when |
| --- | --- | --- | --- | --- | --- |
| `headless` | Avalonia headless runtime | Yes | Yes | Yes | You want Blender to draw an overlay and forward input |
| `desktop-business` | Real Avalonia desktop window | No | No | Yes | You want to keep a real Avalonia window and only exchange business data |

The mode is selected explicitly through CLI bridge arguments, and the final active capabilities are confirmed in the `init` / `init ack` handshake.

## 4. Update your Program entry point

The recommended approach is to keep one shared entry point in `Program.cs` and branch automatically by `WindowMode`.

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
