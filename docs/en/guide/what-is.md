# What Is Blender Avalonia Bridge

Blender Avalonia Bridge is a componentized bridge that lets you use the Avalonia framework seamlessly inside Blender.

- the Avalonia side owns the actual UI, state, and business logic
- the Blender side owns embedding, input forwarding, and business command bridging

That means you do not need to maintain your own Blender GPU-based UI stack, and you do not have to push all UI behavior into Blender panels and Python-only workflows.

## Good fit

- developers who want more capable UI for Blender tools
- teams that want to reuse the .NET ecosystem and Avalonia experience
- projects that prefer more business logic to live in a separate executable

## Not a good fit

- very small addons that only need a simple Blender panel
- teams that do not want to maintain both Python and C#
- projects that require all logic to live inside Blender's Python process


## Advantage

### 1. No need to build your own Blender GPU UI framework

This bridge allows you to build UI in Avalonia without having to maintain a custom GPU-driven UI layer in Blender.

### 2. Use. .NET existing ecosystem

If you're familiar with Avalonia, C#, and .NET, you can continue to use the frameworks, libraries, and engineering organizations you're familiar with

### 3. Performance

Using dotnet for your external business logic will perform better than pure Python, especially if you need to process large amounts of data or complex calculations.

### 4. AOT compiles business code and avoids distributing Python source code

If you don't want to distribute the core business code directly as Python source code, you can use .NET AOT to compile the Avalonia project as a native program. This approach is different from compiling Python to `pyd` / `pyc` and does not pose the risk of distributing Blender GPL code.

## Known Limitations

- Currently only supported on the Windows platform
- Avalonia's layout animation is not supported: for example, the pane animation effect of SplitView will be changed to a direct switching state
- No external drag support: Blender will catch the drop event, making it impossible to transfer data to Avalonia