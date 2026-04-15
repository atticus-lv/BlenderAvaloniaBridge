using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge;

[JsonConverter(typeof(WatchSourceJsonConverter))]
public enum WatchSource
{
    Depsgraph,
    Frame,
    Lifecycle,
}

internal sealed class WatchSourceJsonConverter : JsonConverter<WatchSource>
{
    public override WatchSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "depsgraph" => WatchSource.Depsgraph,
            "frame" => WatchSource.Frame,
            "lifecycle" => WatchSource.Lifecycle,
            _ => throw new JsonException($"Unsupported watch source '{value}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, WatchSource value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            WatchSource.Depsgraph => "depsgraph",
            WatchSource.Frame => "frame",
            WatchSource.Lifecycle => "lifecycle",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        });
    }
}

public sealed class RnaItemRef
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "rna";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("rnaType")]
    public string? RnaType { get; set; }

    [JsonPropertyName("idType")]
    public string? IdType { get; set; }

    [JsonPropertyName("sessionUid")]
    public long? SessionUid { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }
}

public sealed class RnaPropertyDescriptor
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("valueType")]
    public string? ValueType { get; set; }

    [JsonPropertyName("readonly")]
    public bool Readonly { get; set; }

    [JsonPropertyName("animatable")]
    public bool Animatable { get; set; }

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("arrayLength")]
    public int? ArrayLength { get; set; }

    [JsonPropertyName("enumItems")]
    public List<string>? EnumItems { get; set; }

    [JsonPropertyName("isEnumFlag")]
    public bool IsEnumFlag { get; set; }

    [JsonPropertyName("fixedType")]
    public string? FixedType { get; set; }
}

public sealed class RnaDescribeResult
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("rnaType")]
    public string? RnaType { get; set; }

    [JsonPropertyName("valueType")]
    public string? ValueType { get; set; }

    [JsonPropertyName("readonly")]
    public bool Readonly { get; set; }

    [JsonPropertyName("animatable")]
    public bool Animatable { get; set; }

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("arrayLength")]
    public int? ArrayLength { get; set; }

    [JsonPropertyName("enumItems")]
    public List<string>? EnumItems { get; set; }

    [JsonPropertyName("isEnumFlag")]
    public bool IsEnumFlag { get; set; }

    [JsonPropertyName("fixedType")]
    public string? FixedType { get; set; }

    [JsonPropertyName("properties")]
    public List<RnaPropertyDescriptor>? Properties { get; set; }
}

public sealed class BlenderArrayReadResult
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("rnaType")]
    public string? RnaType { get; set; }

    [JsonPropertyName("valueType")]
    public string ValueType { get; set; } = "array_buffer";

    [JsonPropertyName("elementType")]
    public string ElementType { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("shape")]
    public int[] Shape { get; set; } = Array.Empty<int>();

    [JsonIgnore]
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();
}

public sealed class OperatorPollResult
{
    [JsonPropertyName("operator")]
    public string OperatorName { get; set; } = string.Empty;

    [JsonPropertyName("canExecute")]
    public bool CanExecute { get; set; }

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }
}

public sealed class OperatorCallResult
{
    [JsonPropertyName("operator")]
    public string OperatorName { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public List<string> Result { get; set; } = new();
}

public sealed class WatchDirtyEvent
{
    [JsonPropertyName("watchId")]
    public string WatchId { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public long Revision { get; set; }

    [JsonPropertyName("source")]
    public WatchSource Source { get; set; }

    [JsonPropertyName("dirtyRefs")]
    public List<RnaItemRef>? DirtyRefs { get; set; }
}

public sealed class WatchSnapshot
{
    [JsonPropertyName("watchId")]
    public string WatchId { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public long Revision { get; set; }

    [JsonPropertyName("source")]
    public WatchSource Source { get; set; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }
}

public sealed class BlenderApiOptions
{
    public IList<IJsonTypeInfoResolver> TypeInfoResolvers { get; } = new List<IJsonTypeInfoResolver>();

