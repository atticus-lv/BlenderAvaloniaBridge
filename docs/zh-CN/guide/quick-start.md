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
