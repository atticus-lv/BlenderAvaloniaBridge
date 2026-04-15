using BlenderAvaloniaBridge;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public abstract partial class BlenderBridgePageViewModelBase : ObservableObject, IBlenderSamplePageViewModel
{
    private readonly string _disconnectedStatusText;
    private readonly string _connectedStatusText;
    private bool _isActive;

    protected BlenderBridgePageViewModelBase(string disconnectedStatusText, string connectedStatusText)
    {
        _disconnectedStatusText = disconnectedStatusText;
        _connectedStatusText = connectedStatusText;
        _statusText = disconnectedStatusText;
    }

    protected IBlenderDataApi? BlenderDataApi { get; private set; }

    protected bool IsActive => _isActive;

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private string _bridgeStatusText = "Bridge disconnected";

    public void AttachBlenderDataApi(IBlenderDataApi? blenderDataApi)
    {
        _ = RunOnUiThreadAsync(async () =>
        {
            BlenderDataApi = blenderDataApi;
            BridgeStatusText = blenderDataApi is null ? "Bridge disconnected" : "Bridge connected";
            StatusText = blenderDataApi is null ? _disconnectedStatusText : _connectedStatusText;
            OnBlenderDataApiChanged();

            if (_isActive)
            {
                await RunPageOperationAsync(async () =>
                {
                    await OnDeactivatedAsync();
                    if (BlenderDataApi is not null)
                    {
                        await OnActivatedAsync();
                    }
                });
            }
        });
    }

    public void SetBridgeStatus(string status)
    {
        _ = RunOnUiThreadAsync(() =>
        {
            BridgeStatusText = status;
            return Task.CompletedTask;
        });
    }

    public Task ActivateAsync()
    {
        _isActive = true;
        return RunPageOperationAsync(OnActivatedAsync);
    }

    public Task DeactivateAsync()
    {
        _isActive = false;
        return RunPageOperationAsync(OnDeactivatedAsync);
    }

    protected virtual void OnBlenderDataApiChanged()
    {
    }

    protected virtual Task OnActivatedAsync()
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnDeactivatedAsync()
    {
        return Task.CompletedTask;
    }

    protected void SetDisconnectedStatus()
    {
        StatusText = _disconnectedStatusText;
    }

    protected void SetConnectedIdleStatus(string statusText)
    {
        if (BlenderDataApi is not null)
        {
            StatusText = statusText;
        }
    }

    protected IBlenderDataApi RequireBlenderDataApi()
    {
        return BlenderDataApi ?? throw new InvalidOperationException("Bridge is not attached.");
    }

    protected Task RunOnUiThreadAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Dispatcher.UIThread.CheckAccess())
        {
            return operation();
        }

        return Dispatcher.UIThread.InvokeAsync(operation);
    }

    protected async Task RunPageOperationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            await RunOnUiThreadAsync(() =>
            {
                StatusText = "Business request canceled.";
                BridgeStatusText = "Bridge request canceled";
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() =>
            {
                StatusText = ex.Message;
                BridgeStatusText = "Bridge request failed";
                return Task.CompletedTask;
            });
        }
    }
}
