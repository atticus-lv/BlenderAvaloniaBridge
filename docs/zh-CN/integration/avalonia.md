# C# API 使用指南

本页说明 Avalonia 程序在 bridge 已经接通之后，如何通过 C# API 访问 Blender 数据和 operator。

如果你还没有完成 Blender 侧与 Avalonia 侧的整体接线，请先看[集成指南](./index.md)。

## 根 API

bridge 现在对外暴露一个根对象 `BlenderApi`，并按领域拆成三个入口：

- `blenderApi.Rna`：RNA 路径读写、描述和 RNA 方法调用
- `blenderApi.Ops`：Blender operator 的 poll 与调用
- `blenderApi.Observe`：watch 订阅与快照读取

底层协议名保持不变：

- `rna.list`：列出某个 Blender collection 路径下的子项引用
- `rna.get`：读取某个路径当前值
- `rna.set`：写入某个路径当前值
- `rna.describe`：读取路径对应属性的类型与只读等描述信息
- `rna.call`：调用 RNA 对象上的方法
- `ops.poll`：检查某个 operator 在当前上下文是否可执行
- `ops.call`：执行某个 operator
- `watch.subscribe`：订阅某个路径的变更通知
- `watch.unsubscribe`：取消 watch 订阅
- `watch.read`：按需读取某个 watch 的当前快照

`Data` 这一层会为后续资源型 API 预留，但本次版本不会先暴露空命名空间。

## 接口概览

对外的 C# 接口更适合按领域理解：

```csharp
blenderApi.Rna.ListAsync / GetAsync<T> / SetAsync<T> / DescribeAsync / CallAsync<T>
blenderApi.Ops.PollAsync / CallAsync
blenderApi.Observe.WatchAsync / ReadAsync
```

- `Rna` 负责路径列举、读取、写入、描述和 RNA 方法调用
- `Ops` 负责 Blender operator 的 poll 与执行
- `Observe` 负责 watch 订阅和快照读取

推荐用法

- 列表入口用 `blenderApi.Rna.ListAsync`
- 字段读写用 `blenderApi.Rna.GetAsync<T>` / `blenderApi.Rna.SetAsync<T>`
- RNA 方法调用用 `blenderApi.Rna.CallAsync<T>`
- operator 用 `blenderApi.Ops.PollAsync` + `blenderApi.Ops.CallAsync`
- 变更监听用 `blenderApi.Observe.WatchAsync`

## `RnaItemRef` 的定位

`blenderApi.Rna.ListAsync` 返回的是一组可继续寻址的 `RnaItemRef`。常用稳定字段包括：

1.  Blender 自身的 RNA / ID 属性

   - `Name`

   - `RnaType`

   - `IdType`

   - `SessionUid`


2. bridge 内部处理

    - `Path` 引用信息

    - `Label` 引用信息

    - `Kind `固定写入的类型标记

推荐把它理解成“后续读取和写入的入口引用”，而不是已经带齐所有业务字段的展开对象。

`Path` 可继续用于：

- 读取子属性：`$"{item.Path}.name"`
- 写入子属性：`$"{item.Path}.location"`
- 作为 operator context override 中的对象引用

例如对象列表页如果还需要 `type` 或当前 active object 状态，建议继续使用原始 API 显式读取：

```csharp
var items = await blenderApi.Rna.ListAsync("bpy.context.scene.objects", ct);
var activeObject = await blenderApi.Rna.GetAsync<RnaItemRef>("bpy.context.active_object", ct);

foreach (var item in items)
{
    var objectType = await blenderApi.Rna.GetAsync<string>($"{item.Path}.type", ct);
    var isActiveObject = item.Path == activeObject.Path;
}
```

bridge core 默认暴露的是通用业务能力，不会为 sample UI 预置专用摘要字段。

## 数据读写

推荐用法：贴近 Blender Python API：

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

如果你拿到的是 `RnaItemRef` 列表，字段详情通常继续通过 `item.Path` 做 `blenderApi.Rna.GetAsync<T>` / `blenderApi.Rna.SetAsync<T>`。

## Operator 调用

- RNA 对象方法用 `blenderApi.Rna.CallAsync<T>`
- Blender operator 用 `blenderApi.Ops.PollAsync` 和 `blenderApi.Ops.CallAsync`

operator 调用使用 tuple kwargs 和强类型 `contextOverride`，不再依赖匿名对象：

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

## Watch 订阅

`blenderApi.Observe.WatchAsync` 采用轻量事件通知加按需读取的模型。Blender 侧会发送 `watch.dirty`，然后由 C# 侧决定是否执行 `blenderApi.Observe.ReadAsync`、`blenderApi.Rna.GetAsync` 或 `blenderApi.Rna.ListAsync`。

```csharp
await using var watch = await blenderApi.Observe.WatchAsync(
    watchId: "materials",
    source: WatchSource.Depsgraph,
    path: "bpy.data.materials",
    onDirty: async _ =>
    {
        var materials = await blenderApi.Rna.ListAsync("bpy.data.materials");
        // 在这里刷新 UI。
    });
```

如果你只是需要重新拉取整个列表，直接再次执行 `blenderApi.Rna.ListAsync(...)` 就够了。

如果你要维护详情页状态，通常是收到 dirty 后按路径 `blenderApi.Rna.GetAsync(...)`。

如果你要维护更细粒度的页面状态，可以结合 `dirtyRefs` 和 `blenderApi.Observe.ReadAsync(...)` 自己决定刷新范围。

## 值模型说明

支持的值模型：

- 标量：`bool`、`int`、`long`、`double`、`string`
- 数组：`bool[]`、`int[]`、`long[]`、`double[]`、`string[]`
- RNA 引用：`RnaItemRef`
- `null`

如果你要在 `GetAsync<T>`、`SetAsync<T>` 或 `CallAsync<T>` 中使用自定义 DTO，需要在启动时注册自己的 source-generated resolver。

如果类型信息未注册，运行时会抛出 `missing_json_type_info_for_type`。

如果你需要完全自定义的 Blender 侧能力，建议保留默认 endpoint 处理标准 RNA / operator 流量，只把应用私有命令放进额外的自定义 business endpoint。
