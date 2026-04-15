# Observe

`blenderApi.Observe` owns watch subscriptions and explicit snapshot reads.

It is designed around a lightweight notification model:

1. Blender sends `watch.dirty`.
2. The Avalonia app decides what to refresh.
3. The app reads updated state through `blenderApi.Observe.ReadAsync`, `blenderApi.Rna.GetAsync`, or `blenderApi.Rna.ListAsync`.

## Methods

- `WatchAsync(watchId, source, path, onDirty, cancellationToken)`
- `ReadAsync(watchId, cancellationToken)`

## Start A Watch

```csharp
await using var watch = await blenderApi.Observe.WatchAsync(
    watchId: "materials",
    source: WatchSource.Depsgraph,
    path: "bpy.data.materials",
    onDirty: async _ =>
    {
        var materials = await blenderApi.Rna.ListAsync("bpy.data.materials");
        // Refresh your UI here.
    });
```

## Choose The Refresh Strategy

- If a full list is cheap to reload, call `blenderApi.Rna.ListAsync(...)` again.
- If a detail panel is bound to a known path, call `blenderApi.Rna.GetAsync(...)`.
- If you need a watch snapshot and dirty references together, use `blenderApi.Observe.ReadAsync(...)`.

## Watch Sources

The watch API stays unified under `Observe`. Source selection still uses `WatchSource`:

- `WatchSource.Depsgraph`
- `WatchSource.Frame`
- `WatchSource.Lifecycle`
