# Ops

`blenderApi.Ops` 负责 Blender operator。

当动作本质上对应 `bpy.ops.*` 时，使用它；如果是在 RNA 对象上调用方法，则继续使用 [`blenderApi.Rna`](./rna.md)。

## 方法

- `PollAsync(operatorName, BlenderOperatorCall? call = null)`
- `CallAsync(operatorName, params (string Name, object? Value)[] kwargs)`
- `CallAsync(operatorName, BlenderOperatorCall call)`

## 先 Poll 再执行

```csharp
var canDelete = await blenderApi.Ops.PollAsync("object.delete", ct);

if (canDelete)
{
    await blenderApi.Ops.CallAsync("object.delete", ct);
}
```

## 传递关键字参数

```csharp
await blenderApi.Ops.CallAsync(
    "mesh.primitive_cube_add",
    ("size", 2.0));
```

## 使用 Context Override

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

`BlenderOperatorCall` 和 `BlenderContextOverride` 的说明见 [Types](./types.md)。
