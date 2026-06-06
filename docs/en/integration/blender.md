# Blender-side integration

## 1. Copy the Blender-side bridge core

Copy this directory into your own Blender addon package:

```text
src\blender_extension\avalonia_bridge\core\
```

Blender-side structure:

- `BridgeController`: bridge core only. It owns process lifecycle, transport, frame pipeline, business packets, state, and diagnostics
- `View3DOverlayHost`: optional `3D View` host for overlay drawing, title-bar drag, hit-testing, input forwarding, and redraw
- `native_gpu`: optional native GPU hook loader used to copy external frames into Blender GPU textures faster

## 2. Build the native GPU hook

The fast frame path for offscreen UI needs the Blender-side native hook. The hook is loaded through `ctypes`, and its default build output goes into the extension directory:

```text
src\blender_extension\avalonia_bridge\native\
```

Run from the repository root:

```sh
cmake -S src/blender_native -B src/blender_native/build
cmake --build src/blender_native/build --config Release
```

On macOS, this hook resolves Blender's Metal GPU texture symbols, imports the Avalonia `IOSurfaceID` as a Metal texture, and copies it into a Blender-created `gpu.types.GPUTexture`. If the hook is missing or fails to load, the bridge records diagnostics and falls back to an available frame transport path.

When integrating into your own extension, use one of these options:

- Ship the built library in `your_addon/native/`
- Set the `AVALONIA_BRIDGE_NATIVE_PATH` environment variable
- Provide `native_library_path` in the extension preferences

## 3. Assemble the controller

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
    target_fps=120,
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

- offscreen UI (`window_mode="headless"`): `BridgeController(..., host=View3DOverlayHost(...))`
- `desktop` / business-only: `BridgeController(..., host=None)`

## 4. Choose the presentation host

- `View3DOverlayHost` is the optional presentation host for Blender `3D View`
- The current sample uses it in offscreen UI mode to draw the UI into `3D View`
- If you do not want to draw in `3D View`, do not assemble `View3DOverlayHost` and keep only the business channel
- Other Blender presentation paths should be assembled at the addon layer

In the default host, Blender receives frames on a background socket thread and stores only the latest frame. The modal timer calls `tick_once()` to present that latest frame and process queued business packets. It does not request a higher frame rate from the bridge process.

## 5. Lifecycle and event driving

Blender-side integration has two layers:

- runtime adapter: build `BridgeConfig` and assemble `BridgeController` with an optional `View3DOverlayHost`
- modal operator: call `tick_once()` on `TIMER` and `handle_event(context, event)` inside the event pipeline

Current forwarded event packets:

- pointer packets: `pointer_down`, `pointer_up`, `pointer_move`, `wheel`
- keyboard packets: `key_down`, `key_up`
- text packets: `text` when the pressed key carries a non-empty `event.unicode`

Current Blender `event.type` support in `View3DOverlayHost`:

- pointer and wheel: `MOUSEMOVE`, `INBETWEEN_MOUSEMOVE`, `LEFTMOUSE`, `RIGHTMOUSE`, `MIDDLEMOUSE`, `WHEELUPMOUSE`, `WHEELDOWNMOUSE`, `EVT_TWEAK_L`, `EVT_TWEAK_M`, `EVT_TWEAK_R`
- title-bar drag handling only: `LEFTMOUSE`, `EVT_TWEAK_L`
- letters: `A`-`Z`
- top-row digits: `ZERO`-`NINE`
- basic edit and navigation: `SPACE`, `TAB`, `RET`, `NUMPAD_ENTER`, `BACK_SPACE`, `DEL`, `INSERT`, `HOME`, `END`, `PAGE_UP`, `PAGE_DOWN`, `ESC`, `LINE_FEED`
- arrow keys: `LEFT_ARROW`, `RIGHT_ARROW`, `UP_ARROW`, `DOWN_ARROW`
- punctuation: `PERIOD`, `NUMPAD_PERIOD`, `COMMA`, `MINUS`, `PLUS`, `EQUAL`, `SEMI_COLON`, `QUOTE`, `SLASH`, `BACK_SLASH`, `LEFT_BRACKET`, `RIGHT_BRACKET`, `ACCENT_GRAVE`
- modifier keys: `LEFT_SHIFT`, `RIGHT_SHIFT`, `LEFT_CTRL`, `RIGHT_CTRL`, `LEFT_ALT`, `RIGHT_ALT`, `OSKEY`, `APP`
- numpad keys: `NUMPAD_0`-`NUMPAD_9`, `NUMPAD_SLASH`, `NUMPAD_ASTERIX`, `NUMPAD_MINUS`, `NUMPAD_PLUS`
- function keys: `F1`-`F24`

Notes:

- keyboard packets are only forwarded while the overlay is capturing input
- `desktop` mode does not forward pointer or keyboard input because it runs without the frame host
- `View3DOverlayHost` consumes title-bar drag events locally and does not forward them as keyboard packets

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
        self._timer = context.window_manager.event_timer_add(1.0 / 120.0, window=context.window)
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
