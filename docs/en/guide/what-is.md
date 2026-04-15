# What Is Blender Avalonia Bridge

Blender Avalonia Bridge is a toolkit for bringing Avalonia UI into Blender.

- The Avalonia side owns the actual UI, state, and business logic
- The Blender side owns hosting and bridging

That means you do not need to maintain your own Blender GPU-based UI stack, and you do not have to push all UI behavior into Blender panels and Python-only workflows.

## What It Is Not

This is not a "build everything as Blender Python panels" approach.

If you only need a very small Blender panel or utility, a traditional addon is usually simpler.

This bridge is a better fit when Blender acts as the host and bridge layer, while the Avalonia app owns the UI and business logic.

## Run Modes

The bridge currently supports two `window_mode` values:

- `headless`: the default mode. Avalonia frames are drawn inside Blender and mouse or keyboard input is captured inside the active region
- `desktop`: a classic desktop window mode with business connection only

Recommended choice:

- Use `headless` when you want the UI embedded directly inside Blender
- Use `desktop` when you want to validate business connectivity first or build the desktop UI first

### Advantages

#### 1. Better fit for complex UI

You can build desktop-grade UI in Avalonia without maintaining a custom GPU UI layer inside Blender.

#### 2. Better fit for reusing the .NET / Avalonia stack

If you're familiar with Avalonia, C#, and .NET, you can continue to use the frameworks, libraries, and project structure you already know.

#### 3. Better fit for moving complex business logic into a separate process

Keeping business logic on the .NET side is often a better fit for large data processing, complex computation, or existing backend-style modules.

#### 4. Better fit for reducing Python business source distribution

If you do not want to distribute core business logic as Python source, you can compile the Avalonia project as a native program with .NET AOT.

### Known Limitations

- Headless shared-memory bridge currently supports Windows and macOS
- Layout animation is not supported. For example, `SplitView` pane animations switch directly to the final state.
- Transitions-based movement may stutter and jump directly to the final state.
- External drag and drop is not supported because Blender captures the drop event first.

## Next Step

- If you want to run a minimal example first, start with [Quick Start](./quick-start.md)
- If you want to integrate your own Blender extension and Avalonia app, go to the [Integration Guide](../integration/index.md)
