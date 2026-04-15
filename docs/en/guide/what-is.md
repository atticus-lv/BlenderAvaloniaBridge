# What Is Blender Avalonia Bridge

Blender Avalonia Bridge is a toolkit for bringing Avalonia UI into Blender.

- The Avalonia side owns the actual UI, state, and business logic
- The Blender side owns embedding, input forwarding, and business command bridging

That means you do not need to maintain your own Blender GPU-based UI stack, and you do not have to push all UI behavior into Blender panels and Python-only workflows.

## Modes

The bridge currently supports two `window_mode` values:

- `headless`: the default mode. Avalonia frames are drawn inside Blender and mouse or keyboard input is captured inside the active region
- `desktop`: a classic desktop window mode with business connection only

## Good fit

- developers who want more capable UI for Blender tools
- teams that want to reuse the .NET ecosystem and Avalonia experience
- projects that prefer more business logic to live in a separate executable

## Not a good fit

- very small addons that only need a simple Blender panel
- teams that do not want to maintain both Python and C#
- projects that require all logic to live inside Blender's Python process

## Advantages

### 1. No need to build your own Blender GPU UI framework

This bridge allows you to build UI in Avalonia without having to maintain a custom GPU-driven UI layer in Blender.

### 2. Reuse the .NET ecosystem

If you're familiar with Avalonia, C#, and .NET, you can continue to use the frameworks, libraries, and project structure you already know.

### 3. Performance

Moving external business logic to .NET is often a better fit than pure Python when you need to process large amounts of data or more complex calculations.

### 4. AOT compiles business code and avoids distributing Python source code

If you do not want to distribute core business logic as Python source, you can compile the Avalonia project as a native program with .NET AOT.

## Known Limitations

- Headless shared-memory bridge currently supports Windows and macOS
- Headless mode currently has these rendering limits:
- Layout animation is not supported. For example, `SplitView` pane animations switch directly to the final state.
- Transitions-based movement may stutter and jump directly to the final state.
- External drag and drop is not supported because Blender captures the drop event first.
