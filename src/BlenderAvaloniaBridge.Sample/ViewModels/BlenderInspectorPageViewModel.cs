using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class BlenderInspectorPageViewModel : ObservableObject
{
    private const string SceneObjectsPath = "bpy.context.scene.objects";
    private IBlenderDataApi? _blenderDataApi;
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

    public void AttachBlenderDataApi(IBlenderDataApi? blenderDataApi)
    {
        _blenderDataApi = blenderDataApi;
        BridgeStatusText = blenderDataApi is null ? "Bridge disconnected" : "Bridge connected";
        StatusText = blenderDataApi is null
            ? "Desktop window mode. Start the bridge from Blender to inspect objects."
            : "Waiting for Blender actions.";
        UpdateCommandStates();

        if (blenderDataApi is not null)
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
        if (_blenderDataApi is null)
        {
            StatusText = "Bridge is not attached.";
            return;
        }

        BridgeStatusText = "Refreshing scene objects...";
        var items = await _blenderDataApi.ListAsync(SceneObjectsPath);
        var previousSelection = SelectedObject?.RnaRef;

        Objects.Clear();
        foreach (var item in items)
        {
            var metadata = item.Metadata;
            var objectType = metadata.HasValue && metadata.Value.TryGetProperty("objectType", out var objectTypeElement)
                ? objectTypeElement.GetString() ?? "?"
                : "?";
            var isActive = metadata.HasValue
                && metadata.Value.TryGetProperty("isActive", out var isActiveElement)
                && isActiveElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                && isActiveElement.GetBoolean();

            Objects.Add(new BlenderObjectListItem(item, item.Label, objectType, isActive));
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
            await LoadSelectedObjectPropertiesAsync(SelectedObject.RnaRef);
        }
        else
        {
            ClearPropertyEditors();
            BridgeStatusText = "Scene contains no objects.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    private void AddCube()
    {
        _ = RunBusinessOperationAsync(AddCubeCoreAsync);
    }

    private async Task AddCubeCoreAsync()
    {
        if (_blenderDataApi is null)
        {
            StatusText = "Bridge is not attached.";
            return;
        }

        var response = await _blenderDataApi.CallOperatorAsync(
            "mesh.primitive_cube_add",
            ("size", 2.0));

        HandleOperatorResponse(response);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void Duplicate()
    {
        _ = RunBusinessOperationAsync(DuplicateCoreAsync);
    }

    private async Task DuplicateCoreAsync()
    {
        if (_blenderDataApi is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        var response = await _blenderDataApi.CallOperatorAsync(
            "object.duplicate_move",
            new BlenderOperatorCall
            {
                ContextOverride = BuildSelectionContextOverride(SelectedObject.RnaRef.Path),
            });

        HandleOperatorResponse(response);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void ViewSelected()
    {
        _ = RunBusinessOperationAsync(ViewSelectedCoreAsync);
    }

    private async Task ViewSelectedCoreAsync()
    {
        if (_blenderDataApi is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        var response = await _blenderDataApi.CallOperatorAsync(
            "view3d.view_selected",
            new BlenderOperatorCall
            {
                ContextOverride = BuildSelectionContextOverride(SelectedObject.RnaRef.Path),
            });

        HandleOperatorResponse(response);
    }

    public async Task SelectionChangedAsync()
    {
        OnPropertyChanged(nameof(HasSelectedObject));
        UpdateCommandStates();
        UpdateSelectedReferenceText();

        if (_isApplyingRemoteState || _blenderDataApi is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        await RunBusinessOperationAsync(() => LoadSelectedObjectPropertiesAsync(SelectedObject.RnaRef));
    }

    public async Task CommitNameAsync()
    {
        if (_isApplyingRemoteState || _blenderDataApi is null || SelectedObject?.RnaRef is null)
        {
            return;
        }

        await RunBusinessOperationAsync(async () =>
        {
            await _blenderDataApi.SetAsync($"{SelectedObject.RnaRef.Path}.name", ObjectName);
            await RefreshObjectsCoreAsync();
        });
    }

    public async Task CommitLocationAsync()
    {
        if (_isApplyingRemoteState || _blenderDataApi is null || SelectedObject?.RnaRef is null)
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
            await _blenderDataApi.SetAsync($"{SelectedObject.RnaRef.Path}.location", new[] { x, y, z });
            await LoadSelectedObjectPropertiesAsync(SelectedObject.RnaRef);
        });
    }

    public void SetBridgeStatus(string status)
    {
        BridgeStatusText = status;
    }

    private bool CanOperateOnSelection() => _blenderDataApi is not null && SelectedObject is not null;

    private bool CanUseBridge() => _blenderDataApi is not null;

    private async Task LoadSelectedObjectPropertiesAsync(RnaItemRef rnaRef)
    {
        if (_blenderDataApi is null)
        {
            return;
        }

        var objectName = await _blenderDataApi.GetAsync<string>($"{rnaRef.Path}.name");
        var location = await _blenderDataApi.GetAsync<double[]>($"{rnaRef.Path}.location");

        _isApplyingRemoteState = true;
        ObjectName = objectName;
        if (location.Length >= 3)
        {
            LocationX = location[0].ToString("0.###", CultureInfo.InvariantCulture);
            LocationY = location[1].ToString("0.###", CultureInfo.InvariantCulture);
            LocationZ = location[2].ToString("0.###", CultureInfo.InvariantCulture);
        }
        _isApplyingRemoteState = false;

        UpdateSelectedReferenceText();
        BridgeStatusText = $"Loading properties for {rnaRef.Name}";
    }

    private void HandleOperatorResponse(OperatorCallResult response)
    {
        var result = response.Result.Count > 0 ? string.Join(", ", response.Result) : "no result";
        StatusText = $"{response.OperatorName}: {result}";
        RefreshObjects();
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

    private BlenderObjectListItem? FindByReference(RnaItemRef rnaRef)
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
            $"{rnaRef.RnaType} | {rnaRef.Name} | session_uid={rnaRef.SessionUid?.ToString() ?? "0"} | {rnaRef.Path}";
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

    private static bool ReferenceMatches(RnaItemRef left, RnaItemRef right)
    {
        if (left.SessionUid.HasValue && right.SessionUid.HasValue && left.SessionUid.Value != 0 && right.SessionUid.Value != 0)
        {
            return left.SessionUid.Value == right.SessionUid.Value;
        }

        return string.Equals(left.Path, right.Path, StringComparison.Ordinal)
               || (string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                   && string.Equals(left.RnaType, right.RnaType, StringComparison.Ordinal));
    }

    private static BlenderContextOverride BuildSelectionContextOverride(string path)
    {
        return new BlenderContextOverride
        {
            ActiveObject = path,
            SelectedObjects = new[] { path },
        };
    }
}
