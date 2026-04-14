# Architecture

## Overview

```mermaid
flowchart LR
    A["Blender Addon Shell"] --> B["Blender Core"]
    B --> C["Avalonia Bridge"]
    C --> D["Avalonia UI"]

    C -- "frame" --> B
    C -- "business request" --> B
```

- `Blender Addon Shell`: Blender panels, settings, and runtime entry.
- `Blender Core`: process launch, control connection, frame ingestion, input forwarding, and business handling.
- `Avalonia Bridge`: protocol handling, input application, and frame output.
- `Avalonia UI`: interface and state.

## Runtime Flow

```mermaid
sequenceDiagram
    participant Blender as Blender Addon Shell
    participant Core as Blender Core
    participant Bridge as Avalonia Bridge
    participant UI as Avalonia UI

    Blender->>Core: start()
    Core->>Bridge: Launch Avalonia process
    Bridge->>Core: Establish control connection + init

    UI-->>Bridge: Render UI
    Bridge-->>Core: frame / frame_ready
    Core-->>Blender: Draw overlay

    Blender->>Core: Mouse / keyboard events
    Core->>Bridge: pointer / wheel / key
    Bridge->>UI: Dispatch input

    UI->>Bridge: business request
    Bridge->>Core: business_request
    Core->>Core: Business Endpoint
    Core-->>Bridge: business_response
    Bridge-->>UI: Update state
```

## Protocol Summary

- Control channel: localhost TCP
- Packet format: length-prefixed + JSON header
- Frame transport: shared memory on Windows, with TCP payload fallback when needed
