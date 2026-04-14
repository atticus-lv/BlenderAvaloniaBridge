# 项目架构

## 整体架构

```mermaid
flowchart LR
    A["Blender Addon Shell"] --> B["Blender Core"]
    B --> C["Avalonia Bridge"]
    C --> D["Avalonia UI"]

    C -- "frame" --> B
    C -- "business request" --> B
```

- `Blender Addon Shell`：Blender 面板、配置和运行入口。
- `Blender Core`：负责进程启动、控制连接、帧接收、输入转发和业务处理。
- `Avalonia Bridge`：负责协议处理、输入应用和帧输出。
- `Avalonia UI`：负责界面和状态。

## 运行时数据流

```mermaid
sequenceDiagram
    participant Blender as Blender Addon Shell
    participant Core as Blender Core
    participant Bridge as Avalonia Bridge
    participant UI as Avalonia UI

    Blender->>Core: start()
    Core->>Bridge: 启动 Avalonia 进程
    Bridge->>Core: 建立控制连接 + init

    UI-->>Bridge: 渲染 UI
    Bridge-->>Core: frame / frame_ready
    Core-->>Blender: 绘制 overlay

    Blender->>Core: 鼠标 / 键盘事件
    Core->>Bridge: pointer / wheel / key
    Bridge->>UI: 分发输入

    UI->>Bridge: business request
    Bridge->>Core: business_request
    Core->>Core: Business Endpoint
    Core-->>Bridge: business_response
    Bridge-->>UI: 更新状态
```

## 协议摘要

- 控制通道：localhost TCP
- 包格式：长度前缀 + JSON header
- 帧传输：Windows 默认共享内存，必要时回退到 TCP payload
