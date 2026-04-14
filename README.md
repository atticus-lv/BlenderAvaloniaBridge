# Blender Avalonia Bridge

Windows-first toolkit for running an Avalonia UI in a separate process, streaming frames into Blender, and sending Blender input back to Avalonia.

[中文文档](./docs/zh-CN/index.md) | [English Docs](./docs/en/index.md)

## Documentation Site

The project now ships a VitePress documentation site under [`docs/`](./docs/).

Run it locally:

```powershell
cd .\docs
npm install
npm run docs:dev
```

Build the static site:

```powershell
cd .\docs
npm run docs:build
```

Main entry points:

- [Overview](./docs/en/guide/what-is.md)
- [简介](./docs/zh-CN/guide/what-is.md)
- [Quick Start](./docs/en/guide/quick-start.md)
- [Blender Integration](./docs/en/integration/blender.md)
- [Avalonia Integration](./docs/en/integration/avalonia.md)
- [Project Architecture](./docs/en/advanced/architecture.md)

## Repo Layout

```text
src/
  BlenderAvaloniaBridge.Core/
  BlenderAvaloniaBridge.Sample/
  blender_extension/
    avalonia_bridge/
      core/
tests/
  BlenderAvaloniaBridge.Tests/
  blender_extension/avalonia_bridge/
docs/
  .vitepress/
  zh-CN/
  en/
```

## Quick Start

The fastest end-to-end path is:

1. Publish the sample as AOT from `src/BlenderAvaloniaBridge.Sample`
2. Add `src/blender_extension` as a local Blender extension repository
3. Enable `avalonia_bridge`
4. Point the panel to the published exe and click `Start UI Bridge`

See the full walkthrough in [Quick Start](./docs/en/guide/quick-start.md).

## What It Is

Blender Avalonia Bridge is a componentized way to use Avalonia inside Blender.

It is a strong fit for:

- teams that want a distinctive UI without maintaining a Blender GPU drawing stack
- Avalonia / .NET users who want to reuse the broader .NET ecosystem
- teams that prefer compiling business code into a native AOT executable instead of shipping Python source
- teams that are comfortable maintaining both Python and C#

## Known Limits

- Windows-first
- Shared memory path is Windows-only
- Fixed bridge size per launch
- No IME / clipboard / drag-drop
- Blender background mode is not suitable for GPU overlay testing
