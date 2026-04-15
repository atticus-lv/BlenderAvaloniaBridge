# Observe

`blenderApi.Observe` 负责 watch 订阅和显式快照读取。

它采用轻量通知模型：

1. Blender 发送 `watch.dirty`。
2. Avalonia 程序自己决定刷新什么。
3. 程序再通过 `blenderApi.Observe.ReadAsync`、`blenderApi.Rna.GetAsync` 或 `blenderApi.Rna.ListAsync` 拉取新状态。

## 方法

- `WatchAsync(watchId, source, path, onDirty, cancellationToken)`
- `ReadAsync(watchId, cancellationToken)`

## 建立 Watch

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

## 选择刷新策略

- 如果整个列表重新拉取成本不高，直接再次调用 `blenderApi.Rna.ListAsync(...)`
- 如果详情面板绑定在明确路径上，调用 `blenderApi.Rna.GetAsync(...)`
- 如果你需要 watch 快照和 dirty 引用一起参与判断，使用 `blenderApi.Observe.ReadAsync(...)`

## 分发语义

`watch.dirty` 是失效通知，不是完整数据流。

- 同一个 `watchId` 的 `onDirty` 回调不会并发执行
- 同一个 `watchId` 的 dirty 事件会按顺序串行分发
- 如果前一个回调尚未完成，同一个 `watchId` 上后续积压的 dirty 会采用 latest-wins，只保留最新 revision 继续投递
- 不同 `watchId` 之间仍然可以并行处理

这意味着 `onDirty` 更适合做“标记需要刷新”或“读取最新快照”，而不是依赖每一个中间 revision 都被逐条处理。

## WatchSource

watch API 统一归在 `Observe` 下，具体来源仍通过 `WatchSource` 选择：

- `WatchSource.Depsgraph`
- `WatchSource.Frame`
- `WatchSource.Lifecycle`
