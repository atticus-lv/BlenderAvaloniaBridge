# Blender Avalonia Bridge 是什么

Blender Avalonia Bridge 是一个把 Avalonia UI 接入 Blender 的桥接工具套件。

- Avalonia 侧负责真正的 UI、状态和业务逻辑
- Blender 侧负责嵌入显示、输入转发和业务命令桥接

这样你不需要在 Blender 里自己维护一整套 GPU 绘制 UI 框架，也不需要把所有业务都写成 Python 面板逻辑。

## 模式

当前支持两种 `window_mode`：

- `headless`：默认模式，将 Avalonia 画面绘制到 Blender 中，在显示区域内捕捉鼠标和键盘事件
- `desktop`：经典桌面窗口模式，只建立 business 连接

## 适合谁

- 想给 Blender 工具做更强 UI 表现的开发者
- 想重用 .NET 生态和 Avalonia 能力的团队
- 需要把业务逻辑更多放在独立可执行程序中的项目



## 不适合谁

- 只想写一个非常简单的 Blender 面板
- 不希望维护 Python 和 C# 两套代码
- 明确要求所有逻辑都运行在 Blender Python 进程内

## 优势

### 1. 无需自建 Blender GPU UI 框架

这个桥接方案让你可以在 Avalonia 里构建 UI，而不需要在 Blender 里维护一个自定义的 GPU 驱动 UI 层。

### 2. 复用 .NET 生态

如果你熟悉 Avalonia、C# 和 .NET，就可以继续使用现有框架、库和工程组织方式。

### 3. 性能

把外部业务逻辑放在 .NET 侧，通常会比纯 Python 更适合处理大量数据或复杂计算。

### 4. 使用 AOT 编译业务代码，减少 Python 源码分发

如果你不希望把核心业务代码直接以 Python 源码形式分发，可以使用 .NET AOT 把 Avalonia 项目编译为原生程序。

## 已知限制

- 目前 headless 共享内存桥接支持 Windows 和 macOS 平台
- headless 模式下存在以下限制：
- 不支持布局动画，例如 `SplitView` 的 pane 动画会直接切换状态
- 不支持 transitions 动画，例如位置位移动画可能会卡顿并直接跳到最终状态
- 不支持外部拖拽，因为 Blender 会先捕获 drop 事件
