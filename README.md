# Blender 5.0 + Avalonia Offscreen Bridge MVP

This repository contains a minimal working prototype that connects a Blender 5.0 addon to a separate Avalonia headless UI process.

Top-level deliverables:

- `blender_addon/`
- `avalonia/`

The design goal is correctness first:

- Blender launches the Avalonia process from a user-specified path.
- Avalonia renders offscreen as a separate process.
- Avalonia sends raw `BGRA8` frames over localhost TCP.
- Blender receives frames, converts them to a Blender image, and draws them in `VIEW_3D` with a `POST_PIXEL` handler.
- Blender captures mouse, wheel, key, and text input through a modal operator and forwards it back to Avalonia.
- The Blender launcher now passes an explicit `--bridge true` flag so the same executable can reliably distinguish bridge mode from desktop-window mode.
- The same Avalonia executable can also launch as a normal desktop window when started without bridge arguments.

## Project Structure

```text
blender_addon/
  __init__.py
  preferences.py
  properties.py
  operators.py
  panel.py
  runtime.py
  process_manager.py
  transport.py
  frame_store.py
  image_bridge.py
  draw_overlay.py
  input_mapper.py

avalonia/
  BlenderAvaloniaBridge.slnx
  Directory.Build.props
  NuGet.Config
  src/BlenderAvaloniaBridge/
    App.axaml
    App.axaml.cs
    Program.cs
    Views/MainView.axaml
    Views/MainView.axaml.cs
    ViewModels/MainViewModel.cs
    Bridge/
  tests/BlenderAvaloniaBridge.Tests/
```

## Requirements

- Windows
- Blender 5.0
- .NET 8 SDK

## Communication Protocol

The bridge uses one localhost TCP connection.

Packet format:

1. `uint32 little-endian`: JSON header length
2. `uint32 little-endian`: binary payload length
3. UTF-8 JSON header
4. optional payload

Supported control message types:

- `init`
- `resize`
- `pointer_move`
- `pointer_down`
- `pointer_up`
- `wheel`
- `key_down`
- `key_up`
- `text`
- `focus`
- `frame`
- `error`

## Why Raw BGRA8 Instead Of PNG

This MVP uses raw `BGRA8` frames instead of PNG.

Tradeoff:

- Simpler and closer to future texture-upload optimization.
- More bandwidth and more CPU work on the Blender side because bytes must be converted into Blender RGBA float pixels.

For an `800x600` frame, the payload is `1,920,000` bytes.

## Build And Publish Avalonia

Run from the repository root.

Debug build:

```powershell
$env:DOTNET_CLI_HOME='E:\blender_ava_demo\.dotnet'
dotnet build .\avalonia\src\BlenderAvaloniaBridge\BlenderAvaloniaBridge.csproj -c Debug --configfile .\avalonia\NuGet.Config
```

Debug executable path:

```text
avalonia\src\BlenderAvaloniaBridge\bin\Debug\net8.0\BlenderAvaloniaBridge.exe
```

Release build:

```powershell
$env:DOTNET_CLI_HOME='E:\blender_ava_demo\.dotnet'
dotnet build .\avalonia\src\BlenderAvaloniaBridge\BlenderAvaloniaBridge.csproj -c Release --configfile .\avalonia\NuGet.Config
```

Release publish:

```powershell
$env:DOTNET_CLI_HOME='E:\blender_ava_demo\.dotnet'
dotnet publish .\avalonia\src\BlenderAvaloniaBridge\BlenderAvaloniaBridge.csproj -c Release -r win-x64 --self-contained false -o .\avalonia\artifacts\publish\release --configfile .\avalonia\NuGet.Config
```

Release executable path:

```text
avalonia\artifacts\publish\release\BlenderAvaloniaBridge.exe
```

Native AOT publish:

```powershell
$env:DOTNET_CLI_HOME='E:\blender_ava_demo\.dotnet'
dotnet publish .\avalonia\src\BlenderAvaloniaBridge\BlenderAvaloniaBridge.csproj -c Release -r win-x64 -p:PublishAot=true -o .\avalonia\artifacts\publish\aot --configfile .\avalonia\NuGet.Config
```

AOT executable path:

```text
avalonia\artifacts\publish\aot\BlenderAvaloniaBridge.exe
```

Optional DLL path:

