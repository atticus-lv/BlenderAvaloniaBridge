# Blender Integration

Use this path when you already have your own Blender addon and only want to reuse the bridge core.

## What to copy

Copy this directory into your own addon package:

```text
src\blender_extension\avalonia_bridge\core\
```

You do not need the repo-specific shell files:

- `src/blender_extension/avalonia_bridge/panel.py`
- `src/blender_extension/avalonia_bridge/operators.py`
- `src/blender_extension/avalonia_bridge/preferences.py`
- `src/blender_extension/avalonia_bridge/properties.py`
- `src/blender_extension/avalonia_bridge/runtime.py`

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
        host="127.0.0.1",
        show_overlay_debug=False,
    ),
    business_endpoint=DefaultBusinessEndpoint(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)
```

## Sizing and render density

The bridge now separates logical size from render density:

- `width` and `height` control the logical Avalonia window size
- `render_scaling` controls how densely the headless frame is rendered before Blender displays it

This is useful when Blender and Avalonia do not line up perfectly on DPI assumptions. Instead of increasing `width` and `height` just to make the overlay look sharper, prefer raising `render_scaling` so layout stays stable while the captured frame gains more pixels.

In the sample addon UI, this appears as:

- `Display Size`
- `Render Scaling`

The default `render_scaling` is `1.25`.

## The usual integration points

- Lifecycle: call `start()`, `stop()`, and `tick_once()` from your own operators or runtime adapter
- Input forwarding: call `handle_event(context, event)` from your event pipeline
- State sync: use `state_callback`, `state_snapshot()`, or `diagnostics_snapshot()` to feed your own UI
- User-facing controls: expose `width`, `height`, and `render_scaling` together so users can tune layout size and sharpness independently

## Recommended split

Keep your addon in two layers:

- your addon shell for panels, operators, preferences, and property groups
- the copied `core/` package for process management, transport, frame flow, overlay, and business bridging

That keeps the reusable bridge internals independent from your addon-specific UI code.