    internal BlenderApiOptions Clone()
    {
        var clone = new BlenderApiOptions();
        foreach (var resolver in TypeInfoResolvers)
        {
            clone.TypeInfoResolvers.Add(resolver);
        }

        return clone;
    }
}

public sealed class BlenderContextOverride
{
    public string? ActiveObject { get; init; }

    public IReadOnlyList<string>? SelectedObjects { get; init; }

    public string? Object { get; init; }

    public string? Scene { get; init; }

    public string? ViewLayer { get; init; }

    public string? Collection { get; init; }

    public string? Window { get; init; }

    public string? Screen { get; init; }

    public string? Area { get; init; }

    public string? Region { get; init; }
}

public sealed class BlenderMethodCall
{
    public IReadOnlyList<BlenderValue>? Args { get; init; }

    public IReadOnlyList<BlenderNamedArg>? Kwargs { get; init; }
}

public sealed class BlenderOperatorCall
{
    public IReadOnlyList<BlenderNamedArg>? Properties { get; init; }

    public string OperatorContext { get; init; } = "EXEC_DEFAULT";

    public BlenderContextOverride? ContextOverride { get; init; }
}

public readonly struct BlenderNamedArg
{
    public BlenderNamedArg(string name, BlenderValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public BlenderValue Value { get; }

    public static implicit operator BlenderNamedArg((string Name, BlenderValue Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, bool Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, int Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, long Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, double Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, string? Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, bool[]? Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, int[]? Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, long[]? Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, double[]? Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, string[]? Value) arg) => new(arg.Name, arg.Value);
    public static implicit operator BlenderNamedArg((string Name, RnaItemRef? Value) arg) => new(arg.Name, arg.Value);
}

[JsonConverter(typeof(BlenderValueJsonConverter))]
public readonly struct BlenderValue
{
    private readonly BlenderValueKind _kind;
    private readonly object? _value;

    private BlenderValue(BlenderValueKind kind, object? value)
    {
        _kind = kind;
        _value = value;
    }

    public static BlenderValue Null => new(BlenderValueKind.Null, null);

    public static implicit operator BlenderValue(bool value) => new(BlenderValueKind.Bool, value);
    public static implicit operator BlenderValue(int value) => new(BlenderValueKind.Int, value);
    public static implicit operator BlenderValue(long value) => new(BlenderValueKind.Long, value);
    public static implicit operator BlenderValue(double value) => new(BlenderValueKind.Double, value);
    public static implicit operator BlenderValue(string? value) => value is null ? Null : new(BlenderValueKind.String, value);
    public static implicit operator BlenderValue(bool[]? value) => value is null ? Null : new(BlenderValueKind.BoolArray, value);
    public static implicit operator BlenderValue(int[]? value) => value is null ? Null : new(BlenderValueKind.IntArray, value);
    public static implicit operator BlenderValue(long[]? value) => value is null ? Null : new(BlenderValueKind.LongArray, value);
    public static implicit operator BlenderValue(double[]? value) => value is null ? Null : new(BlenderValueKind.DoubleArray, value);
    public static implicit operator BlenderValue(string[]? value) => value is null ? Null : new(BlenderValueKind.StringArray, value);
    public static implicit operator BlenderValue(RnaItemRef? value) => value is null ? Null : new(BlenderValueKind.RnaItemRef, value);

    internal JsonElement ToJsonElement(BlenderJsonTypeResolver resolver)
    {
        return _kind switch
        {
            BlenderValueKind.Null => default,
            BlenderValueKind.Bool => resolver.SerializeKnown((bool)_value!),
            BlenderValueKind.Int => resolver.SerializeKnown((int)_value!),
            BlenderValueKind.Long => resolver.SerializeKnown((long)_value!),
            BlenderValueKind.Double => resolver.SerializeKnown((double)_value!),
            BlenderValueKind.String => resolver.SerializeKnown((string)_value!),
            BlenderValueKind.BoolArray => resolver.SerializeKnown((bool[])_value!),
            BlenderValueKind.IntArray => resolver.SerializeKnown((int[])_value!),
            BlenderValueKind.LongArray => resolver.SerializeKnown((long[])_value!),
            BlenderValueKind.DoubleArray => resolver.SerializeKnown((double[])_value!),
            BlenderValueKind.StringArray => resolver.SerializeKnown((string[])_value!),
            BlenderValueKind.RnaItemRef => resolver.SerializeKnown((RnaItemRef)_value!),
            _ => throw new InvalidOperationException($"Unsupported BlenderValue kind '{_kind}'.")
        };
    }

    internal static BlenderValue FromJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => Null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Array => new BlenderValue(BlenderValueKind.JsonArray, element.Clone()),
            JsonValueKind.Object => new BlenderValue(BlenderValueKind.JsonObject, element.Clone()),
            _ => throw new JsonException($"Unsupported JsonValueKind '{element.ValueKind}' for BlenderValue.")
        };
    }

