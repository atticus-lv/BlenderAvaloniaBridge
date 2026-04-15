# Custom Business Endpoint

By default, the Blender side uses `DefaultBusinessEndpoint` to process `business_request`, emit `business_event`, and return `business_response`.

## What the default endpoint supports

The built-in endpoint is now the generic Blender data bridge. It handles these names:

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

Watch invalidation is delivered as the `business_event` named `watch.dirty`.

Every `business_request`, `business_response`, and `business_event` now carries explicit version fields:

- `protocolVersion`
- `schemaVersion`

Current defaults are `protocolVersion = 1` and `schemaVersion = 1`.

## Interface contract

Implement `BusinessEndpoint` with one entry point:

```python
class BusinessEndpoint:
    def invoke(self, request):
        raise NotImplementedError
```

`request` is a `BusinessRequest`, and the return value is a `BusinessResponse`.

`BusinessRequest.response(...)` fills the current protocol and schema versions automatically, so simple custom handlers do not need to repeat them manually.

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

Keep `DefaultBusinessEndpoint` if you want the built-in RNA, operator, and watch bridge and only need to add a few app-specific commands.

This is usually the best split:

- use `DefaultBusinessEndpoint` for standard Blender API traffic
- add a custom endpoint only for private commands such as `ping`, app actions, or domain-specific workflows

On the C# side, the default endpoint is surfaced through `IBlenderDataApi`, so most applications do not need any Python-side glue code at all.
