# Blender Avalonia Bridge 是什么

Blender Avalonia Bridge 是一个让你能够在 Blender 中无缝使用 Avalonia 框架的组件化桥接方案。

- Avalonia 侧负责真正的 UI、状态和业务逻辑
- Blender 侧负责嵌入显示、输入转发和业务命令桥接

这样你不需要在 Blender 里自己维护一整套 GPU 绘制 UI 框架，也不需要把所有业务都写成 Python 面板逻辑。

## 适合谁

- 想给 Blender 工具做更强 UI 表现的开发者
- 想重用 .NET 生态和 Avalonia 能力的团队
- 需要把业务逻辑更多放在独立可执行程序中的项目

## 不适合谁

- 只想写一个非常简单的 Blender 面板
- 不希望维护 Python 和 C# 两套代码
- 明确要求所有逻辑都运行在 Blender Python 进程内


## 优势

### 1. 无需构建自己的blender gpu ui框架

这个桥接方案让你可以在 Avalonia 里构建 UI，而不需要在 Blender 里维护一个自定义的 GPU 驱动 UI 层。

### 2. 使用.NET现有生态

如果你熟悉 Avalonia、C# 和 .NET，就可以继续使用你熟悉的框架、库和工程组织方式

### 3. 性能

使用dotnet完成你的外部业务逻辑，性能会比纯Python更好，尤其是当你需要处理大量数据或者复杂计算的时候。

### 4. aot编译业务代码，避免分发 Python 源码

如果你不希望把核心业务代码直接以 Python 源码形式分发，可以使用 .NET AOT 把 Avalonia 项目编译为原生程序。这种方式和把 Python 编译成 `pyd` / `pyc` 不同，不会带来那类 Blender GPL 代码分发风险顾虑。

## 已知限制

- 目前只支持 Windows 平台
- 不支持avalonia的layout动画：如splitview的pane动画效果会被，改为直接切换状态
- 不支持外部拖拽：blender会捕捉drop事件，导致无法把数据传输到avalonia