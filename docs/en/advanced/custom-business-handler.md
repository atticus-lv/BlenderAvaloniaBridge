# Custom Business Endpoint

By default, the Blender side uses `DefaultBusinessEndpoint` to process `business_request` and return `business_response`.

## What the default endpoint supports

The built-in endpoint currently handles these business names:

- `scene.objects.list`
- `object.property.get`
- `object.property.set`
- `operator.call`

## Interface contract

Implement `BusinessEndpoint` with one entry point:

```python
class BusinessEndpoint:
    def invoke(self, request):
        raise NotImplementedError
```

`request` is a `BusinessRequest`, and the return value is a `BusinessResponse`.

## Minimal custom example

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

## When to keep the default implementation

Keep `DefaultBusinessEndpoint` if you still want the built-in support for:

- listing scene objects
- reading or writing object properties