    internal JsonElement RawJsonOrSerialized(BlenderJsonTypeResolver resolver)
    {
        return _kind switch
        {
            BlenderValueKind.JsonArray or BlenderValueKind.JsonObject => ((JsonElement)_value!).Clone(),
            _ => ToJsonElement(resolver)
        };
    }
}

internal enum BlenderValueKind
{
    Null,
    Bool,
    Int,
    Long,
    Double,
    String,
    BoolArray,
    IntArray,
    LongArray,
    DoubleArray,
    StringArray,
    RnaItemRef,
    JsonArray,
    JsonObject,
}

internal sealed class BlenderValueJsonConverter : JsonConverter<BlenderValue>
{
    public override BlenderValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BlenderValue.FromJsonElement(document.RootElement.Clone());
    }

    public override void Write(Utf8JsonWriter writer, BlenderValue value, JsonSerializerOptions options)
    {
        value.RawJsonOrSerialized(BlenderJsonTypeResolver.Default).WriteTo(writer);
    }
}

public interface IBlenderRnaApi
{
    Task<IReadOnlyList<RnaItemRef>> ListAsync(string path, CancellationToken cancellationToken = default);

    Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default);

    Task<BlenderArrayReadResult> ReadArrayAsync(string path, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string path, T value, CancellationToken cancellationToken = default);

    Task<RnaDescribeResult> DescribeAsync(string path, CancellationToken cancellationToken = default);

    Task<T> CallAsync<T>(string path, string method, params BlenderNamedArg[] kwargs);

    Task<T> CallAsync<T>(
        string path,
        string method,
        BlenderMethodCall call,
        CancellationToken cancellationToken = default);
}

public interface IBlenderOpsApi
{
    Task<OperatorPollResult> PollAsync(
        string operatorName,
        string operatorContext = "EXEC_DEFAULT",
        BlenderContextOverride? contextOverride = null,
        CancellationToken cancellationToken = default);

    Task<OperatorCallResult> CallAsync(string operatorName, params BlenderNamedArg[] properties);

    Task<OperatorCallResult> CallAsync(
        string operatorName,
        BlenderOperatorCall call,
        CancellationToken cancellationToken = default);
}

public interface IBlenderObserveApi
{
    Task<IAsyncDisposable> WatchAsync(
        string watchId,
        WatchSource source,
        string path,
        Func<WatchDirtyEvent, Task> onDirty,
        CancellationToken cancellationToken = default);

    Task<WatchSnapshot> ReadAsync(string watchId, CancellationToken cancellationToken = default);
}

internal interface IBusinessEventSink
{
    Task HandleEventAsync(BusinessEvent businessEvent);
}

internal interface IWatchActivitySource
{
    event Action<bool>? ActiveWatchStateChanged;

    bool HasActiveWatches { get; }
}

public class BlenderApi : IBusinessEventSink, IWatchActivitySource
{
    private static readonly JsonElement EmptyPayload = JsonDocument.Parse("{}").RootElement.Clone();
    private readonly IBusinessEndpoint _businessEndpoint;
    private readonly BlenderJsonTypeResolver _typeResolver;
    private readonly ConcurrentDictionary<string, WatchRegistration> _registrations = new(StringComparer.Ordinal);

    event Action<bool>? IWatchActivitySource.ActiveWatchStateChanged
    {
        add => _activeWatchStateChanged += value;
        remove => _activeWatchStateChanged -= value;
    }

