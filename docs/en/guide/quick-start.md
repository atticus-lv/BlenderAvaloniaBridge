# Quick Start

## 1. Publish the Avalonia Sample as AOT

Make sure `.NET 10 SDK` is installed, then run this from the repository root:

```powershell
dotnet publish .\src\BlenderAvaloniaBridge.Sample\BlenderAvaloniaBridge.Sample.csproj -c Release -r win-x64 -p:PublishAot=true -o .\artifacts\publish\aot-net10 --configfile .\NuGet.Config
```

The output executable is created at:

```text
artifacts\publish\aot-net10\BlenderAvaloniaBridge.Sample.exe
```

## 2. Add the local Blender extension repository

Open Blender's extension preferences and use `Add Local Repository` with this directory:

```text
src\blender_extension
```

Then enable the `avalonia_bridge` extension from that repository.

## 3. Point the panel to the AOT exe and start it

After enabling the extension:

1. Open `View3D > Sidebar > RenderBuilder`
2. Set `Avalonia Executable` to the published AOT exe
3. Click `Start UI Bridge`

If everything is wired correctly, the sample UI should appear in the Blender overlay.
