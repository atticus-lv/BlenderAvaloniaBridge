# Blender Integration

Use this path when you already have your own Blender addon and only want to reuse the bridge core.

## What to copy

Copy this directory into your own addon package:

```text
src\blender_extension\avalonia_bridge\core\
```

You do not need to copy the repo-specific shell files, but they can still be used as references.

## Minimal integration snippet

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessEndpoint,
)


controller = BridgeController(
    BridgeConfig(
        executable_path="C:/path/to/YourAvaloniaApp.exe",
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
# run
controller.start()
```

## Transport modes

There are two `window_mode` transport modes:

- `headless`: default mode. Avalonia frames are streamed into the Blender window, usually drawn in the 3D viewport, and mouse/keyboard events are captured inside that region
- `desktop`: classic desktop window mode, with business connection only

## Sizing and render density

The bridge now separates logical size from render density:

- `width` and `height` control the logical Avalonia window size
- `render_scaling` controls how densely the headless frame is rendered before Blender displays it

`render_scaling` is used to keep the UI layout aligned with desktop mode on high-resolution displays. It only applies to headless mode.

In the sample addon UI, this appears as:

- `Display Size`
- `Render Scaling`

The default `render_scaling` is `1.25`.

## Integration points

- Lifecycle: call `start()`, `stop()`, and `tick_once()` from your own operators or runtime adapter
- Input forwarding: call `handle_event(context, event)` from your event pipeline when remote input is enabled
- State sync: use `state_callback`, `state_snapshot()`, or `diagnostics_snapshot()` to feed your own UI
- User-facing controls: expose `width`, `height`, and `render_scaling` together so users can tune layout size and sharpness independently in headless mode
- Capability-aware UX: reflect whether the current session has business only, frame streaming, or input enabled

See [Architecture](../advanced/architecture.md) for the shared session model and capability negotiation flow.
