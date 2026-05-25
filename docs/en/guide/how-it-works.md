# How It Works

## Overall structure

```mermaid
flowchart TB
    A["Blender Addon"] <--> B["BridgeController"]
    B <--> C["Avalonia Bridge Process"]

    C --> D["Custom Offscreen Avalonia Backend"]
    C --> E["Desktop UI Session"]
    C --> H["Business Bridge"]

    D --> F["Avalonia UI"]
    D --> G["Stable Frame Pump"]
    G --> I["Frame Transport"]
    I --> J["View3DOverlayHost"]
    J --> K["Blender 3D View Overlay"]

    K --> L["Input Forwarding"]
    L --> B
    F <--> H
    E <--> H
```

## Runtime flow

```mermaid
sequenceDiagram
    participant Blender as Blender Addon
    participant Core as BridgeController
    participant Bridge as Avalonia Bridge Process
    participant Backend as Custom Offscreen Avalonia Backend
    participant Pump as Stable Frame Pump
    participant Host as View3DOverlayHost
    participant UI as Avalonia UI

    Blender->>Core: create BridgeConfig
    Blender->>Core: start()
    Core->>Bridge: launch executable_path
    Bridge->>Backend: initialize offscreen backend
    Backend->>UI: create logical window
    Bridge->>Core: init ack

    Blender->>Host: input [offscreen]
    Host->>Core: forward input [offscreen]
    Core->>Bridge: input packet [offscreen]
    Bridge->>Backend: dispatch input [offscreen]
    Backend->>UI: apply input [offscreen]

    UI->>Bridge: business request
    Bridge->>Core: business_request
    Core-->>Bridge: business_response
    Bridge-->>UI: deliver response

    Backend->>Pump: mark active / dirty
    Pump->>Backend: capture at TargetFps
    Backend-->>Bridge: frame or external frame handle
    Bridge-->>Core: frame packet [offscreen]
    Core-->>Host: store latest frame / tag redraw
    Host-->>Blender: draw latest overlay [offscreen]
```

## Offscreen frame flow

In offscreen mode, the bridge process owns a custom Avalonia backend instead of a desktop window. The backend creates the logical Avalonia window, dispatches input, and feeds a stable frame pump. Input and UI invalidation mark the UI as active, but Blender does not request frames faster than the configured cadence.

The current startup value for this mode is still `window_mode="headless"` for compatibility.

Frame flow:

1. Avalonia applies input or business state.
2. The bridge frame pump publishes frames at `TargetFps` while the UI is active.
3. Blender receives frame metadata or pixels on a background socket thread.
4. Blender stores only the latest frame.
5. `View3DOverlayHost.tick_once()` presents the latest frame during the modal timer.

On macOS, the preferred path uses IOSurface metadata and a Blender-side Metal texture copy. Shared memory and inline frame payloads remain fallback paths.

## Sizing

- `width` and `height` are Avalonia logical window dimensions.
- `render_scaling` controls render density and the Blender overlay display scale in offscreen mode.
- Input coordinates are mapped back to the logical `width` and `height`.
- If the `3D View` area is smaller than the requested overlay size, the overlay is fitted into the available region.
