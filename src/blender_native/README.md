# Blender Native GPU Hook

This helper is loaded by the Blender addon through `ctypes`.

On macOS it resolves Blender's private Metal GPU texture symbols at runtime, looks up an
Avalonia-exported `IOSurfaceID`, creates a Metal texture view for that IOSurface, and blits it into
a Python-created `gpu.types.GPUTexture`.

The hook is intentionally small and self-diagnosing because it depends on Blender's private GPU ABI.
Callers can inspect `ava_blender_native_status_json()` when symbol resolution or Metal import fails.

Build:

```sh
cmake -S src/blender_native -B src/blender_native/build
cmake --build src/blender_native/build --config Release
```

The default output directory is `src/blender_extension/avalonia_bridge/native`, where the addon
searches first.
