# C# API Usage Guide

This page explains how an Avalonia app can use the C# bridge API after the Blender-side and Avalonia-side integration is already connected.

If you have not wired both sides together yet, start with the [Integration Guide](./index.md).

## Root API

The bridge now exposes a root `BlenderApi` with explicit domain properties:

- `blenderApi.Rna`: RNA path reads, writes, descriptions, and RNA method calls
- `blenderApi.Ops`: Blender operator polling and execution
- `blenderApi.Observe`: watch subscription and snapshot reads

The wire protocol names are unchanged:

- `rna.list`: list child item references under a Blender collection path
- `rna.get`: read the current value at a path
- `rna.set`: write the current value at a path
- `rna.describe`: inspect type, readonly state, and other property metadata for a path
- `rna.call`: call a method on an RNA object
- `ops.poll`: check whether an operator can run in the current context
- `ops.call`: execute an operator
- `watch.subscribe`: subscribe to change notifications for a path
- `watch.unsubscribe`: remove a watch subscription
- `watch.read`: read the current snapshot for a watch on demand

`Data` is intentionally reserved for a future resource-oriented API and is not part of the current release.

## API overview

The public C# surface is easiest to read by domain:

```csharp
blenderApi.Rna.ListAsync / GetAsync<T> / SetAsync<T> / DescribeAsync / CallAsync<T>
blenderApi.Ops.PollAsync / CallAsync
blenderApi.Observe.WatchAsync / ReadAsync
```

- `Rna` covers path listing, reading, writing, description, and RNA method calls
- `Ops` covers Blender operator poll and execute flows
- `Observe` covers watch subscription and snapshot reads

Recommended usage

- Use `blenderApi.Rna.ListAsync` for list entry points
- Use `blenderApi.Rna.GetAsync<T>` / `blenderApi.Rna.SetAsync<T>` for field reads and writes
- Use `blenderApi.Rna.CallAsync<T>` for RNA method calls
- Use `blenderApi.Ops.PollAsync` + `blenderApi.Ops.CallAsync` for operators
- Use `blenderApi.Observe.WatchAsync` for change notifications

## What `RnaItemRef` represents

`blenderApi.Rna.ListAsync` returns addressable `RnaItemRef` values. Common stable fields include:

1. Blender-side RNA / ID properties

   - `Name`

   - `RnaType`

   - `IdType`

   - `SessionUid`

2. bridge internal handling

   - `Path` reference information

   - `Label` reference information

   - `Kind` fixed type marker

Treat them as reference handles for follow-up reads and writes, not as fully expanded business objects.

You can continue using `Path` for:

- child-property reads: `$"{item.Path}.name"`
- child-property writes: `$"{item.Path}.location"`
- object references inside operator context overrides

For example, if an object list page also needs `type` or current active-object state, fetch those explicitly through the generic API:

```csharp
var items = await blenderApi.Rna.ListAsync("bpy.context.scene.objects", ct);
var activeObject = await blenderApi.Rna.GetAsync<RnaItemRef>("bpy.context.active_object", ct);

foreach (var item in items)
{
    var objectType = await blenderApi.Rna.GetAsync<string>($"{item.Path}.type", ct);
    var isActiveObject = item.Path == activeObject.Path;
}
```

The bridge core exposes generic business primitives by default and does not pre-populate sample-specific summary fields for list items.

## Data access

Recommended usage stays close to Blender's Python API:

```csharp
public sealed class MaterialService
{
    private readonly BlenderApi _blenderApi;

    public MaterialService(BlenderApi blenderApi)
    {
        _blenderApi = blenderApi;
    }

    public Task<IReadOnlyList<RnaItemRef>> ListMaterialsAsync(CancellationToken ct = default)
        => _blenderApi.Rna.ListAsync("bpy.data.materials", ct);

    public Task<string> GetMaterialNameAsync(string name, CancellationToken ct = default)
        => _blenderApi.Rna.GetAsync<string>($"bpy.data.materials[\"{name}\"].name", ct);

    public Task RenameMaterialAsync(string name, string newName, CancellationToken ct = default)
        => _blenderApi.Rna.SetAsync($"bpy.data.materials[\"{name}\"].name", newName, ct);

    public Task<RnaItemRef> CreateMaterialAsync(string newName, CancellationToken ct = default)
        => _blenderApi.Rna.CallAsync<RnaItemRef>(
            "bpy.data.materials",
            "new",
            ("name", newName));
}
```

If you start from an `RnaItemRef` list, field details usually continue through `item.Path` with `blenderApi.Rna.GetAsync<T>` / `blenderApi.Rna.SetAsync<T>`.

## Operator calls

- Use `blenderApi.Rna.CallAsync<T>` for RNA object methods
- Use `blenderApi.Ops.PollAsync` and `blenderApi.Ops.CallAsync` for Blender operators

Operator calls use tuple kwargs and optional strongly typed context override data instead of anonymous objects:

```csharp
await blenderApi.Ops.CallAsync(
    "mesh.primitive_cube_add",
    ("size", 2.0));

await blenderApi.Ops.CallAsync(
    "object.duplicate_move",
    new BlenderOperatorCall
    {
        ContextOverride = new BlenderContextOverride
        {
            ActiveObject = "bpy.data.objects[\"Cube\"]",
            SelectedObjects = new[] { "bpy.data.objects[\"Cube\"]" },
        }
    });
```

## Watch subscriptions

`blenderApi.Observe.WatchAsync` uses lightweight socket events plus explicit reads. Blender sends `watch.dirty`, and your app decides when to call `blenderApi.Observe.ReadAsync`, `blenderApi.Rna.GetAsync`, or `blenderApi.Rna.ListAsync` again.

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

If you only need to reload a full list, calling `blenderApi.Rna.ListAsync(...)` again is usually enough.

If you need to keep a detail view in sync, the usual follow-up is path-based `blenderApi.Rna.GetAsync(...)`.

If you need more granular page state updates, combine `dirtyRefs` and `blenderApi.Observe.ReadAsync(...)` to control refresh scope yourself.

## Value model notes

Supported value models:

- scalars: `bool`, `int`, `long`, `double`, `string`
- arrays: `bool[]`, `int[]`, `long[]`, `double[]`, `string[]`
- RNA references: `RnaItemRef`
- `null`

For custom DTOs used with `GetAsync<T>`, `SetAsync<T>`, or `CallAsync<T>`, register your own source-generated resolver at startup.

If type information is missing, runtime deserialization fails with `missing_json_type_info_for_type`.

If you need full Blender-side freedom, keep the default endpoint for standard RNA / operator traffic and move application-specific commands into a separate custom business endpoint.
