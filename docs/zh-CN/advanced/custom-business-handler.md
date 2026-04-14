# 自定义 Business Endpoint

默认情况下，Blender 侧使用 `DefaultBusinessEndpoint` 处理 `business_request` 并返回 `business_response`。

## 默认 endpoint 做了什么

当前内置了这几类默认业务名：

- `scene.objects.list`
- `object.property.get`
- `object.property.set`
- `operator.call`

## 接口约定

你可以实现 `BusinessEndpoint`，提供统一入口：

```python
class BusinessEndpoint:
    def invoke(self, request):
        raise NotImplementedError
```

其中 `request` 是 `BusinessRequest`，返回值是 `BusinessResponse`。

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

如果你还需要这些现成能力，优先继续用 `DefaultBusinessEndpoint`：

- 列出场景对象
- 获取或设置对象属性
- 调用 allowlist 内的 Blender operator
