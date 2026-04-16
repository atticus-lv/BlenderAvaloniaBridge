# AGENTS

## Project Model

This repository has four parts:

- `src/BlenderAvaloniaBridge.Core`: Avalonia bridge core
- `src/BlenderAvaloniaBridge.Sample`: Avalonia sample app
- `src/blender_extension/avalonia_bridge/core`: Blender bridge core
- `src/blender_extension/avalonia_bridge`: Blender addon shell

The two core layers already handle most bridge infrastructure: transport, session, frame delivery, input forwarding, and business messaging.

Integration usually focuses on:

- Avalonia UI and business logic
- Blender addon configuration and business-side wiring

## Runtime Modes

- `headless`: `frames + input + business`
- `desktop`: `business` only

`View3DOverlayHost` is only used for the Blender `3D View` host path in `headless` mode.

## Recommended Reading Order

1. `docs/en/guide/what-is.md`
2. `docs/en/guide/how-it-works.md`
3. `docs/en/integration/index.md`
4. `docs/en/api/index.md`

## Repo Map

- `src/BlenderAvaloniaBridge.Core/`: C# bridge runtime and `BlenderApi`
- `src/BlenderAvaloniaBridge.Sample/`: Avalonia sample application
- `src/blender_extension/avalonia_bridge/core/`: Blender-side controller, host, transport, and business bridge
- `docs/`: VitePress documentation in Chinese and English
- `tests/BlenderAvaloniaBridge.Tests/`: .NET tests
- `tests/blender_extension/avalonia_bridge/`: Blender-side Python tests

## Key Entry Points

- Avalonia sample entry: `src/BlenderAvaloniaBridge.Sample/Program.cs`
- C# API root: `src/BlenderAvaloniaBridge.Core/BlenderApi.cs`
- Blender controller: `src/blender_extension/avalonia_bridge/core/controller.py`
- Optional View3D host: `src/blender_extension/avalonia_bridge/core/view3d_overlay_host.py`
- Default business endpoint: `src/blender_extension/avalonia_bridge/core/business.py`

## API Assumptions

- The built-in C# `BlenderApi` currently assumes Blender-side compatibility with:
  - `rna.*`
  - `ops.*`
  - `watch.*`
- Replacing `DefaultBusinessEndpoint` with a custom endpoint breaks the built-in `BlenderApi` unless those names remain compatible.

## Editing Constraints

- Keep Blender-side core generic. Do not move sample-specific business fields or sample-only semantics back into core.
- `View3DOverlayHost` is optional composition around controller behavior, not the controller itself.
- Prefer keeping docs concise and factual. Avoid descriptive filler, explanatory throat-clearing, and recommendation-heavy prose.
- Keep integration docs split by concern:
  - `docs/*/integration/index.md`: overview and shared configuration
  - `docs/*/integration/avalonia.md`: Avalonia-side integration
  - `docs/*/integration/blender.md`: Blender-side integration

## Verification

- Docs build:
  - `npm run docs:build` in `docs/`
- .NET sample AOT publish profiles:
  - `dotnet publish ./src/BlenderAvaloniaBridge.Sample/BlenderAvaloniaBridge.Sample.csproj -c Release /p:PublishProfile=aot-win-x64 --configfile ./NuGet.Config`
  - `dotnet publish ./src/BlenderAvaloniaBridge.Sample/BlenderAvaloniaBridge.Sample.csproj -c Release /p:PublishProfile=aot-osx-arm64 --configfile ./NuGet.Config`
- Blender-side Python tests:
  - `py -3 -m unittest discover -s tests/blender_extension/avalonia_bridge -p "test_*.py"`
