using System.Collections.ObjectModel;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public partial class CollectionsPageViewModel : BlenderBridgePageViewModelBase
{
    private bool _isApplyingRemoteState;

    public CollectionsPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect collections.",
            "Waiting for Blender collection data.")
    {
    }

    public ObservableCollection<RnaItemRef> Collections { get; } = new();

    public ObservableCollection<RnaItemRef> ChildCollections { get; } = new();

    public ObservableCollection<RnaItemRef> CollectionObjects { get; } = new();

    [ObservableProperty]
    private RnaItemRef? _selectedCollection;

    protected override Task OnActivatedAsync()
    {
        if (BlenderDataApi is null)
        {
            SetDisconnectedStatus();
            return Task.CompletedTask;
        }

        return RefreshCollectionsAsync();
    }

    protected override void OnBlenderDataApiChanged()
    {
        RefreshCollectionsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCollectionChanged(RnaItemRef? value)
    {
        if (_isApplyingRemoteState)
        {
            return;
        }

        _ = SelectionChangedAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task RefreshCollectionsAsync()
    {
        return RunPageOperationAsync(RefreshCollectionsCoreAsync);
    }

    public Task SelectionChangedAsync()
    {
        return RunPageOperationAsync(SelectionChangedCoreAsync);
    }

    private bool CanUseBridge() => BlenderDataApi is not null;

    private async Task RefreshCollectionsCoreAsync()
    {
        var blender = RequireBlenderDataApi();
        var previousSelection = SelectedCollection;
        var items = await blender.ListAsync(BlenderSampleDataHelpers.CollectionsPath);

        Collections.Clear();
        foreach (var item in items)
        {
            Collections.Add(item);
        }

        var nextSelection = previousSelection is null
            ? Collections.FirstOrDefault()
            : Collections.FirstOrDefault(item => BlenderSampleDataHelpers.ReferenceMatches(item, previousSelection))
              ?? Collections.FirstOrDefault(item => string.Equals(item.Name, previousSelection.Name, StringComparison.Ordinal))
              ?? Collections.FirstOrDefault();

        _isApplyingRemoteState = true;
        SelectedCollection = nextSelection;
        _isApplyingRemoteState = false;

        if (SelectedCollection is not null)
        {
            await LoadSelectedCollectionAsync(SelectedCollection);
        }
        else
        {
            ChildCollections.Clear();
            CollectionObjects.Clear();
            SetConnectedIdleStatus("No collections found.");
        }
    }

    private async Task SelectionChangedCoreAsync()
    {
        if (SelectedCollection is null)
        {
            ChildCollections.Clear();
            CollectionObjects.Clear();
            return;
        }

        await LoadSelectedCollectionAsync(SelectedCollection);
    }

    private async Task LoadSelectedCollectionAsync(RnaItemRef collection)
    {
        var blender = RequireBlenderDataApi();
        var children = await blender.ListAsync($"{collection.Path}.children");
        var objects = await blender.ListAsync($"{collection.Path}.objects");

        ChildCollections.Clear();
        foreach (var child in children)
        {
            ChildCollections.Add(child);
        }

        CollectionObjects.Clear();
        foreach (var item in objects)
        {
            CollectionObjects.Add(item);
        }

        SetConnectedIdleStatus($"Loaded collection {collection.Name}.");
    }
}
