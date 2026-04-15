using System.Collections.ObjectModel;
using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class MaterialsPageViewModel : BlenderBridgePageViewModelBase
{
    private bool _isApplyingRemoteState;
    private IAsyncDisposable? _watchSubscription;

    public MaterialsPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect materials.",
            "Waiting for Blender material data.")
    {
    }

    public ObservableCollection<RnaItemRef> Materials { get; } = new();

    [ObservableProperty]
    private RnaItemRef? _selectedMaterial;

    [ObservableProperty]
    private string _materialName = string.Empty;

    [ObservableProperty]
    private bool _useNodes;

    [ObservableProperty]
    private string _newMaterialName = "Material";

    public bool HasSelectedMaterial => SelectedMaterial is not null;

    protected override async Task OnActivatedAsync()
    {
        if (BlenderDataApi is null)
        {
            SetDisconnectedStatus();
            return;
        }

        await RefreshMaterialsAsync();
        await EnsureWatchAsync();
    }

    protected override async Task OnDeactivatedAsync()
    {
        await StopWatchAsync();
    }

    protected override void OnBlenderDataApiChanged()
    {
        RefreshMaterialsCommand.NotifyCanExecuteChanged();
        CreateMaterialCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedMaterial));
    }

    partial void OnSelectedMaterialChanged(RnaItemRef? value)
    {
        OnPropertyChanged(nameof(HasSelectedMaterial));

        if (_isApplyingRemoteState)
        {
            return;
        }

        _ = SelectionChangedAsync();
    }

    partial void OnUseNodesChanged(bool value)
    {
        if (_isApplyingRemoteState || SelectedMaterial is null || BlenderDataApi is null)
        {
            return;
        }

        _ = CommitUseNodesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task RefreshMaterialsAsync()
    {
        return RunPageOperationAsync(RefreshMaterialsCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task CreateMaterialAsync()
    {
        return RunPageOperationAsync(CreateMaterialCoreAsync);
    }

    public Task SelectionChangedAsync()
    {
        return RunPageOperationAsync(SelectionChangedCoreAsync);
    }

    public Task CommitMaterialNameAsync()
    {
        return RunPageOperationAsync(CommitMaterialNameCoreAsync);
    }

    public Task CommitUseNodesAsync()
    {
        return RunPageOperationAsync(CommitUseNodesCoreAsync);
    }

    private bool CanUseBridge() => BlenderDataApi is not null;

    private async Task RefreshMaterialsCoreAsync()
    {
        var blender = RequireBlenderDataApi();
        var previousSelection = SelectedMaterial;
        var items = await blender.ListAsync(BlenderSampleViewModelHelpers.MaterialsPath);
        var nextSelection = previousSelection is null
            ? items.FirstOrDefault()
            : items.FirstOrDefault(item => BlenderSampleViewModelHelpers.ReferenceMatches(item, previousSelection))
              ?? items.FirstOrDefault(item => string.Equals(item.Name, previousSelection.Name, StringComparison.Ordinal))
              ?? items.FirstOrDefault();

        await RunOnUiThreadAsync(() =>
        {
            Materials.Clear();
            foreach (var item in items)
            {
                Materials.Add(item);
            }

            _isApplyingRemoteState = true;
            SelectedMaterial = nextSelection;
            _isApplyingRemoteState = false;
            return Task.CompletedTask;
        });

        if (nextSelection is not null)
        {
            await LoadSelectedMaterialAsync(nextSelection);
        }
        else
        {
            await RunOnUiThreadAsync(() =>
            {
                ClearSelectedMaterial();
                SetConnectedIdleStatus("No materials found.");
                return Task.CompletedTask;
            });
        }
    }

    private async Task SelectionChangedCoreAsync()
    {
        if (SelectedMaterial is null)
        {
            ClearSelectedMaterial();
            return;
        }

        await LoadSelectedMaterialAsync(SelectedMaterial);
    }

    private async Task CreateMaterialCoreAsync()
    {
        var requestedName = string.IsNullOrWhiteSpace(NewMaterialName) ? "Material" : NewMaterialName.Trim();
        var created = await RequireBlenderDataApi().CallAsync<RnaItemRef>(
            BlenderSampleViewModelHelpers.MaterialsPath,
            "new",
            ("name", requestedName));

        NewMaterialName = requestedName;
        await RefreshMaterialsCoreAsync();

        var matched = Materials.FirstOrDefault(item => BlenderSampleViewModelHelpers.ReferenceMatches(item, created))
                      ?? Materials.FirstOrDefault(item => string.Equals(item.Name, created.Name, StringComparison.Ordinal));
        if (matched is not null)
        {
            _isApplyingRemoteState = true;
            SelectedMaterial = matched;
            _isApplyingRemoteState = false;
            await LoadSelectedMaterialAsync(matched);
        }
    }

    private async Task CommitMaterialNameCoreAsync()
    {
        if (_isApplyingRemoteState || SelectedMaterial is null)
        {
            return;
        }

        await RequireBlenderDataApi().SetAsync($"{SelectedMaterial.Path}.name", MaterialName);
        await RefreshMaterialsCoreAsync();
    }

    private async Task CommitUseNodesCoreAsync()
    {
        if (_isApplyingRemoteState || SelectedMaterial is null)
        {
            return;
        }

        await RequireBlenderDataApi().SetAsync($"{SelectedMaterial.Path}.use_nodes", UseNodes);
        SetConnectedIdleStatus($"Updated use_nodes for {SelectedMaterial.Name}.");
    }

    private async Task LoadSelectedMaterialAsync(RnaItemRef material)
    {
        var blender = RequireBlenderDataApi();
        var materialName = await blender.GetAsync<string>($"{material.Path}.name");
        var useNodes = await blender.GetAsync<bool>($"{material.Path}.use_nodes");

        await RunOnUiThreadAsync(() =>
        {
            _isApplyingRemoteState = true;
            MaterialName = materialName;
            UseNodes = useNodes;
            _isApplyingRemoteState = false;

            SetConnectedIdleStatus($"Loaded material {materialName}.");
            return Task.CompletedTask;
        });
    }

    private async Task EnsureWatchAsync()
    {
        if (_watchSubscription is not null || BlenderDataApi is null || !IsActive)
        {
            return;
        }

        _watchSubscription = await BlenderDataApi.WatchAsync(
            "materials-page",
            WatchSource.Depsgraph,
            BlenderSampleViewModelHelpers.MaterialsPath,
            async _ => await RefreshMaterialsAsync());
    }

    private async Task StopWatchAsync()
    {
        if (_watchSubscription is null)
        {
            return;
        }

        await _watchSubscription.DisposeAsync();
        _watchSubscription = null;
    }

    private void ClearSelectedMaterial()
    {
        _isApplyingRemoteState = true;
        MaterialName = string.Empty;
        UseNodes = false;
        _isApplyingRemoteState = false;
    }
}
