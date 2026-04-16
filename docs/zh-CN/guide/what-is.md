# Blender Avalonia Bridge 是什么

Blender Avalonia Bridge 是一个把 Avalonia UI 接入 Blender 的桥接工具套件。

Bridge 把 Avalonia 带进 Blender，在保留 Avalonia 几乎完整框架能力的同时，提供 Blender 原生的绘制与交互体验。

<div class="doc-image-row">
  <figure class="doc-image-card">
    <img src="/statics/images/headlessmode.png" alt="Blender Avalonia Bridge 总览">
    <figcaption>Headless 模式中的 Blender 内嵌 UI</figcaption>
  </figure>
  <figure class="doc-image-card">
    <img src="/statics/images/windowmode.png" alt="Blender Avalonia Bridge 运行模式">
    <figcaption>Desktop 模式中的独立 Avalonia 窗口</figcaption>
  </figure>
</div>

- Avalonia 侧负责真正的 UI、状态和业务逻辑
- Blender 侧负责宿主与桥接

这样你不需要在 Blender 里自己维护一整套 GPU 绘制 UI 框架，也不需要把所有业务都写成 Python 面板逻辑。



## 它不是什么

它不是一个“只在 Blender Python 里堆面板”的方案。

如果你只是要做一个很简单的 Blender 面板或小工具，直接用传统 addon 开发更轻。

这个 bridge 更适合“Blender 负责宿主与桥接，Avalonia 程序负责 UI 和业务”的分工模式。

### 优势

#### 1. 适合复杂 UI

你可以直接用 Avalonia 构建桌面级 UI，而不需要在 Blender 里维护一套自定义 GPU UI 层。

#### 2. 适合复用 .NET / Avalonia 技术栈

如果你熟悉 Avalonia、C# 和 .NET，就可以继续使用现有框架、库和工程组织方式。

#### 3. 适合把复杂业务放在独立进程

把业务逻辑放在 .NET 侧，更适合处理大量数据、复杂计算或已有后端式模块。

#### 4. 适合减少 Python 业务源码分发

如果你不希望把核心业务代码直接以 Python 源码形式分发，可以使用 .NET AOT 把 Avalonia 项目编译为原生程序。



### Headless 模式下的已知限制

- 当前只支持 Windows 和 macOS 平台
- 部分 UI 场景可能出现画面卡顿或缺帧问题
- 不支持外部拖拽，因为 Blender 会先捕获 drop 事件



## 下一步

- 想先跑通一套最小示例，请看[快速开始](./quick-start.md)
- 想接入你自己的 Blender 扩展和 Avalonia 程序，请看[集成指南](../integration/index.md)
