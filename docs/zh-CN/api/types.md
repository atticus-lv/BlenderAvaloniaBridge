# Shared Types

这些类型会在 `BlenderApi` 的多个领域中复用。

## RnaItemRef

`RnaItemRef` 是 `blenderApi.Rna.ListAsync` 或 `blenderApi.Rna.GetAsync<RnaItemRef>` 返回的引用句柄。

常见稳定字段包括：

- `Name`
- `RnaType`
- `IdType`
- `SessionUid`
- `Path`
- `Label`
- `Kind`

建议把它理解成可继续寻址的引用，而不是已经展开完成的 DTO。

## BlenderArrayReadResult

`BlenderArrayReadResult` 是 `blenderApi.Rna.ReadArrayAsync` 的返回类型。

它把数组元数据和原始二进制 payload 分开表示：

- `Path`
- `RnaType`
- `ValueType`
- `ElementType`
- `Count`
- `Shape`
- `RawBytes`

`RawBytes` 不是 JSON 数组，而是协议包中的二进制 payload。使用时应结合 `ElementType` 和 `Shape` 进行解释。

## BlenderOperatorCall

`BlenderOperatorCall` 是 `blenderApi.Ops` 使用的结构化 operator 请求模型。

适合在下面这些场景使用：

- 需要命名 kwargs 集合
- 需要 context override
- 需要比 tuple 重载更强的请求形状

## BlenderContextOverride

`BlenderContextOverride` 用路径字符串表达 operator context override，例如：

- `ActiveObject`
- `SelectedObjects`

## 支持的值模型

bridge 当前支持的值模型包括：

- 标量：`bool`、`int`、`long`、`double`、`string`
- 数组：`bool[]`、`int[]`、`long[]`、`double[]`、`string[]`
- RNA 引用：`RnaItemRef`
- `null`

## 自定义 JSON Resolver

如果你要在 `GetAsync<T>`、`SetAsync<T>` 或 `CallAsync<T>` 中使用自定义 DTO，需要在启动阶段注册自己的 source-generated JSON resolver。

如果缺少运行时类型元数据，反序列化会抛出 `missing_json_type_info_for_type`。
