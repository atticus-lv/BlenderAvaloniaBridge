# Ops

`blenderApi.Ops` is the domain for Blender operators.

Use it when the action should run through `bpy.ops.*`. If you are calling a method on an RNA object, stay on [`blenderApi.Rna`](./rna.md).

## Methods

- `PollAsync(operatorName, BlenderOperatorCall? call = null)`
- `CallAsync(operatorName, params (string Name, object? Value)[] kwargs)`
- `CallAsync(operatorName, BlenderOperatorCall call)`

## Poll Before Execute

```csharp
var canDelete = await blenderApi.Ops.PollAsync("object.delete", ct);

if (canDelete)
{
    await blenderApi.Ops.CallAsync("object.delete", ct);
}
```

## Pass Keyword Arguments

```csharp
await blenderApi.Ops.CallAsync(
    "mesh.primitive_cube_add",
    ("size", 2.0));
```

## Use Context Override

```csharp
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

`BlenderOperatorCall` and `BlenderContextOverride` are described in [Types](./types.md).
