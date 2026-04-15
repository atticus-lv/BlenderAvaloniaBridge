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

## Dispatch Semantics

`watch.dirty` is an invalidation signal, not a full event stream.

- `onDirty` callbacks for the same `watchId` never run concurrently
- Dirty events for the same `watchId` are dispatched serially
- If an earlier callback is still running, queued dirty events for that same `watchId` use latest-wins and only the newest revision is delivered next
- Different `watchId` registrations can still make progress in parallel

In practice, `onDirty` should be used to trigger a refresh or read the latest snapshot, not to depend on every intermediate revision being processed one by one.

## Watch Sources

The watch API stays unified under `Observe`. Source selection still uses `WatchSource`:

- `WatchSource.Depsgraph`
- `WatchSource.Frame`
- `WatchSource.Lifecycle`
