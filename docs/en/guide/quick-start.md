# Quick Start

## 1. Publish the Avalonia Sample

Make sure `.NET 10 SDK` is installed, then run this from the repository root:

```bash
dotnet publish ./src/BlenderAvaloniaBridge.Sample/BlenderAvaloniaBridge.Sample.csproj -c Release -o ./artifacts/publish/net10 --configfile ./NuGet.Config
```

The published bridge files are created at:

```text
artifacts/publish/net10/
```

## 2. Add the local Blender extension repository

Open Blender's extension preferences and use `Add Local Repository` with this directory:

```text
src\blender_extension
```

Then enable the `avalonia_bridge` extension from that repository.

## 3. Point the panel to the bridge binary and start it

After enabling the extension:

1. Open `View3D > Sidebar > AvaloniaBridgeDemo`
2. Set `Avalonia Executable` to the published bridge binary, such as `BlenderAvaloniaBridge.Sample.dll` or a published native executable
3. Adjust `Display Size` and `Render Scaling` if needed
4. Keep `Mode` on `Headless Frames + Input` to use the default Windows/macOS shared-memory frame transport
5. Click `Start UI Bridge`

If everything is wired correctly, the sample UI should appear in the Blender overlay.

## Display Size vs Render Scaling

The Blender panel exposes two related settings:

- `Display Size`: the logical Avalonia window size used for layout and input mapping
- `Render Scaling`: the headless render density multiplier used when Avalonia captures frames

`Render Scaling` defaults to `1.25`.

Use it when the overlay looks slightly soft after Blender scales the captured frame for display. A higher value keeps the same logical UI size, but gives Blender a denser source image to display.

Typical guidance:

- Keep `Display Size` for layout changes
- Keep `Render Scaling` for clarity and DPI matching
- Restart the bridge after changing either value
