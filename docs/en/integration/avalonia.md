# Avalonia Integration

Use this path when you already have your own Avalonia application and want to add Blender bridge support.

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

## 3. Update your Program entry point

Handle bridge mode explicitly in `Program.cs` and keep the bridge window creation under your control:

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

## 4. Optional extension points

For deeper integration, implement these interfaces in your own window or ViewModel:

- `IBusinessEndpointSink`
- `IBlenderBridgeStatusSink`

That lets your app receive a unified business endpoint plus bridge status updates.
