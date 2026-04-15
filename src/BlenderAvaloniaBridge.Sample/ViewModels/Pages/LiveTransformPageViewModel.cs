using System.Globalization;
using System.Threading;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public partial class LiveTransformPageViewModel : BlenderBridgePageViewModelBase
{
    private bool _isChangingLiveWatch;
    private IAsyncDisposable? _watchSubscription;
    private string? _currentWatchPath;
    private readonly SemaphoreSlim _transformRefreshGate = new(1, 1);
    private int _pendingTransformRefresh;

    public LiveTransformPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect live transforms.",
            "Refresh the current Blender context object, then enable live watch.")
    {
    }

    [ObservableProperty]
    private RnaItemRef? _currentObject;

    [ObservableProperty]
    private string _selectedReferenceText = "No active context object";

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

    public bool CanEnableLiveWatch => BlenderApi is not null && CurrentObject is not null;

    protected override async Task OnActivatedAsync()
    {
        if (BlenderApi is null)
        {
            SetDisconnectedStatus();
            return;
        }

        await RefreshObjectsAsync();

        if (IsLiveWatchEnabled && CurrentObject is not null)
        {
            await StartWatchAsync(CurrentObject);
        }
    }

    protected override async Task OnDeactivatedAsync()
    {
        await StopWatchAsync();
    }

    protected override void OnBlenderApiChanged()
    {
        RefreshObjectsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEnableLiveWatch));
    }

    partial void OnCurrentObjectChanged(RnaItemRef? value)
    {
        UpdateSelectedReferenceText();
        OnPropertyChanged(nameof(CanEnableLiveWatch));
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

    private bool CanUseBridge() => BlenderApi is not null;

    private async Task RefreshObjectsCoreAsync()
    {
        var blender = RequireBlenderApi();
        RnaItemRef? currentObject;

        try
        {
            currentObject = await blender.Rna.GetAsync<RnaItemRef>(BlenderSampleDataHelpers.CurrentObjectPath);
        }
        catch (InvalidOperationException)
        {
            currentObject = null;
        }

        CurrentObject = currentObject;

        if (CurrentObject is not null)
        {
            await LoadTransformSnapshotAsync(CurrentObject);
            if (IsLiveWatchEnabled)
            {
                await StartWatchAsync(CurrentObject);
            }
        }
        else
        {
            await StopWatchAsync();
            ClearTransformValues();
            SetConnectedIdleStatus("No active context object.");
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

        if (CurrentObject is not { } rnaRef)
        {
            _isChangingLiveWatch = true;
            IsLiveWatchEnabled = false;
            _isChangingLiveWatch = false;
            StatusText = "Refresh until bpy.context.object resolves to an object.";
            return;
        }

        await StartWatchAsync(rnaRef);
    }

    private async Task StartWatchAsync(RnaItemRef rnaRef)
    {
        await StopWatchAsync();

        if (!IsActive || BlenderApi is null || !IsLiveWatchEnabled)
        {
            return;
        }

        var watchId = $"live-transform-{rnaRef.SessionUid?.ToString() ?? rnaRef.Name}";
        Volatile.Write(ref _currentWatchPath, rnaRef.Path);
        _watchSubscription = await BlenderApi.Observe.WatchAsync(
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
        var blender = RequireBlenderApi();
        var location = await blender.Rna.GetAsync<double[]>($"{rnaRef.Path}.location");
        var rotation = await blender.Rna.GetAsync<double[]>($"{rnaRef.Path}.rotation_euler");
        var scale = await blender.Rna.GetAsync<double[]>($"{rnaRef.Path}.scale");
        ApplyVector(location, "0", out var locationX, out var locationY, out var locationZ);
        ApplyVector(rotation, "0", out var rotationX, out var rotationY, out var rotationZ);
        ApplyVector(scale, "1", out var scaleX, out var scaleY, out var scaleZ);

        await RunOnUiThreadAsync(() =>
        {
            LocationX = locationX;
            LocationY = locationY;
            LocationZ = locationZ;
            RotationX = rotationX;
            RotationY = rotationY;
            RotationZ = rotationZ;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;

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
        LocationX = "0";
        LocationY = "0";
        LocationZ = "0";
        RotationX = "0";
        RotationY = "0";
        RotationZ = "0";
        ScaleX = "1";
        ScaleY = "1";
        ScaleZ = "1";
        SelectedReferenceText = "No active context object";
    }

    private void UpdateSelectedReferenceText()
    {
        if (CurrentObject is not { } rnaRef)
        {
            SelectedReferenceText = "No active context object";
            return;
        }

        SelectedReferenceText = $"{rnaRef.Name} | {rnaRef.Path}";
    }
}
