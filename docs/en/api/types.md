# Shared Types

These types are commonly used across `BlenderApi` domains.

## RnaItemRef

`RnaItemRef` is a reference handle returned by `blenderApi.Rna.ListAsync` or `blenderApi.Rna.GetAsync<RnaItemRef>`.

Common stable fields include:

- `Name`
- `RnaType`
- `IdType`
- `SessionUid`
- `Path`
- `Label`
- `Kind`

It is an addressable reference, not a fully expanded DTO.

## BlenderArrayReadResult

`BlenderArrayReadResult` is returned by `blenderApi.Rna.ReadArrayAsync`.

It separates array metadata from the raw binary payload:

- `Path`
- `RnaType`
- `ValueType`
- `ElementType`
- `Count`
- `Shape`
- `RawBytes`

`RawBytes` is not JSON data. It comes from the protocol packet payload and should be decoded using `ElementType` and `Shape`.

## BlenderOperatorCall

`BlenderOperatorCall` is the structured operator request model used by `blenderApi.Ops`.

Use cases:

- keyword arguments as a named collection
- context override data
- a stronger request shape than tuple-only overloads

## BlenderContextOverride

`BlenderContextOverride` models operator context overrides with path-based object references such as:

- `ActiveObject`
- `SelectedObjects`

## Supported Value Models

Supported bridge value models include:

- scalars: `bool`, `int`, `long`, `double`, `string`
- arrays: `bool[]`, `int[]`, `long[]`, `double[]`, `string[]`
- RNA references: `RnaItemRef`
- `null`

## Custom JSON Resolvers

When using custom DTOs with `GetAsync<T>`, `SetAsync<T>`, or `CallAsync<T>`, register your own source-generated JSON resolver during startup.

If runtime type metadata is missing, deserialization fails with `missing_json_type_info_for_type`.
