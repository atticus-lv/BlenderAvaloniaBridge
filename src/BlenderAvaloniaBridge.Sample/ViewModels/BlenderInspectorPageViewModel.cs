using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Buffers;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class BlenderInspectorPageViewModel : ObservableObject
{
    private const string SceneRnaType = "bpy.types.Scene";

    private IBusinessEndpoint? _businessEndpoint;
    private bool _isApplyingRemoteState;

    public ObservableCollection<BlenderObjectListItem> Objects { get; } = new();

    [ObservableProperty]
    private BlenderObjectListItem? _selectedObject;

    [ObservableProperty]
    private string _statusText = "Desktop window mode. Start the bridge from Blender to inspect objects.";

    [ObservableProperty]
    private string _bridgeStatusText = "Bridge disconnected";

    [ObservableProperty]
    private string _selectedReferenceText = "No object selected";

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _locationX = "0";

    [ObservableProperty]
    private string _locationY = "0";

    [ObservableProperty]
    private string _locationZ = "0";

    public bool HasSelectedObject => SelectedObject is not null;

    public void AttachBusinessEndpoint(IBusinessEndpoint? businessEndpoint)
    {
        _businessEndpoint = businessEndpoint;
        BridgeStatusText = businessEndpoint is null ? "Bridge disconnected" : "Bridge connected";
        StatusText = businessEndpoint is null
            ? "Desktop window mode. Start the bridge from Blender to inspect objects."
            : "Waiting for Blender actions.";
        UpdateCommandStates();

        if (businessEndpoint is not null)
        {
            RefreshObjects();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    private void RefreshObjects()
    {
        _ = RunBusinessOperationAsync(RefreshObjectsCoreAsync);
    }

    private async Task RefreshObjectsCoreAsync()
    {
        if (_businessEndpoint is null)
        {
            StatusText = "Bridge is not attached.";
            return;
        }

        BridgeStatusText = "Refreshing scene objects...";
        var response = await _businessEndpoint.InvokeAsync(
            new BusinessRequest("scene.objects.list", CreateEmptyPayload()));
        HandleCollectionResponse(response);
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    private void AddCube()
    {
        _ = RunBusinessOperationAsync(AddCubeCoreAsync);
    }

    private async Task AddCubeCoreAsync()
    {
        if (_businessEndpoint is null)
        {
            StatusText = "Bridge is not attached.";
            return;
        }

        var response = await _businessEndpoint.InvokeAsync(
            new BusinessRequest(
                "operator.call",
                CreateOperatorCallPayload(
                    "mesh.primitive_cube_add",
                    executionContext: "EXEC_DEFAULT",
                    properties: [new KeyValuePair<string, JsonElement>("size", JsonSerializer.SerializeToElement(2.0, ProtocolJsonContext.Default.Double))])));

        HandleOperatorResponse(response);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void Duplicate()
    {
        _ = RunBusinessOperationAsync(DuplicateCoreAsync);
    }

    private async Task DuplicateCoreAsync()
    {
        if (_businessEndpoint is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        var response = await _businessEndpoint.InvokeAsync(
            new BusinessRequest(
                "operator.call",
                CreateOperatorCallPayload(
                    "object.duplicate_move",
                    executionContext: "EXEC_DEFAULT",
                    target: SelectedObject.RnaRef)));

        HandleOperatorResponse(response);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void ViewSelected()
    {
        _ = RunBusinessOperationAsync(ViewSelectedCoreAsync);
    }

    private async Task ViewSelectedCoreAsync()
    {
        if (_businessEndpoint is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        var response = await _businessEndpoint.InvokeAsync(
            new BusinessRequest(
                "operator.call",
                CreateOperatorCallPayload(
                    "view3d.view_selected",
                    executionContext: "EXEC_DEFAULT",
                    target: SelectedObject.RnaRef)));

        HandleOperatorResponse(response);
    }

    public async Task SelectionChangedAsync()
    {
        OnPropertyChanged(nameof(HasSelectedObject));
        UpdateCommandStates();
        UpdateSelectedReferenceText();

        if (_isApplyingRemoteState || _businessEndpoint is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        await RunBusinessOperationAsync(() => LoadSelectedObjectPropertiesAsync(SelectedObject.RnaRef));
    }

    public async Task CommitNameAsync()
    {
        if (_isApplyingRemoteState || _businessEndpoint is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        await RunBusinessOperationAsync(async () =>
        {
            var response = await _businessEndpoint.InvokeAsync(
                new BusinessRequest(
                    "object.property.set",
                    CreatePropertySetPayload(SelectedObject.RnaRef, "name", writer => writer.WriteStringValue(ObjectName))));

            HandlePropertyResponse(response);
        });
    }

    public async Task CommitLocationAsync()
    {
        if (_isApplyingRemoteState || _businessEndpoint is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        if (!double.TryParse(LocationX, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(LocationY, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !double.TryParse(LocationZ, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            StatusText = "Location expects three numeric values.";
            return;
        }

        await RunBusinessOperationAsync(async () =>
        {
            var response = await _businessEndpoint.InvokeAsync(
                new BusinessRequest(
                    "object.property.set",
                    CreateLocationSetPayload(SelectedObject.RnaRef, x, y, z)));

            HandlePropertyResponse(response);
        });
    }

    public void SetBridgeStatus(string status)
    {
        BridgeStatusText = status;
    }

    private bool CanOperateOnSelection() => _businessEndpoint is not null && SelectedObject is not null;

    private bool CanUseBridge() => _businessEndpoint is not null;

    private async Task LoadSelectedObjectPropertiesAsync(BlenderRnaRef rnaRef)
    {
        if (_businessEndpoint is null)
        {
            return;
        }

        var nameResponse = await _businessEndpoint.InvokeAsync(
            new BusinessRequest(
                "object.property.get",
                CreatePropertyGetPayload(rnaRef, "name")));
        HandlePropertyResponse(nameResponse);

        var locationResponse = await _businessEndpoint.InvokeAsync(
            new BusinessRequest(
                "object.property.get",
                CreatePropertyGetPayload(rnaRef, "location")));
        HandlePropertyResponse(locationResponse);

        BridgeStatusText = $"Loading properties for {rnaRef.Name}";
    }

    private void HandleCollectionResponse(BusinessResponse response)
    {
        if (!response.Ok)
        {
            StatusText = response.Error?.Message ?? "Failed to load scene objects.";
            return;
        }

        if (!TryGetPayload(response, out var payload) || !payload.TryGetProperty("items", out var itemsElement))
        {
            StatusText = "Collection result payload is missing items.";
            return;
        }

        var previousSelection = SelectedObject?.RnaRef;
        Objects.Clear();
        foreach (var item in EnumerateCollectionItems(itemsElement))
        {
            Objects.Add(
                new BlenderObjectListItem(
                    item.RnaRef,
                    item.Label,
                    item.ObjectType,
                    item.IsActive));
        }
        BridgeStatusText = $"Loaded {Objects.Count} object(s)";

        BlenderObjectListItem? nextSelection = null;
        if (previousSelection is not null)
        {
            nextSelection = FindByReference(previousSelection);
        }
        nextSelection ??= Objects.FirstOrDefault(item => item.IsActive);
        nextSelection ??= Objects.FirstOrDefault();

        _isApplyingRemoteState = true;
        SelectedObject = nextSelection;
        _isApplyingRemoteState = false;

        UpdateSelectedReferenceText();
        UpdateCommandStates();
        OnPropertyChanged(nameof(HasSelectedObject));

        if (SelectedObject?.RnaRef is not null)
        {
            _ = LoadSelectedObjectPropertiesAsync(SelectedObject.RnaRef);
        }
        else
        {
            ClearPropertyEditors();
            BridgeStatusText = "Scene contains no objects.";
        }
    }

    private void HandlePropertyResponse(BusinessResponse response)
    {
        if (!response.Ok)
        {
            StatusText = response.Error?.Message ?? "Failed to read property.";
            return;
        }

        if (!TryGetPayload(response, out var payload)
            || !payload.TryGetProperty("target", out var targetElement)
            || !payload.TryGetProperty("data_path", out var dataPathElement)
            || dataPathElement.ValueKind != JsonValueKind.String)
        {
            StatusText = "Property result payload is incomplete.";
            return;
        }

        if (!TryReadRnaRef(targetElement, out var target))
        {
            StatusText = "Property result target is invalid.";
            return;
        }

        if (SelectedObject?.RnaRef is null || target is null || !ReferenceMatches(SelectedObject.RnaRef, target))
        {
            return;
        }

        var dataPath = dataPathElement.GetString() ?? string.Empty;
        _isApplyingRemoteState = true;
        if (dataPath == "name" && payload.TryGetProperty("value", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
        {
            ObjectName = nameElement.GetString() ?? string.Empty;
            SelectedObject.Label = ObjectName;
            SelectedObject.UpdateReference(target);
        }
        else if (dataPath == "location" && payload.TryGetProperty("value", out var locationElement) && locationElement.ValueKind == JsonValueKind.Array)
        {
            var components = locationElement.EnumerateArray()
                .Select(value => value.GetDouble().ToString("0.###", CultureInfo.InvariantCulture))
                .ToArray();
            if (components.Length == 3)
            {
                LocationX = components[0];
                LocationY = components[1];
                LocationZ = components[2];
            }
        }
        _isApplyingRemoteState = false;

        UpdateSelectedReferenceText();
        BridgeStatusText = $"Updated {dataPath}";
    }

    private void HandleOperatorResponse(BusinessResponse response)
    {
        if (!TryGetPayload(response, out var payload))
        {
            StatusText = response.Ok ? "operator.call: no result" : response.Error?.Message ?? "operator.call failed";
            return;
        }

        var operatorName = payload.TryGetProperty("operator", out var operatorElement) && operatorElement.ValueKind == JsonValueKind.String
            ? operatorElement.GetString() ?? "operator.call"
            : "operator.call";
        var result = payload.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.Array
            ? string.Join(", ", resultElement.EnumerateArray().Select(value => value.GetString() ?? string.Empty))
            : "no result";

        StatusText = response.Ok
            ? $"{operatorName}: {result}"
            : $"{operatorName}: {response.Error?.Message ?? "failed"}";

        if (response.Ok)
        {
            RefreshObjects();
        }
    }

    private async Task RunBusinessOperationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Business request canceled.";
            BridgeStatusText = "Bridge request canceled";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            BridgeStatusText = "Bridge request failed";
        }
    }

    private static JsonElement CreateEmptyPayload()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static JsonElement CreatePropertyGetPayload(BlenderRnaRef target, string dataPath)
    {
        return WritePayload(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("target");
            JsonSerializer.Serialize(writer, target, ProtocolJsonContext.Default.BlenderRnaRef);
            writer.WriteString("data_path", dataPath);
            writer.WriteEndObject();
        });
    }

    private static JsonElement CreatePropertySetPayload(BlenderRnaRef target, string dataPath, Action<Utf8JsonWriter> writeValue)
    {
        return WritePayload(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("target");
            JsonSerializer.Serialize(writer, target, ProtocolJsonContext.Default.BlenderRnaRef);
            writer.WriteString("data_path", dataPath);
            writer.WritePropertyName("value");
            writeValue(writer);
            writer.WriteEndObject();
        });
    }

    private static JsonElement CreateLocationSetPayload(BlenderRnaRef target, double x, double y, double z)
    {
        return CreatePropertySetPayload(target, "location", writer =>
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(x);
            writer.WriteNumberValue(y);
            writer.WriteNumberValue(z);
            writer.WriteEndArray();
        });
    }

    private static JsonElement CreateOperatorCallPayload(
        string operatorName,
        string executionContext,
        BlenderRnaRef? target = null,
        IEnumerable<KeyValuePair<string, JsonElement>>? properties = null)
    {
        return WritePayload(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("operator", operatorName);
            writer.WriteString("execution_context", executionContext);

            if (target is not null)
            {
                writer.WritePropertyName("target");
                JsonSerializer.Serialize(writer, target, ProtocolJsonContext.Default.BlenderRnaRef);
            }

            if (properties is not null)
            {
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                foreach (var (name, value) in properties)
                {
                    writer.WritePropertyName(name);
                    value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });
    }

    private static JsonElement WritePayload(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return JsonDocument.Parse(buffer.WrittenMemory).RootElement.Clone();
    }

    private static IEnumerable<ParsedCollectionItem> EnumerateCollectionItems(JsonElement itemsElement)
    {
        if (itemsElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var itemElement in itemsElement.EnumerateArray())
        {
            if (!itemElement.TryGetProperty("rna_ref", out var rnaRefElement)
                || !TryReadRnaRef(rnaRefElement, out var rnaRef))
            {
                continue;
            }

            var label = itemElement.TryGetProperty("label", out var labelElement) && labelElement.ValueKind == JsonValueKind.String
                ? labelElement.GetString() ?? string.Empty
                : string.Empty;

            var objectType = "?";
            var isActive = false;
            if (itemElement.TryGetProperty("meta", out var metaElement) && metaElement.ValueKind == JsonValueKind.Object)
            {
                if (metaElement.TryGetProperty("object_type", out var objectTypeElement) && objectTypeElement.ValueKind == JsonValueKind.String)
                {
                    objectType = objectTypeElement.GetString() ?? "?";
                }

                if (metaElement.TryGetProperty("is_active", out var isActiveElement)
                    && (isActiveElement.ValueKind == JsonValueKind.True || isActiveElement.ValueKind == JsonValueKind.False))
                {
                    isActive = isActiveElement.GetBoolean();
                }
            }

            yield return new ParsedCollectionItem(rnaRef, label, objectType, isActive);
        }
    }

    private static bool TryReadRnaRef(JsonElement element, out BlenderRnaRef rnaRef)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            rnaRef = null!;
            return false;
        }

        if (!element.TryGetProperty("rna_type", out var rnaTypeElement) || rnaTypeElement.ValueKind != JsonValueKind.String)
        {
            rnaRef = null!;
            return false;
        }

        rnaRef = new BlenderRnaRef
        {
            RnaType = rnaTypeElement.GetString() ?? string.Empty,
            IdType = element.TryGetProperty("id_type", out var idTypeElement) && idTypeElement.ValueKind == JsonValueKind.String
                ? idTypeElement.GetString()
                : null,
            Name = element.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null,
            SessionUid = TryReadInt64(element, "session_uid", out var sessionUid) ? sessionUid : null,
        };
        return true;
    }

    private static bool TryReadInt64(JsonElement element, string propertyName, out long value)
    {
        if (element.TryGetProperty(propertyName, out var propertyElement) && propertyElement.ValueKind == JsonValueKind.Number)
        {
            return propertyElement.TryGetInt64(out value);
        }

        value = 0;
        return false;
    }

    private BlenderObjectListItem? FindByReference(BlenderRnaRef rnaRef)
    {
        return Objects.FirstOrDefault(item => ReferenceMatches(item.RnaRef, rnaRef));
    }

    private void UpdateSelectedReferenceText()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            SelectedReferenceText = "No object selected";
            return;
        }

        SelectedReferenceText =
            $"{rnaRef.RnaType} | {rnaRef.Name} | session_uid={rnaRef.SessionUid?.ToString() ?? "0"}";
    }

    private void ClearPropertyEditors()
    {
        _isApplyingRemoteState = true;
        ObjectName = string.Empty;
        LocationX = "0";
        LocationY = "0";
        LocationZ = "0";
        _isApplyingRemoteState = false;
    }

    private void UpdateCommandStates()
    {
        RefreshObjectsCommand.NotifyCanExecuteChanged();
        AddCubeCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        ViewSelectedCommand.NotifyCanExecuteChanged();
    }

    private static bool ReferenceMatches(BlenderRnaRef left, BlenderRnaRef right)
    {
        if (left.SessionUid.HasValue && right.SessionUid.HasValue && left.SessionUid.Value != 0 && right.SessionUid.Value != 0)
        {
            return left.SessionUid.Value == right.SessionUid.Value;
        }

        return string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
               string.Equals(left.RnaType, right.RnaType, StringComparison.Ordinal);
    }

    private static bool TryGetPayload(BusinessResponse response, out JsonElement payload)
    {
        if (response.Payload is JsonElement payloadElement)
        {
            payload = payloadElement;
            return true;
        }

        payload = default;
        return false;
    }

    private sealed record ParsedCollectionItem(BlenderRnaRef RnaRef, string Label, string ObjectType, bool IsActive);
}
