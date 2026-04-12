# Blender Avalonia Bridge

Windows-first toolkit for running an Avalonia UI in a separate process, streaming frames into Blender, and sending Blender input back to Avalonia.

[中文](E:/blender_ava_demo/docs/README.zh-CN.md) | English

## Choose A Path

### Recommended Integration

Use this when you are building your own Avalonia app and your own Blender addon.

1. Integrate `BlenderAvaloniaBridge.Core` into your Avalonia project.
2. Copy `src/blender_extension/avalonia_bridge/core/` into your own Blender addon.
3. Optionally plug in your own message or business handler.

Avalonia side:

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

Blender side:

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessBridgeHandler,
)


controller = BridgeController(
    BridgeConfig(
        executable_path="C:/path/to/YourAvaloniaApp.exe",
        width=1100,
        height=760,
        host="127.0.0.1",
        show_overlay_debug=False,
    ),
    business_handler=DefaultBusinessBridgeHandler(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)
```

What you copy into your Blender addon:

- `src/blender_extension/avalonia_bridge/core/`

What you do not need to copy:

- `src/blender_extension/avalonia_bridge/panel.py`
- `src/blender_extension/avalonia_bridge/operators.py`
- `src/blender_extension/avalonia_bridge/preferences.py`
- `src/blender_extension/avalonia_bridge/properties.py`
- `src/blender_extension/avalonia_bridge/runtime.py`

Optional customization points:

- Avalonia side: implement `IBlenderBridgeMessageHost` or `IBlenderBridgeStatusSink`
- Blender side: replace `DefaultBusinessBridgeHandler` with your own handler

Bridge CLI arguments are intentionally namespaced so they do not collide with your app's own CLI:

- `--blender-bridge true`
- `--blender-bridge-host 127.0.0.1`
- `--blender-bridge-port 34567`
- `--blender-bridge-width 1100`
- `--blender-bridge-height 760`

### Quick Try With Your Own Avalonia App

Use this when you want to try the bridge without writing your own Blender addon yet.

1. Integrate `BlenderAvaloniaBridge.Core` into your Avalonia project.
2. Install the current `src/blender_extension/avalonia_bridge/` Blender extension from this repo.
3. In Blender, point `Avalonia Path` to your Avalonia executable.
4. Click `Start UI Bridge`.

This is the fastest way to validate:

- process launch
- connection
- frame streaming
- Blender-side overlay and input forwarding

### Preview The Included Sample

Use this when you just want to see the repo working end-to-end.

1. Build the sample Avalonia project in this repo.
2. Install the current Blender extension in `src/blender_extension/avalonia_bridge/`.
3. Point the addon to the built sample executable.
4. Click `Start UI Bridge`.

## Repo Layout

```text
src/
  BlenderAvaloniaBridge.Core/            reusable Avalonia SDK
  BlenderAvaloniaBridge.Sample/          sample app and demo UI
  blender_extension/
    avalonia_bridge/                     Blender extension shell used in this repo
      core/                              copyable Blender bridge core package
tests/
  BlenderAvaloniaBridge.Tests/
  blender_extension/avalonia_bridge/
```

## Build

Run from the repository root.

Restore:

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path '.').Path
dotnet restore .\BlenderAvaloniaBridge.slnx --configfile .\NuGet.Config
```

Build the sample app:

```powershell
dotnet build .\BlenderAvaloniaBridge.slnx -c Debug
```

Publish a release exe:

```powershell
dotnet build .\BlenderAvaloniaBridge.slnx -c Release
dotnet publish .\src\BlenderAvaloniaBridge.Sample\BlenderAvaloniaBridge.Sample.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\release-net10 --configfile .\NuGet.Config
```

Publish an AOT exe:

```powershell
dotnet publish .\src\BlenderAvaloniaBridge.Sample\BlenderAvaloniaBridge.Sample.csproj -c Release -r win-x64 -p:PublishAot=true -o .\artifacts\publish\aot-net10 --configfile .\NuGet.Config
```

Common executable paths:

- `src\BlenderAvaloniaBridge.Sample\bin\Debug\net10.0\BlenderAvaloniaBridge.Sample.exe`
- `artifacts\publish\release-net10\BlenderAvaloniaBridge.Sample.exe`
- `artifacts\publish\aot-net10\BlenderAvaloniaBridge.Sample.exe`

Recommended Blender addon path target:

- AOT exe for best performance

## Blender Extension Setup

1. Install the `src/blender_extension/avalonia_bridge/` folder as a Blender extension.
2. Open `View3D > Sidebar > RenderBuilder`.
3. Set `Avalonia Path`.
4. Click `Start UI Bridge`.

## More Docs

- Architecture and protocol notes: [docs/ARCHITECTURE.md](E:/blender_ava_demo/docs/ARCHITECTURE.md)
- 中文架构说明: [docs/ARCHITECTURE.zh-CN.md](E:/blender_ava_demo/docs/ARCHITECTURE.zh-CN.md)

## Known Limits

- Windows-first
- Shared memory path is Windows-only
- Fixed bridge size per launch
- No IME / clipboard / drag-drop
- Blender background mode is not suitable for GPU overlay testing