    bool IWatchActivitySource.HasActiveWatches => !_registrations.IsEmpty;

    private event Action<bool>? _activeWatchStateChanged;

    public BlenderApi(IBusinessEndpoint businessEndpoint, BlenderApiOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(businessEndpoint);
        _businessEndpoint = businessEndpoint;
        _typeResolver = new BlenderJsonTypeResolver(options);
        Rna = new BlenderRnaApi(this);
        Ops = new BlenderOpsApi(this);
        Observe = new BlenderObserveApi(this);
    }

    protected BlenderApi()
    {
        _businessEndpoint = new UnsupportedBusinessEndpoint();
        _typeResolver = BlenderJsonTypeResolver.Default;
        Rna = new UnsupportedBlenderRnaApi();
        Ops = new UnsupportedBlenderOpsApi();
        Observe = new UnsupportedBlenderObserveApi();
    }

    public IBlenderRnaApi Rna { get; protected set; }

    public IBlenderOpsApi Ops { get; protected set; }

    public IBlenderObserveApi Observe { get; protected set; }

    internal async Task<BusinessResponse> InvokeResponseAsync<TPayload>(
        string name,
        TPayload payload,
        JsonTypeInfo<TPayload> payloadTypeInfo,
        CancellationToken cancellationToken)
    {
        var response = await _businessEndpoint.InvokeAsync(
            new BusinessRequest(
                name,
                JsonSerializer.SerializeToElement(payload, payloadTypeInfo)),
            cancellationToken);

        if (!response.Ok)
        {
            throw new BlenderBusinessException(name, response);
        }

        return response;
    }

    internal async Task<JsonElement> InvokePayloadAsync<TPayload>(
        string name,
        TPayload payload,
        JsonTypeInfo<TPayload> payloadTypeInfo,
        CancellationToken cancellationToken)
    {
        var response = await InvokeResponseAsync(name, payload, payloadTypeInfo, cancellationToken);
        return response.Payload ?? EmptyPayload;
    }

    internal BlenderJsonTypeResolver TypeResolver => _typeResolver;

    internal async Task<IAsyncDisposable> WatchAsync(
        string watchId,
        WatchSource source,
        string path,
        Func<WatchDirtyEvent, Task> onDirty,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(onDirty);

        var registration = new WatchRegistration(this, watchId, onDirty);
        if (!_registrations.TryAdd(watchId, registration))
        {
            throw new InvalidOperationException($"A watch with id '{watchId}' is already registered.");
        }

        try
        {
            _ = await InvokePayloadAsync(
                "watch.subscribe",
                new WatchSubscribeRequest
                {
                    WatchId = watchId,
                    Source = ToWireValue(source),
                    Path = path,
                },
                ProtocolJsonContext.Default.WatchSubscribeRequest,
                cancellationToken);
        }
        catch
        {
            _registrations.TryRemove(watchId, out _);
            throw;
        }

        NotifyActiveWatchStateIfNeeded();
        return registration;
    }

    internal async Task<WatchSnapshot> ReadWatchAsync(string watchId, CancellationToken cancellationToken = default)
    {
        var payload = await InvokePayloadAsync(
            "watch.read",
            new WatchIdRequest { WatchId = watchId },
            ProtocolJsonContext.Default.WatchIdRequest,
            cancellationToken);

        return _typeResolver.DeserializeRequired(payload, ProtocolJsonContext.Default.WatchSnapshot);
    }

    async Task IBusinessEventSink.HandleEventAsync(BusinessEvent businessEvent)
    {
        if (!string.Equals(businessEvent.Name, "watch.dirty", StringComparison.Ordinal))
        {
            return;
        }

        if (businessEvent.Payload is not JsonElement payload)
        {
            return;
        }

        var dirtyEvent = _typeResolver.DeserializeRequired(payload, ProtocolJsonContext.Default.WatchDirtyEvent);
        if (!_registrations.TryGetValue(dirtyEvent.WatchId, out var registration))
        {
            return;
        }

        registration.Dispatch(dirtyEvent);
    }

