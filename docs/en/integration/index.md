# Integration Overview

If you only want to run the sample included in this repository, start with [Quick Start](../guide/quick-start.md).

## Integration boundary

A complete integration has two parts:

- The Avalonia app owns the UI, state, and business logic and runs as the bridge process
- The Blender addon starts the bridge, draws the overlay, forwards input, and hosts the business channel

Typical integration flow:

1. Configure `BridgeConfig` on the Blender side
2. Update the Avalonia app entry point to support bridge startup
3. Assemble `BridgeController` with an optional `View3DOverlayHost` on the Blender side
4. Drive `tick_once()` and `handle_event(context, event)` from a modal operator

## Shared configuration

The following values are provided on the Blender-side Python runtime when creating `BridgeConfig`.

- `executable_path`: path to the Avalonia app executable. The Blender side starts the bridge process from this path.
- `window_mode`: runtime mode. `headless` enables `frames + input + business`, while `desktop` establishes `business` only.
- `width` and `height`: logical Avalonia window size.
- `render_scaling`: render density used in headless mode to improve sharpness.

`executable_path` can point to a Debug or Release executable during development. For published builds, use an AOT executable. The path should be visible to the Blender process and match the current platform, such as a Windows `.exe` or the executable inside a macOS app bundle.

`window_mode`, `width`, `height`, and `render_scaling` affect the Avalonia side through bridge startup parameters.

## Core vs Sample / Addon

- `BlenderAvaloniaBridge.Core` and Blender-side `avalonia_bridge/core` handle bridge infrastructure: process, transport, frame, input, and business
- `BlenderAvaloniaBridge.Sample` and the Blender addon shell handle sample UI, configuration assembly, and business code
- Integration usually does not require changes to either core layer and mainly happens in the Avalonia app layer and Blender addon layer

## By side

- Avalonia-side integration: see [Avalonia-side integration](./avalonia.md)
- Blender-side integration: see [Blender-side integration](./blender.md)

## Related sections

- For the shared session model and runtime flow, see [How It Works](../guide/how-it-works.md)
- For C# API usage, see the [API section](../api/index.md)
