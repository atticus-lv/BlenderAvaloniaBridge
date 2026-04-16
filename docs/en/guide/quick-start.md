# Quick Start

This page only covers how to run the sample that ships with this repository.

For integration, see [Integration Overview](../integration/index.md).

## 1. Publish the Avalonia Sample

Make sure `.NET 10 SDK` is installed, then run this from the repository root:

```bash
dotnet publish ./src/BlenderAvaloniaBridge.Sample/BlenderAvaloniaBridge.Sample.csproj -c Release -o ./artifacts/publish/net10 --configfile ./NuGet.Config
```

The generated bridge folder is created at:

```text
artifacts/publish/net10/
```

This command produces a regular publish output, not an AOT publish output.

## 2. Add the local Blender extension repository

Open Blender's extension preferences and use `Add Local Repository` with this directory:

```text
src\blender_extension
```

Then enable the `avalonia_bridge` extension from that repository.

## 3. Point the panel to the bridge program and start it

After enabling the extension:

1. Open `View3D > Sidebar > AvaloniaBridgeDemo`
2. Set `Avalonia Executable` to the executable file of the bridge program you just published
3. Adjust `Display Size` and `Render Scaling` if needed
4. Click `Start UI Bridge`

If everything works correctly, the sample UI should appear in the Blender overlay.
