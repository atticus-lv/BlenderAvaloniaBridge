# Integration Guide

This page explains how to integrate Blender Avalonia Bridge into an existing project.

If you only want to run the sample included in this repository, start with [Quick Start](../guide/quick-start.md).

## Integration boundary

A real integration usually requires changes on both sides:

- The Avalonia app owns the UI, state, and business logic and runs as the bridge process
- The Blender addon starts the bridge, draws the overlay, forwards input, and hosts the business channel

In practice, integration work is usually done by wiring both sides together and then iterating in end-to-end testing.

## Shared configuration

`window_mode` matches the bridge runtime mode:

- `headless`: the default mode, enabling `frames + input + business`
- `desktop`: classic desktop window mode with `business` connection only

Sizing and density settings:

- `width` and `height` control the logical Avalonia window size
- `render_scaling` controls how densely the headless frame is rendered

`render_scaling` only applies to headless mode and helps preserve desktop-like sharpness and layout on high-resolution displays.

The mode is selected explicitly through CLI bridge arguments, and the final active capabilities are confirmed in the `init` / `init ack` handshake.

Headless frame transport uses shared memory by default on Windows and macOS. The bridge keeps fields such as `shm_name`, `frame_size`, and `slot_count` inside the transport handshake, so addon UIs usually do not need a separate shared-memory toggle.

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

## 3. Copy the Blender-side bridge core

Copy this directory into your own Blender addon package:

```text
src\blender_extension\avalonia_bridge\core\
```

Blender-side structure:

- `BridgeController` owns the bridge core only: process lifecycle, transport, frame pipeline, business packets, state, and diagnostics
- `View3DOverlayHost` is the optional `3D View` presentation adapter: overlay drawing, title-bar drag, hit-testing, input forwarding, and redraw

Minimal assembly example:

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessEndpoint,
    View3DOverlayHost,
)


config = BridgeConfig(
    executable_path="/path/to/YourAvaloniaApp",
    width=1100,
    height=760,
    render_scaling=1.25,
    window_mode="headless",
    supports_business=True,
    supports_frames=True,
    supports_input=True,
    host="127.0.0.1",
    show_overlay_debug=False,
)

presentation_host = View3DOverlayHost() if config.supports_frames else None

controller = BridgeController(
    config,
    host=presentation_host,
    business_endpoint=DefaultBusinessEndpoint(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)

controller.start()
```

- `headless`: `BridgeController(..., host=View3DOverlayHost(...))`
- `desktop` / business-only: `BridgeController(..., host=None)`

## 4. Wire lifecycle and input forwarding

On the Blender side, integration usually happens in two layers:

- runtime adapter: build `BridgeConfig` and assemble `BridgeController` with an optional `View3DOverlayHost`
- modal operator: call `tick_once()` on `TIMER` and `handle_event(context, event)` inside the event pipeline
- state sync: use `state_callback`, `state_snapshot()`, or `diagnostics_snapshot()` to feed your own UI
- user-facing controls: expose `width`, `height`, and `render_scaling` together so users can tune layout size and sharpness independently in headless mode

Runtime assembly example:

```python
def create_controller(mode: str) -> BridgeController:
    config = BridgeConfig(
        executable_path="/path/to/YourAvaloniaApp",
        width=1100,
        height=760,
        render_scaling=1.25,
        window_mode=mode,
        supports_business=True,
        supports_frames=mode != "desktop",
        supports_input=mode != "desktop",
        host="127.0.0.1",
        show_overlay_debug=False,
    )
    host = View3DOverlayHost() if config.supports_frames else None
    return BridgeController(
        config,
        host=host,
        business_endpoint=DefaultBusinessEndpoint(),
    )
```

Minimal modal operator example:

```python
import bpy


class BRIDGE_OT_start(bpy.types.Operator):
    bl_idname = "your_addon.bridge_start"
    bl_label = "Start Bridge"

    def execute(self, context):
        controller = create_controller(mode="headless")
        context.window_manager.your_bridge_controller = controller
        controller.start()
        bpy.ops.your_addon.bridge_modal("INVOKE_DEFAULT")
        return {"FINISHED"}


class BRIDGE_OT_modal(bpy.types.Operator):
    bl_idname = "your_addon.bridge_modal"
    bl_label = "Bridge Modal"
    bl_options = {"BLOCKING"}

    _timer = None

    def invoke(self, context, _event):
        self._timer = context.window_manager.event_timer_add(1.0 / 60.0, window=context.window)
        context.window_manager.modal_handler_add(self)
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        controller = getattr(context.window_manager, "your_bridge_controller", None)
        if controller is None:
            self.cancel(context)
            return {"CANCELLED"}

        if not controller.state_snapshot().process_running:
            self.cancel(context)
            return {"CANCELLED"}

        if event.type == "TIMER":
            controller.tick_once()
            return {"RUNNING_MODAL"}

        if context.area and context.area.type == "VIEW_3D":
            if controller.handle_event(context, event):
                return {"RUNNING_MODAL"}

        return {"PASS_THROUGH"}

    def cancel(self, context):
        controller = getattr(context.window_manager, "your_bridge_controller", None)
        if controller is not None:
            controller.stop()
            context.window_manager.your_bridge_controller = None
        if self._timer is not None:
            context.window_manager.event_timer_remove(self._timer)
            self._timer = None
```

See [Architecture](../advanced/architecture.md) for the shared session model and capability negotiation flow.

## 5. C# business-side usage

Once the bridge is connected, your Avalonia app can use the built-in `BlenderApi` root for Blender data, operators, and watch subscriptions through `Rna`, `Ops`, and `Observe`.

Read that part separately in the [API section](../api/index.md).
