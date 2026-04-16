# 自定义 Business Endpoint

默认情况下，Blender 侧使用 `DefaultBusinessEndpoint` 处理 `business_request`、发出 `business_event`，并返回 `business_response`。

## 默认 endpoint 做了什么

`DefaultBusinessEndpoint` 承载当前内置的 Blender 数据桥接能力，默认支持这些业务名：

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

当前 C# 侧 `BlenderApi` 的 `Rna`、`Ops`、`Observe` 也建立在这组默认业务名之上。

如果 Blender 侧直接替换成自定义 endpoint，而不继续兼容这些业务名，当前内置 `BlenderApi` 不能直接使用。

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

## 当前限制

- 使用 `DefaultBusinessEndpoint` 时，可以直接使用当前内置 `BlenderApi`
- 直接替换为自定义 endpoint 后，当前内置 `BlenderApi` 不再可用，除非自定义 endpoint 继续兼容 `rna.*`、`ops.*`、`watch.*`
- 只增加少量应用私有命令时，应继续保留 `DefaultBusinessEndpoint`，并在其上扩展