    internal async Task ReleaseWatchAsync(string watchId)
    {
        if (_registrations.TryRemove(watchId, out _))
        {
            await InvokePayloadAsync(
                "watch.unsubscribe",
                new WatchIdRequest { WatchId = watchId },
                ProtocolJsonContext.Default.WatchIdRequest,
                CancellationToken.None);
            NotifyActiveWatchStateIfNeeded();
        }
    }

    private void NotifyActiveWatchStateIfNeeded()
    {
        _activeWatchStateChanged?.Invoke(!_registrations.IsEmpty);
    }

    private static string ToWireValue(WatchSource source)
    {
        return source switch
        {
            WatchSource.Depsgraph => "depsgraph",
            WatchSource.Frame => "frame",
            WatchSource.Lifecycle => "lifecycle",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
    }

    private sealed class WatchRegistration : IAsyncDisposable
    {
        private readonly BlenderApi _owner;
        private readonly Func<WatchDirtyEvent, Task> _onDirty;
        private int _disposed;

        public WatchRegistration(BlenderApi owner, string watchId, Func<WatchDirtyEvent, Task> onDirty)
        {
            _owner = owner;
            WatchId = watchId;
            _onDirty = onDirty;
        }

        public string WatchId { get; }

        public void Dispatch(WatchDirtyEvent watchDirtyEvent)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _onDirty(watchDirtyEvent);
                }
                catch
                {
                    // Watch callbacks are user-provided and must not block or tear down the transport loop.
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            await _owner.ReleaseWatchAsync(WatchId);
        }
    }

    private sealed class UnsupportedBusinessEndpoint : IBusinessEndpoint
    {
        public ValueTask<BusinessResponse> InvokeAsync(BusinessRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This BlenderApi instance does not have a business endpoint.");
        }
    }

