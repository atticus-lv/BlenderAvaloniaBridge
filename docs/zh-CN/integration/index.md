# 集成概览

如果你只是想先运行仓库自带示例，请看[快速开始](../guide/quick-start.md)。

## 接入范围

一次完整接入包含两部分：

- Avalonia 程序负责 UI、状态和业务逻辑，并作为 bridge 进程启动
- Blender 扩展负责启动 bridge、绘制 overlay、转发输入和承载业务通道

常见接入顺序：

1. 配置 Blender 侧 `BridgeConfig`
2. 修改 Avalonia 程序入口，支持 bridge 启动
3. 在 Blender 侧组装 `BridgeController` 与可选的 `View3DOverlayHost`
4. 在 modal operator 中驱动 `tick_once()` 和 `handle_event(context, event)`

## 公共配置

下面这些配置由 Blender 侧 Python runtime / addon 在创建 `BridgeConfig` 时提供。

- `executable_path`：Avalonia 程序可执行文件路径。Blender 侧通过这个路径启动 bridge 进程。
- `window_mode`：运行模式。`headless` 启用 `frames + input + business`，`desktop` 只建立 `business` 连接。
- `width` 和 `height`：Avalonia 逻辑窗口尺寸。
- `render_scaling`：headless 模式下的渲染倍率，用于提高清晰度。

`executable_path` 可以指向开发期的 Debug / Release 可执行文件。发布场景优先使用 AOT 可执行文件。路径应对 Blender 进程可见，并与当前平台一致，例如 Windows 上的 `.exe` 或 macOS 上的应用可执行文件。

其中 `window_mode`、`width`、`height` 和 `render_scaling` 会作为 bridge 启动参数影响 Avalonia 端运行方式。

## Core 与 Sample / Addon 的边界

- `BlenderAvaloniaBridge.Core` 和 Blender 侧 `avalonia_bridge/core` 负责桥接基础设施：进程、传输、frame、input、business
- `BlenderAvaloniaBridge.Sample` 和 Blender addon 壳层负责示例 UI、配置组装与业务代码
- 集成时通常不需要修改两侧 core，主要在 Avalonia app 层和 Blender addon 层接入

## 分侧接入

- Avalonia 侧接入：看 [Avalonia 侧接入](./avalonia.md)
- Blender 侧接入：看 [Blender 侧接入](./blender.md)

## 相关章节

- 共享 session 模型和运行链路，请看[工作原理](../guide/how-it-works.md)
- 想查看 C# 侧调用方式，请看 [API 章节](../api/index.md)