```text
avalonia\src\BlenderAvaloniaBridge\bin\Debug\net8.0\BlenderAvaloniaBridge.dll
```

The Blender addon supports both `.exe` and `.dll`. If you point it at a `.dll`, the addon launches it with `dotnet`.

## Desktop Window Mode

The same executable supports two launch modes:

- No `--host/--port`: opens as a normal Avalonia desktop window.
- With `--bridge true --host/--port`: runs in Blender bridge mode and connects back to Blender.

Examples:

```powershell
.\avalonia\src\BlenderAvaloniaBridge\bin\Debug\net8.0\BlenderAvaloniaBridge.exe
.\avalonia\artifacts\publish\release\BlenderAvaloniaBridge.exe
.\avalonia\artifacts\publish\aot\BlenderAvaloniaBridge.exe
```

When launched this way, the app opens the same button/TextBox/scroll/status UI in a standard desktop window.

## Install The Blender Addon

Repository note:

- The source addon was originally authored under the requested `blender_addon/` layout.
- For Blender 5 extension-style installation, the installed folder can be renamed to `avalonia_bridge/` and include `blender_manifest.toml`, which is the layout currently used in this workspace.

Option 1:

- Zip the `blender_addon/` folder contents.
- In Blender 5.0 open `Edit > Preferences > Add-ons > Install from Disk`.
- Select the zip and enable `RenderBuilder Avalonia Bridge`.

Option 2:

- Copy `blender_addon/` into Blender's add-ons directory.
- Restart Blender and enable the addon.

## Configure The Avalonia Executable Path In Blender

There are two places to set it:

1. Add-on preferences:
   `Edit > Preferences > Add-ons > RenderBuilder Avalonia Bridge`
2. 3D View sidebar:
   `View3D > Sidebar > RenderBuilder`

Field name:

- `Avalonia Path`

Accepted path types:

- Debug executable
- Release executable
- AOT executable
- Debug or Release DLL

If the path is missing, invalid, or points to an unsupported file type, the addon shows a clear error in the panel and in Blender operator reports.

## Start The Demo

1. Build or publish one of the Avalonia outputs above.
2. Install and enable the addon in Blender 5.0.
3. In the `RenderBuilder` sidebar panel, set `Avalonia Path`.
4. Click `Start UI Bridge`.
5. Wait for the connection status to change to `Connected`.
6. Click inside the overlay to enter capture mode.
7. Try:
   - move the mouse
   - click the button
   - type in the text box
   - use the mouse wheel inside the scroll area
8. Click outside the overlay or press `Esc` to release focus.
9. Click `Stop UI Bridge` to stop the process and overlay.

## Current UI Behavior

The Avalonia UI includes:

- one button
- one `TextBox`
- one `ScrollViewer`
- one status text field

The status text updates with recent input, for example:

- `MouseMove 120,45`
- `Clicked Button (1)`
- `Typed: a`

## Verification Performed

The following were run successfully in this workspace:

- `dotnet test` for `avalonia/tests/BlenderAvaloniaBridge.Tests`
- `dotnet build -c Debug`
- `dotnet build -c Release`
- `dotnet publish -c Release -r win-x64 --self-contained false`
- `dotnet publish -c Release -r win-x64 -p:PublishAot=true`

Runtime handshake verified:

- Debug executable connected and returned `init` ack plus first `frame`
- Release executable connected and returned `init` ack plus first `frame`
- Native AOT executable connected and returned `init` ack plus first `frame`

## Known Limitations

- This is a Windows-first MVP.
- Blender-side automated tests were not run in this workspace because standalone Python/Blender Python is not available on PATH here.
- The addon uses a simple image update path and redraw overlay, not optimized GPU texture streaming.
- The underlying bridge framebuffer remains fixed-size for this MVP, but the displayed overlay is now centered in the active `VIEW_3D` region and auto-fitted with preserved aspect ratio.
- Input capture currently focuses on left mouse, wheel, keyboard press/release, and direct text input only.
- IME composition, clipboard integration, drag/drop, and advanced key-layout handling are not implemented.
- The addon assumes Blender 5.0 APIs for `gpu`, draw handler usage, and modal input.

## Assumptions

- Blender version target is fixed at 5.0.
- The Avalonia bridge runs as a separate child process and is never embedded into Blender.
- `localhost TCP + raw BGRA8` is preferred over PNG for first-pass correctness.
