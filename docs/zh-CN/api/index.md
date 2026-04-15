# BlenderApi

`BlenderApi` 是 Avalonia 到 Blender 的 C# 业务调用根入口。

它把 bridge 能力明确拆成三个领域：

- `blenderApi.Rna`：RNA 路径列举、读写、描述与 RNA 方法调用
- `blenderApi.Ops`：Blender operator 的 poll 与执行
- `blenderApi.Observe`：watch 订阅与快照读取

这样可以把资源访问、命令执行和观察流拆开，同时保持底层协议名不变：

- `rna.*`
- `ops.*`
- `watch.*`

`Data` 会为未来偏资源型的 API 预留，但当前版本不会先暴露空入口。

## 领域总览

```csharp
blenderApi.Rna.ListAsync / GetAsync<T> / ReadArrayAsync / SetAsync<T> / DescribeAsync / CallAsync<T>
blenderApi.Ops.PollAsync / CallAsync
blenderApi.Observe.WatchAsync / ReadAsync
```

## 按任务阅读

- 需要按路径浏览或修改 Blender 状态：看 [RNA](./rna.md)
- 需要调用 Blender operator：看 [Ops](./ops.md)
- 需要变更通知或 watch 快照：看 [Observe](./observe.md)
- 需要了解共享请求类型和值模型：看 [Types](./types.md)

## 常见使用方式

典型流程通常是：

1. 先用 `blenderApi.Rna` 列表或读取 UI 需要的数据。
2. 遇到动作型能力时，用 `blenderApi.Ops` 调 Blender operator。
3. 需要响应 Blender 侧变化时，用 `blenderApi.Observe` 建 watch 并刷新相关路径。

如果你还没有接好 Avalonia 程序与 Blender 扩展，请先看[集成指南](../integration/index.md)。
