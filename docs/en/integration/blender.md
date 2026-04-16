# Blender-side integration

## 1. Copy the Blender-side bridge core

Copy this directory into your own Blender addon package:

```text
src\blender_extension\avalonia_bridge\core\
```

Blender-side structure:

- `BridgeController`: bridge core only. It owns process lifecycle, transport, frame pipeline, business packets, state, and diagnostics
- `View3DOverlayHost`: optional `3D View` host for overlay drawing, title-bar drag, hit-testing, input forwarding, and redraw

## 2. Assemble the controller

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

## 3. Lifecycle and event driving

Blender-side integration has two layers:

- runtime adapter: build `BridgeConfig` and assemble `BridgeController` with an optional `View3DOverlayHost`
- modal operator: call `tick_once()` on `TIMER` and `handle_event(context, event)` inside the event pipeline

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

## Next step

- Back to [Integration Overview](./index.md)
- For C# API usage, see the [API section](../api/index.md)
