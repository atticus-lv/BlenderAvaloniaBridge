# BlenderApi

`BlenderApi` is the root C# entry point for business calls from Avalonia to Blender.

It groups bridge capabilities into explicit domains:

- `blenderApi.Rna` for RNA path listing, reads, writes, descriptions, and RNA method calls
- `blenderApi.Ops` for Blender operator polling and execution
- `blenderApi.Observe` for watch subscriptions and snapshot reads

This split keeps resource access, imperative commands, and observation flows separate while preserving the existing wire protocol names:

- `rna.*`
- `ops.*`
- `watch.*`

`Data` is intentionally reserved for a future resource-oriented API. It is not exposed in the current release.

## Domain Map

```csharp
blenderApi.Rna.ListAsync / GetAsync<T> / ReadArrayAsync / SetAsync<T> / DescribeAsync / CallAsync<T>
blenderApi.Ops.PollAsync / CallAsync
blenderApi.Observe.WatchAsync / ReadAsync
```

## Read By Task

- Need to browse or edit Blender state by path: go to [RNA](./rna.md)
- Need to call Blender operators: go to [Ops](./ops.md)
- Need change notifications or watch snapshots: go to [Observe](./observe.md)
- Need shared request and value types: go to [Types](./types.md)

## Usage Pattern

The usual application flow is:

1. Use `blenderApi.Rna` to list or read the objects your UI needs.
2. Use `blenderApi.Ops` when the action maps to a Blender operator.
3. Use `blenderApi.Observe` to subscribe to changes and refresh affected paths.

If you have not connected the Avalonia app and Blender addon yet, start with the [Integration Guide](../integration/index.md).
