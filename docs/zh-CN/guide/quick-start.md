# 快速开始

## 1. 发布 Avalonia Sample 的 AOT 版本

先确认机器上安装了 `.NET 10 SDK`，然后在仓库根目录运行：

```powershell
dotnet publish .\src\BlenderAvaloniaBridge.Sample\BlenderAvaloniaBridge.Sample.csproj -c Release -r win-x64 -p:PublishAot=true -o .\artifacts\publish\aot-net10 --configfile .\NuGet.Config
```

生成的可执行文件默认位于：

```text
artifacts\publish\aot-net10\BlenderAvaloniaBridge.Sample.exe
```

## 2. 在 Blender 中添加本地扩展仓库

打开 Blender 的扩展设置，使用 `Add Local Repository` 指向下面这个目录：

```text
src\blender_extension
```

然后启用仓库里的 `avalonia_bridge` 扩展。

## 3. 在面板中指定 AOT exe 并启动

启用扩展后：

1. 打开 `View3D > Sidebar > AvaloniaBridgeDemo`
2. 在 `Avalonia Executable` 中选择刚刚发布出来的 AOT exe
3. 按需调整 `Display Size` 和 `Render Scaling`
3. 点击 `Start UI Bridge`

如果一切正常，你会看到 sample UI 出现在 Blender overlay 中。

## Display Size 和 Render Scaling 的区别

Blender 面板里现在有两个相关设置：

- `Display Size`：Avalonia 逻辑窗口尺寸，影响布局和输入映射
- `Render Scaling`：headless 截图时的渲染倍率，影响输出帧像素密度

`Render Scaling` 默认值是 `1.25`。

当 overlay 因为 Blender 二次缩放而看起来有些发糊时，优先提高 `Render Scaling`，这样可以在不改变逻辑 UI 尺寸的前提下，让 Blender 拿到更清晰的源图。

一般建议：

- 调整布局大小时改 `Display Size`
- 调整清晰度和 DPI 对齐时改 `Render Scaling`
- 修改任一项后都重新启动 bridge
