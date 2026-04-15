using System.Collections.ObjectModel;
using System.Globalization;
using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class BlenderObjectsPageViewModel : BlenderBridgePageViewModelBase
{
    private bool _isApplyingRemoteState;

    public BlenderObjectsPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect scene objects.",
            "Waiting for Blender object data.")
    {
    }

    public ObservableCollection<BlenderObjectListItem> Objects { get; } = new();

    [ObservableProperty]
    private BlenderObjectListItem? _selectedObject;

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

    protected override Task OnActivatedAsync()
    {
        if (BlenderDataApi is null)
        {
            SetDisconnectedStatus();
            return Task.CompletedTask;
        }

        return RefreshObjectsAsync();
    }

    protected override void OnBlenderDataApiChanged()
    {
        RefreshObjectsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedObject));
    }

    partial void OnSelectedObjectChanged(BlenderObjectListItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedObject));
        UpdateSelectedReferenceText();

        if (_isApplyingRemoteState)
        {
            return;
        }

        _ = SelectionChangedAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task RefreshObjectsAsync()
    {
        return RunPageOperationAsync(RefreshObjectsCoreAsync);
    }

    public Task SelectionChangedAsync()
    {
        return RunPageOperationAsync(SelectionChangedCoreAsync);
    }

    public Task CommitNameAsync()
    {
        return RunPageOperationAsync(CommitNameCoreAsync);
    }

    public Task CommitLocationAsync()
    {
        return RunPageOperationAsync(CommitLocationCoreAsync);
    }

    private bool CanUseBridge() => BlenderDataApi is not null;

    private async Task RefreshObjectsCoreAsync()
    {
        var blender = RequireBlenderDataApi();
        BridgeStatusText = "Refreshing scene objects...";

        var previousSelection = SelectedObject?.RnaRef;
        var objectItems = await BlenderSampleViewModelHelpers.LoadSceneObjectItemsAsync(blender);

        Objects.Clear();
        foreach (var item in objectItems)
        {
            Objects.Add(item);
        }

        BridgeStatusText = $"Loaded {Objects.Count} object(s)";

        BlenderObjectListItem? nextSelection = null;
        if (previousSelection is not null)
        {
            nextSelection = Objects.FirstOrDefault(item => BlenderSampleViewModelHelpers.ReferenceMatches(item.RnaRef, previousSelection));
        }

        nextSelection ??= Objects.FirstOrDefault(item => item.IsActive);
        nextSelection ??= Objects.FirstOrDefault();

        _isApplyingRemoteState = true;
        SelectedObject = nextSelection;
        _isApplyingRemoteState = false;

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

    private async Task SelectionChangedCoreAsync()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            ClearPropertyEditors();
            SelectedReferenceText = "No object selected";
            return;
        }

        await LoadSelectedObjectPropertiesAsync(rnaRef);
    }

    private async Task CommitNameCoreAsync()
    {
        if (_isApplyingRemoteState || BlenderDataApi is null || SelectedObject is null)
        {
            return;
        }

        await BlenderDataApi.SetAsync($"{SelectedObject.RnaRef.Path}.name", ObjectName);
        await RefreshObjectsCoreAsync();
    }

    private async Task CommitLocationCoreAsync()
    {
        if (_isApplyingRemoteState || BlenderDataApi is null || SelectedObject is null)
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

        await BlenderDataApi.SetAsync($"{SelectedObject.RnaRef.Path}.location", new[] { x, y, z });
        await LoadSelectedObjectPropertiesAsync(SelectedObject.RnaRef);
    }

    private async Task LoadSelectedObjectPropertiesAsync(RnaItemRef rnaRef)
    {
        var blender = RequireBlenderDataApi();
        var objectName = await blender.GetAsync<string>($"{rnaRef.Path}.name");
        var location = await blender.GetAsync<double[]>($"{rnaRef.Path}.location");

        _isApplyingRemoteState = true;
        ObjectName = objectName;
        if (location.Length >= 3)
        {
            LocationX = location[0].ToString("0.###", CultureInfo.InvariantCulture);
            LocationY = location[1].ToString("0.###", CultureInfo.InvariantCulture);
            LocationZ = location[2].ToString("0.###", CultureInfo.InvariantCulture);
        }
        else
        {
            LocationX = "0";
            LocationY = "0";
            LocationZ = "0";
        }

        _isApplyingRemoteState = false;

        UpdateSelectedReferenceText();
        SetConnectedIdleStatus($"Loaded properties for {rnaRef.Name}.");
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
}
