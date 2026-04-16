# Custom Business Endpoint

By default, the Blender side uses `DefaultBusinessEndpoint` to process `business_request`, emit `business_event`, and return `business_response`.

## What the default endpoint supports

`DefaultBusinessEndpoint` carries the built-in Blender data bridge and handles these names:

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

On the C# side, the current `BlenderApi` surface for `Rna`, `Ops`, and `Observe` depends on these default business names.

If the Blender side replaces the endpoint with a custom one and does not keep these names compatible, the built-in `BlenderApi` can no longer be used directly.

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

## Current limitation

- With `DefaultBusinessEndpoint`, the built-in `BlenderApi` works directly
- If you replace it with a custom endpoint, the built-in `BlenderApi` stops working unless the custom endpoint keeps `rna.*`, `ops.*`, and `watch.*` compatible
- When only adding a few app-specific commands, keep `DefaultBusinessEndpoint` and extend on top of it
