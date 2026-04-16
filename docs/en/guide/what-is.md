# What Is Blender Avalonia Bridge

Blender Avalonia Bridge is a toolkit for bringing Avalonia UI into Blender.

Bridge brings Avalonia into Blender, preserving almost the full Avalonia framework while delivering Blender-native rendering and interaction.

<div class="doc-image-row">
  <figure class="doc-image-card">
    <img src="/statics/images/headlessmode.png" alt="Blender Avalonia Bridge overview">
    <figcaption>Blender-embedded UI in headless mode</figcaption>
  </figure>
  <figure class="doc-image-card">
    <img src="/statics/images/windowmode.png" alt="Blender Avalonia Bridge runtime modes">
    <figcaption>Standalone Avalonia window in desktop mode</figcaption>
  </figure>
</div>

- The Avalonia side owns the actual UI, state, and business logic
- The Blender side owns hosting and bridging

You do not need to maintain your own Blender GPU-based UI stack, and you do not have to push all UI behavior into Blender panels and Python-only workflows.

## What It Is Not

This is not a "build everything as Blender Python panels" approach.

If you only need a very small Blender panel or utility, a traditional addon is simpler.

This bridge fits projects where Blender acts as the host and bridge layer, while the Avalonia app owns the UI and business logic.

### Advantages

#### 1. Better fit for complex UI

You can build desktop-grade UI in Avalonia without maintaining a custom GPU UI layer inside Blender.

#### 2. Better fit for reusing the .NET / Avalonia stack

If you're familiar with Avalonia, C#, and .NET, you can continue to use the frameworks, libraries, and project structure you already know.

#### 3. Better fit for moving complex business logic into a separate process

Keeping business logic on the .NET side fits large data processing, complex computation, or existing backend-style modules.

#### 4. Better fit for reducing Python business source distribution

If you do not want to distribute core business logic as Python source, you can compile the Avalonia project as a native program with .NET AOT.

### Known limitations in headless mode

- Currently supported on Windows and macOS only
- Some UI scenarios may show stutter or dropped frames.
- External drag and drop is not supported because Blender captures the drop event first.

## Next Step

- Run a minimal example: [Quick Start](./quick-start.md)
- Integrate your own Blender extension and Avalonia app: [Integration Overview](../integration/index.md)
