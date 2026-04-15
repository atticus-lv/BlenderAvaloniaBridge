# 自定义 Business Endpoint

默认情况下，Blender 侧使用 `DefaultBusinessEndpoint` 处理 `business_request`、发出 `business_event`，并返回 `business_response`。

## 默认 endpoint 做了什么

现在内置 endpoint 已经是通用 Blender 数据桥接层，默认支持这些业务名：

- `rna.list`
- `rna.get`
- `rna.set`
- `rna.describe`
- `rna.call`
- `ops.poll`
- `ops.call`
- `watch.subscribe`
- `watch.unsubscribe`
- `watch.read`

watch 失效通知会通过名为 `watch.dirty` 的 `business_event` 发出。

所有 `business_request`、`business_response`、`business_event` 现在都显式带版本字段：

- `protocolVersion`
- `schemaVersion`

当前默认值固定为 `protocolVersion = 1`、`schemaVersion = 1`。

## 接口约定

你可以实现 `BusinessEndpoint`，提供统一入口：

```python
class BusinessEndpoint:
    def invoke(self, request):
        raise NotImplementedError
```

其中 `request` 是 `BusinessRequest`，返回值是 `BusinessResponse`。

`BusinessRequest.response(...)` 会自动填入当前协议版本和 schema 版本，所以简单自定义 handler 不需要重复写这些字段。

## 最小自定义示例

```python
from .core import (
    BusinessEndpoint,
    BusinessRequest,
    BusinessError,
    BridgeConfig,
    BridgeController,
)


class MyBusinessEndpoint(BusinessEndpoint):
    def invoke(self, request):
        if request.name == "ping":
            return BusinessRequest.response(
                reply_to=request.message_id,
                payload={"pong": True},
            )

        return BusinessRequest.response(
            reply_to=request.message_id,
            ok=False,
            error=BusinessError(
                code="unsupported_business_request",
                message=f"Unsupported business request: {request.name}",
            ),
        )


controller = BridgeController(
    BridgeConfig(executable_path="C:/path/to/YourAvaloniaApp.exe"),
    business_endpoint=MyBusinessEndpoint(),
)
```

## 什么时候保留默认实现

如果你希望继续使用现成的 RNA、operator、watch 桥接能力，只额外补少量应用私有命令，优先继续用 `DefaultBusinessEndpoint`。

通常推荐这样拆分：

- `DefaultBusinessEndpoint` 继续处理标准 Blender API 流量
- 自定义 endpoint 只负责 `ping`、应用命令或领域专用流程

在 C# 侧，默认 endpoint 会直接映射成带 `Rna`、`Ops`、`Observe` 三个领域入口的 `BlenderApi`，所以大多数场景其实不需要再写 Python glue code。