    private sealed class UnsupportedBlenderRnaApi : IBlenderRnaApi
    {
        public Task<IReadOnlyList<RnaItemRef>> ListAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BlenderArrayReadResult> ReadArrayAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetAsync<T>(string path, T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RnaDescribeResult> DescribeAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<T> CallAsync<T>(string path, string method, params BlenderNamedArg[] kwargs) => throw new NotSupportedException();
        public Task<T> CallAsync<T>(string path, string method, BlenderMethodCall call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnsupportedBlenderOpsApi : IBlenderOpsApi
    {
        public Task<OperatorPollResult> PollAsync(string operatorName, string operatorContext = "EXEC_DEFAULT", BlenderContextOverride? contextOverride = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OperatorCallResult> CallAsync(string operatorName, params BlenderNamedArg[] properties) => throw new NotSupportedException();
        public Task<OperatorCallResult> CallAsync(string operatorName, BlenderOperatorCall call, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnsupportedBlenderObserveApi : IBlenderObserveApi
    {
        public Task<IAsyncDisposable> WatchAsync(string watchId, WatchSource source, string path, Func<WatchDirtyEvent, Task> onDirty, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WatchSnapshot> ReadAsync(string watchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

public sealed class BlenderRnaApi : IBlenderRnaApi
{
    private readonly BlenderApi _owner;

    public BlenderRnaApi(BlenderApi owner)
    {
        _owner = owner;
    }

    public async Task<IReadOnlyList<RnaItemRef>> ListAsync(string path, CancellationToken cancellationToken = default)
    {
        var payload = await _owner.InvokePayloadAsync(
            "rna.list",
            new RnaPathRequest { Path = path },
            ProtocolJsonContext.Default.RnaPathRequest,
            cancellationToken);

        if (!payload.TryGetProperty("items", out var itemsElement))
        {
            return Array.Empty<RnaItemRef>();
        }

        return _owner.TypeResolver.Deserialize(itemsElement, ProtocolJsonContext.Default.ListRnaItemRef) ?? (IReadOnlyList<RnaItemRef>)Array.Empty<RnaItemRef>();
    }

    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var payload = await _owner.InvokePayloadAsync(
            "rna.get",
            new RnaPathRequest { Path = path },
            ProtocolJsonContext.Default.RnaPathRequest,
            cancellationToken);

        if (!payload.TryGetProperty("value", out var valueElement))
        {
            throw new InvalidOperationException($"Missing 'value' in rna.get response for '{path}'.");
        }

        return _owner.TypeResolver.DeserializeRequired<T>(valueElement);
    }

    public async Task<BlenderArrayReadResult> ReadArrayAsync(string path, CancellationToken cancellationToken = default)
    {
        var response = await _owner.InvokeResponseAsync(
            "rna.read_array",
            new RnaPathRequest { Path = path },
            ProtocolJsonContext.Default.RnaPathRequest,
            cancellationToken);

        var metadata = response.Payload
                       ?? throw new InvalidOperationException($"Missing payload in rna.read_array response for '{path}'.");
        var result = _owner.TypeResolver.DeserializeRequired(metadata, ProtocolJsonContext.Default.BlenderArrayReadResult);
        result.RawBytes = response.RawPayload ?? Array.Empty<byte>();
        return result;
    }

    public async Task SetAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        _ = await _owner.InvokePayloadAsync(
            "rna.set",
            new RnaSetRequest
            {
                Path = path,
                Value = _owner.TypeResolver.Serialize(value),
            },
            ProtocolJsonContext.Default.RnaSetRequest,
            cancellationToken);
    }

    public async Task<RnaDescribeResult> DescribeAsync(string path, CancellationToken cancellationToken = default)
    {
        var payload = await _owner.InvokePayloadAsync(
            "rna.describe",
            new RnaPathRequest { Path = path },
            ProtocolJsonContext.Default.RnaPathRequest,
            cancellationToken);

        return _owner.TypeResolver.DeserializeRequired(payload, ProtocolJsonContext.Default.RnaDescribeResult);
    }

    public Task<T> CallAsync<T>(string path, string method, params BlenderNamedArg[] kwargs)
    {
        return CallAsync<T>(
            path,
            method,
            new BlenderMethodCall
            {
                Kwargs = kwargs,
            });
    }

    public async Task<T> CallAsync<T>(string path, string method, BlenderMethodCall call, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);

        var payload = await _owner.InvokePayloadAsync(
            "rna.call",
            new RnaCallRequest
            {
                Path = path,
                Method = method,
                Args = ToJsonElementList(call.Args),
                Kwargs = ToJsonElementDictionary(call.Kwargs),
            },
            ProtocolJsonContext.Default.RnaCallRequest,
            cancellationToken);

        if (!payload.TryGetProperty("return", out var returnElement))
        {
            throw new InvalidOperationException($"Missing 'return' in rna.call response for '{path}.{method}'.");
        }

        if (returnElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (default(T) is null)
            {
                return default!;
            }

            throw new InvalidOperationException($"Missing non-null return value in rna.call response for '{path}.{method}'.");
        }

        return _owner.TypeResolver.DeserializeRequired<T>(returnElement);
    }

    private List<JsonElement>? ToJsonElementList(IReadOnlyList<BlenderValue>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var result = new List<JsonElement>(values.Count);
        foreach (var value in values)
        {
            result.Add(value.RawJsonOrSerialized(_owner.TypeResolver));
        }

        return result;
    }

    private Dictionary<string, JsonElement>? ToJsonElementDictionary(IReadOnlyList<BlenderNamedArg>? namedArgs)
    {
        if (namedArgs is null || namedArgs.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var namedArg in namedArgs)
        {
            if (!result.TryAdd(namedArg.Name, namedArg.Value.RawJsonOrSerialized(_owner.TypeResolver)))
            {
                throw new InvalidOperationException($"Duplicate named argument '{namedArg.Name}'.");
            }
        }

        return result;
    }
}

public sealed class BlenderOpsApi : IBlenderOpsApi
{
    private readonly BlenderApi _owner;

    public BlenderOpsApi(BlenderApi owner)
    {
        _owner = owner;
    }

    public async Task<OperatorPollResult> PollAsync(
        string operatorName,
        string operatorContext = "EXEC_DEFAULT",
        BlenderContextOverride? contextOverride = null,
        CancellationToken cancellationToken = default)
    {
        var payload = await _owner.InvokePayloadAsync(
            "ops.poll",
            new OpsPollRequest
            {
                Operator = operatorName,
                OperatorContext = operatorContext,
                ContextOverride = ToContextOverrideDictionary(contextOverride),
            },
            ProtocolJsonContext.Default.OpsPollRequest,
            cancellationToken);

        return _owner.TypeResolver.DeserializeRequired(payload, ProtocolJsonContext.Default.OperatorPollResult);
    }

    public Task<OperatorCallResult> CallAsync(string operatorName, params BlenderNamedArg[] properties)
    {
        return CallAsync(
            operatorName,
            new BlenderOperatorCall
            {
                Properties = properties,
            });
    }

    public async Task<OperatorCallResult> CallAsync(
        string operatorName,
        BlenderOperatorCall call,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);

        var payload = await _owner.InvokePayloadAsync(
            "ops.call",
            new OpsCallRequest
            {
                Operator = operatorName,
                Properties = ToJsonElementDictionary(call.Properties),
                OperatorContext = call.OperatorContext,
                ContextOverride = ToContextOverrideDictionary(call.ContextOverride),
            },
            ProtocolJsonContext.Default.OpsCallRequest,
            cancellationToken);

        return _owner.TypeResolver.DeserializeRequired(payload, ProtocolJsonContext.Default.OperatorCallResult);
    }

    private Dictionary<string, JsonElement>? ToContextOverrideDictionary(BlenderContextOverride? contextOverride)
    {
        if (contextOverride is null)
        {
            return null;
        }

        var payload = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        AddPathOverride(payload, "active_object", contextOverride.ActiveObject);
        AddPathListOverride(payload, "selected_objects", contextOverride.SelectedObjects);
        AddPathOverride(payload, "object", contextOverride.Object);
        AddPathOverride(payload, "scene", contextOverride.Scene);
        AddPathOverride(payload, "view_layer", contextOverride.ViewLayer);
        AddPathOverride(payload, "collection", contextOverride.Collection);
        AddPathOverride(payload, "window", contextOverride.Window);
        AddPathOverride(payload, "screen", contextOverride.Screen);
        AddPathOverride(payload, "area", contextOverride.Area);
        AddPathOverride(payload, "region", contextOverride.Region);

        return payload.Count == 0 ? null : payload;
    }

    private Dictionary<string, JsonElement>? ToJsonElementDictionary(IReadOnlyList<BlenderNamedArg>? namedArgs)
    {
        if (namedArgs is null || namedArgs.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var namedArg in namedArgs)
        {
            if (!result.TryAdd(namedArg.Name, namedArg.Value.RawJsonOrSerialized(_owner.TypeResolver)))
            {
                throw new InvalidOperationException($"Duplicate named argument '{namedArg.Name}'.");
            }
        }

        return result;
    }

    private void AddPathOverride(Dictionary<string, JsonElement> payload, string key, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        payload[key] = _owner.TypeResolver.SerializeKnown(new PathRef { Path = path });
    }

    private void AddPathListOverride(Dictionary<string, JsonElement> payload, string key, IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0)
        {
            return;
        }

        var items = new List<JsonElement>(paths.Count);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            items.Add(_owner.TypeResolver.SerializeKnown(new PathRef { Path = path }));
        }

        if (items.Count > 0)
        {
            payload[key] = _owner.TypeResolver.SerializeKnown(items);
        }
    }
}

public sealed class BlenderObserveApi : IBlenderObserveApi
{
    private readonly BlenderApi _owner;

    public BlenderObserveApi(BlenderApi owner)
    {
        _owner = owner;
    }

    public Task<IAsyncDisposable> WatchAsync(
        string watchId,
        WatchSource source,
        string path,
        Func<WatchDirtyEvent, Task> onDirty,
        CancellationToken cancellationToken = default)
    {
        return _owner.WatchAsync(watchId, source, path, onDirty, cancellationToken);
    }

    public Task<WatchSnapshot> ReadAsync(string watchId, CancellationToken cancellationToken = default)
    {
        return _owner.ReadWatchAsync(watchId, cancellationToken);
    }
}

internal sealed class BlenderJsonTypeResolver
{
    private readonly List<IJsonTypeInfoResolver> _resolvers;
    private readonly JsonSerializerOptions _options;

    public static BlenderJsonTypeResolver Default { get; } = new();

    public BlenderJsonTypeResolver(BlenderApiOptions? options = null)
    {
        _resolvers = new List<IJsonTypeInfoResolver> { ProtocolJsonContext.Default };
        if (options is not null)
        {
            foreach (var resolver in options.TypeInfoResolvers)
            {
                _resolvers.Add(resolver);
            }
        }

        _options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(_resolvers.ToArray()),
        };
    }

    public JsonElement Serialize<T>(T value)
    {
        var typeInfo = ResolveTypeInfo<T>();
        return JsonSerializer.SerializeToElement(value, typeInfo);
    }

    public JsonElement SerializeKnown(bool value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.Boolean);
    public JsonElement SerializeKnown(int value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.Int32);
    public JsonElement SerializeKnown(long value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.Int64);
    public JsonElement SerializeKnown(double value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.Double);
    public JsonElement SerializeKnown(string value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.String);
    public JsonElement SerializeKnown(bool[] value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.BooleanArray);
    public JsonElement SerializeKnown(int[] value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.Int32Array);
    public JsonElement SerializeKnown(long[] value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.Int64Array);
    public JsonElement SerializeKnown(double[] value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.DoubleArray);
    public JsonElement SerializeKnown(string[] value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.StringArray);
    public JsonElement SerializeKnown(RnaItemRef value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.RnaItemRef);
    public JsonElement SerializeKnown(List<JsonElement> value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.ListJsonElement);
    public JsonElement SerializeKnown(PathRef value) => JsonSerializer.SerializeToElement(value, ProtocolJsonContext.Default.PathRef);

    public T DeserializeRequired<T>(JsonElement jsonElement)
    {
        return Deserialize(jsonElement, ResolveTypeInfo<T>())
            ?? throw new InvalidOperationException($"Unable to deserialize payload to {typeof(T).Name}.");
    }

    public T DeserializeRequired<T>(JsonElement jsonElement, JsonTypeInfo<T> typeInfo)
    {
        return Deserialize(jsonElement, typeInfo)
            ?? throw new InvalidOperationException($"Unable to deserialize payload to {typeof(T).Name}.");
    }

    public T? Deserialize<T>(JsonElement jsonElement, JsonTypeInfo<T> typeInfo)
    {
        return JsonSerializer.Deserialize(jsonElement.GetRawText(), typeInfo);
    }

    private JsonTypeInfo<T> ResolveTypeInfo<T>()
    {
        foreach (var resolver in _resolvers)
        {
            if (resolver.GetTypeInfo(typeof(T), _options) is JsonTypeInfo<T> typeInfo)
            {
                return typeInfo;
            }
        }

        throw new InvalidOperationException($"missing_json_type_info_for_type: {typeof(T).FullName}");
    }
}

internal sealed class RnaPathRequest
{
    public string Path { get; set; } = string.Empty;
}

internal sealed class RnaSetRequest
{
    public string Path { get; set; } = string.Empty;

    public JsonElement Value { get; set; }
}

internal sealed class RnaCallRequest
{
    public string Path { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public List<JsonElement>? Args { get; set; }

    public Dictionary<string, JsonElement>? Kwargs { get; set; }
}

internal sealed class OpsPollRequest
{
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;

    public string OperatorContext { get; set; } = "EXEC_DEFAULT";

    public Dictionary<string, JsonElement>? ContextOverride { get; set; }
}

internal sealed class OpsCallRequest
{
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;

    public Dictionary<string, JsonElement>? Properties { get; set; }

    public string OperatorContext { get; set; } = "EXEC_DEFAULT";

    public Dictionary<string, JsonElement>? ContextOverride { get; set; }
}

internal sealed class WatchSubscribeRequest
{
    public string WatchId { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

internal sealed class WatchIdRequest
{
    public string WatchId { get; set; } = string.Empty;
}

internal sealed class PathRef
{
    public string Path { get; set; } = string.Empty;
}
