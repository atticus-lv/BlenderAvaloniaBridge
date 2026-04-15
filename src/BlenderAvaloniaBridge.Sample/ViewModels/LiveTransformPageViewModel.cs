using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class LiveTransformPageViewModel : BlenderBridgePageViewModelBase
{
    private bool _isApplyingRemoteState;
    private bool _isChangingLiveWatch;
    private IAsyncDisposable? _watchSubscription;
    private string? _currentWatchPath;
    private readonly SemaphoreSlim _transformRefreshGate = new(1, 1);
    private int _pendingTransformRefresh;

    public LiveTransformPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect live transforms.",
            "Select an object, then enable live watch.")
    {
    }

    public ObservableCollection<BlenderObjectListItem> Objects { get; } = new();

    [ObservableProperty]
    private BlenderObjectListItem? _selectedObject;

    [ObservableProperty]
    private string _selectedReferenceText = "No object selected";

    [ObservableProperty]
    private bool _isLiveWatchEnabled;

    [ObservableProperty]
    private string _locationX = "0";

    [ObservableProperty]
    private string _locationY = "0";

    [ObservableProperty]
    private string _locationZ = "0";

    [ObservableProperty]
    private string _rotationX = "0";

    [ObservableProperty]
    private string _rotationY = "0";

    [ObservableProperty]
    private string _rotationZ = "0";

    [ObservableProperty]
    private string _scaleX = "1";

    [ObservableProperty]
    private string _scaleY = "1";

    [ObservableProperty]
    private string _scaleZ = "1";

    public bool CanEnableLiveWatch => BlenderDataApi is not null && SelectedObject is not null;

    protected override async Task OnActivatedAsync()
    {
        if (BlenderDataApi is null)
        {
            SetDisconnectedStatus();
            return;
        }

        await RefreshObjectsAsync();

        if (IsLiveWatchEnabled && SelectedObject?.RnaRef is not null)
        {
            await StartWatchAsync(SelectedObject.RnaRef);
        }
    }

    protected override async Task OnDeactivatedAsync()
    {
        await StopWatchAsync();
    }

    protected override void OnBlenderDataApiChanged()
    {
        RefreshObjectsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEnableLiveWatch));
    }

    partial void OnSelectedObjectChanged(BlenderObjectListItem? value)
    {
        UpdateSelectedReferenceText();
        OnPropertyChanged(nameof(CanEnableLiveWatch));

        if (_isApplyingRemoteState)
        {
            return;
        }

        _ = SelectionChangedAsync();
    }

    partial void OnIsLiveWatchEnabledChanged(bool value)
    {
        if (_isChangingLiveWatch || !IsActive)
        {
            return;
        }

        _ = RunPageOperationAsync(() => HandleLiveWatchToggleAsync(value));
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

    private bool CanUseBridge() => BlenderDataApi is not null;

    private async Task RefreshObjectsCoreAsync()
    {
        var blender = RequireBlenderDataApi();
        var previousSelection = SelectedObject?.RnaRef;
        var objectItems = await BlenderSampleViewModelHelpers.LoadSceneObjectItemsAsync(blender);

        Objects.Clear();
        foreach (var item in objectItems)
        {
            Objects.Add(item);
        }

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
            await LoadTransformSnapshotAsync(SelectedObject.RnaRef);
            if (IsLiveWatchEnabled)
            {
                await StartWatchAsync(SelectedObject.RnaRef);
            }
        }
        else
        {
            await StopWatchAsync();
            ClearTransformValues();
        }
    }

    private async Task SelectionChangedCoreAsync()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            await StopWatchAsync();
            ClearTransformValues();
            return;
        }

        await LoadTransformSnapshotAsync(rnaRef);

        if (IsLiveWatchEnabled)
        {
            await StartWatchAsync(rnaRef);
        }
    }

    private async Task HandleLiveWatchToggleAsync(bool enabled)
    {
        if (!enabled)
        {
            await StopWatchAsync();
            SetConnectedIdleStatus("Live watch disabled.");
            return;
        }

        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            _isChangingLiveWatch = true;
            IsLiveWatchEnabled = false;
            _isChangingLiveWatch = false;
            StatusText = "Select an object before enabling live watch.";
            return;
        }

        await StartWatchAsync(rnaRef);
    }

    private async Task StartWatchAsync(RnaItemRef rnaRef)
    {
        await StopWatchAsync();

        if (!IsActive || BlenderDataApi is null || !IsLiveWatchEnabled)
        {
            return;
        }

        var watchId = $"live-transform-{rnaRef.SessionUid?.ToString() ?? rnaRef.Name}";
        Volatile.Write(ref _currentWatchPath, rnaRef.Path);
        _watchSubscription = await BlenderDataApi.WatchAsync(
            watchId,
            WatchSource.Depsgraph,
            rnaRef.Path,
            async _dirtyEvent =>
            {
                if (!string.Equals(Volatile.Read(ref _currentWatchPath), rnaRef.Path, StringComparison.Ordinal))
                {
                    return;
                }

                _ = RunOnUiThreadAsync(() =>
                {
                    BridgeStatusText = "Live transform dirty event received";
                    return Task.CompletedTask;
                });
                await QueueTransformRefreshAsync(rnaRef);
            });

        SetConnectedIdleStatus($"Live watch enabled for {rnaRef.Name}.");
    }

    private async Task StopWatchAsync()
    {
        if (_watchSubscription is null)
        {
            Volatile.Write(ref _currentWatchPath, null);
            return;
        }

        await _watchSubscription.DisposeAsync();
        _watchSubscription = null;
        Volatile.Write(ref _currentWatchPath, null);
    }

    private async Task QueueTransformRefreshAsync(RnaItemRef rnaRef)
    {
        if (!await _transformRefreshGate.WaitAsync(0))
        {
            Interlocked.Exchange(ref _pendingTransformRefresh, 1);
            return;
        }

        try
        {
            do
            {
                Interlocked.Exchange(ref _pendingTransformRefresh, 0);
                if (string.Equals(Volatile.Read(ref _currentWatchPath), rnaRef.Path, StringComparison.Ordinal))
                {
                    await LoadTransformSnapshotAsync(rnaRef);
                }
            }
            while (Interlocked.Exchange(ref _pendingTransformRefresh, 0) == 1);
        }
        finally
        {
            _transformRefreshGate.Release();
        }
    }

    private async Task LoadTransformSnapshotAsync(RnaItemRef rnaRef)
    {
        var blender = RequireBlenderDataApi();
        var location = await blender.GetAsync<double[]>($"{rnaRef.Path}.location");
        var rotation = await blender.GetAsync<double[]>($"{rnaRef.Path}.rotation_euler");
        var scale = await blender.GetAsync<double[]>($"{rnaRef.Path}.scale");
        ApplyVector(location, "0", out var locationX, out var locationY, out var locationZ);
        ApplyVector(rotation, "0", out var rotationX, out var rotationY, out var rotationZ);
        ApplyVector(scale, "1", out var scaleX, out var scaleY, out var scaleZ);

        await RunOnUiThreadAsync(() =>
        {
            _isApplyingRemoteState = true;
            LocationX = locationX;
            LocationY = locationY;
            LocationZ = locationZ;
            RotationX = rotationX;
            RotationY = rotationY;
            RotationZ = rotationZ;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
            _isApplyingRemoteState = false;

            UpdateSelectedReferenceText();
            SetConnectedIdleStatus($"Transform synced for {rnaRef.Name}.");
            return Task.CompletedTask;
        });
    }

    private static void ApplyVector(double[] values, string fallback, out string x, out string y, out string z)
    {
        x = values.Length > 0 ? values[0].ToString("0.###", CultureInfo.InvariantCulture) : fallback;
        y = values.Length > 1 ? values[1].ToString("0.###", CultureInfo.InvariantCulture) : fallback;
        z = values.Length > 2 ? values[2].ToString("0.###", CultureInfo.InvariantCulture) : fallback;
    }

    private void ClearTransformValues()
    {
        _isApplyingRemoteState = true;
        LocationX = "0";
        LocationY = "0";
        LocationZ = "0";
        RotationX = "0";
        RotationY = "0";
        RotationZ = "0";
        ScaleX = "1";
        ScaleY = "1";
        ScaleZ = "1";
        _isApplyingRemoteState = false;
        SelectedReferenceText = "No object selected";
    }

    private void UpdateSelectedReferenceText()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            SelectedReferenceText = "No object selected";
            return;
        }

        SelectedReferenceText = $"{rnaRef.Name} | {rnaRef.Path}";
    }
}
