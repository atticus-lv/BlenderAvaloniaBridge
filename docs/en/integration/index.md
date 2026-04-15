# Integration Guide

This page explains how to integrate Blender Avalonia Bridge into an existing project.

If you only want to run the sample included in this repository, start with [Quick Start](../guide/quick-start.md).

## Integration boundary

A real integration usually requires changes on both sides:

- The Avalonia app owns the UI, state, and business logic and runs as the bridge process
- The Blender addon starts the bridge, draws the overlay, forwards input, and hosts the business channel

In practice, integration work is usually done by wiring both sides together and then iterating in end-to-end testing.

## Runtime modes

The bridge SDK currently supports two runtime modes:

- `headless`: `frames + input + business`
- `desktop-business`: `business` only, hosted by a real Avalonia desktop window

Use `headless` if you want the UI to be drawn inside Blender.

Use `desktop-business` if you want to keep a real Avalonia desktop window and only exchange business data with Blender.

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

The recommended option is to reference the project directly from your Avalonia app:

```xml
<ItemGroup>
  <ProjectReference Include="..\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj" />
</ItemGroup>
```

If you only need a temporary validation setup, you can also reference the built DLL directly.

## 2. Update the Avalonia app entry point

The recommended approach is to keep one shared `Program.cs` entry point and branch automatically between normal desktop startup and bridge startup.

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

The mode is selected explicitly through CLI bridge arguments, and the final active capabilities are confirmed in the `init` / `init ack` handshake.

## 3. Copy the Blender-side bridge core

Copy this directory into your own Blender addon package:

```text
src\blender_extension\avalonia_bridge\core\
```

Then integrate `BridgeController` into your own operators or runtime adapter.

Minimal integration example:

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessEndpoint,
)


controller = BridgeController(
    BridgeConfig(
        executable_path="/path/to/YourAvaloniaApp.dll",
        width=1100,
        height=760,
        render_scaling=1.25,
        window_mode="headless",
        supports_business=True,
        supports_frames=True,
        supports_input=True,
        host="127.0.0.1",
        show_overlay_debug=False,
    ),
    business_endpoint=DefaultBusinessEndpoint(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)

controller.start()
```

## 4. Configure Blender-side runtime parameters

There are two `window_mode` values:

- `headless`: the default mode. Avalonia frames are streamed into the Blender window, usually inside the `3D Viewport`, and mouse or keyboard input is captured inside that region
- `desktop`: classic desktop window mode with business connection only

Sizing and density settings:

- `width` and `height` control the logical Avalonia window size
- `render_scaling` controls how densely the headless frame is rendered

`render_scaling` helps preserve similar UI sharpness and layout feel on high-resolution displays. It only applies to headless mode.

Headless frame transport uses shared memory by default on Windows and macOS. The bridge keeps fields such as `shm_name`, `frame_size`, and `slot_count` inside the transport handshake, so addon UIs usually do not need a separate shared-memory toggle.

## 5. Wire lifecycle and input forwarding

On the Blender side, you will usually connect these integration points:

- Lifecycle: call `start()`, `stop()`, and `tick_once()` from your own operators or runtime adapter
- Input forwarding: call `handle_event(context, event)` from your event pipeline when remote input is enabled
- State sync: use `state_callback`, `state_snapshot()`, or `diagnostics_snapshot()` to feed your own UI
- User-facing controls: expose `width`, `height`, and `render_scaling` together so users can tune layout size and sharpness independently in headless mode
- Capability-aware UX: reflect whether the current session has business only, frame streaming, or input enabled

See [Architecture](../advanced/architecture.md) for the shared session model and capability negotiation flow.

## 6. C# business-side usage

Once the bridge is connected, your Avalonia app can use the built-in `BlenderApi` root for Blender data, operators, and watch subscriptions through `Rna`, `Ops`, and `Observe`.

Read that part separately in the [API section](../api/index.md).
