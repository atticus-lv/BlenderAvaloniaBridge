using System.Collections.ObjectModel;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.Helpers;
using BlenderAvaloniaBridge.Sample.Models;
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

    public ObservableCollection<CollectionTreeItem> CollectionTreeRoots { get; } = new();

    public ObservableCollection<RnaItemRef> CollectionObjects { get; } = new();

    [ObservableProperty]
    private CollectionTreeItem? _selectedCollectionNode;

    public bool HasSelectedCollection => SelectedCollectionNode?.IsCollection == true;

    protected override Task OnActivatedAsync()
    {
        if (BlenderApi is null)
        {
            SetDisconnectedStatus();
            return Task.CompletedTask;
        }

        return RefreshCollectionsAsync();
    }

    protected override void OnBlenderApiChanged()
    {
        RefreshCollectionsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCollectionNodeChanged(CollectionTreeItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedCollection));

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

    private bool CanUseBridge() => BlenderApi is not null;

    private async Task RefreshCollectionsCoreAsync()
    {
        var blender = RequireBlenderApi();
        var previousSelection = SelectedCollectionNode?.Item;
        var sceneCollection = await blender.Rna.GetAsync<RnaItemRef>(BlenderSampleDataHelpers.SceneCollectionPath);
        var roots = await BuildCollectionTreeAsync(blender, sceneCollection);
        foreach (var root in roots)
        {
            root.IsExpanded = true;
        }

        CollectionTreeRoots.Clear();
        foreach (var item in roots)
        {
            CollectionTreeRoots.Add(item);
        }

        var nextSelection = previousSelection is null
            ? CollectionTreeRoots.FirstOrDefault()
            : FindTreeItem(CollectionTreeRoots, previousSelection)
              ?? CollectionTreeRoots.FirstOrDefault();

        _isApplyingRemoteState = true;
        SelectedCollectionNode = nextSelection;
        _isApplyingRemoteState = false;

        if (SelectedCollectionNode is not null)
        {
            await LoadSelectedTreeNodeAsync(SelectedCollectionNode);
        }
        else
        {
            CollectionObjects.Clear();
            SetConnectedIdleStatus("No collections found.");
        }
    }

    private async Task SelectionChangedCoreAsync()
    {
        if (SelectedCollectionNode is null)
        {
            CollectionObjects.Clear();
            return;
        }

        await LoadSelectedTreeNodeAsync(SelectedCollectionNode);
    }

    private async Task LoadSelectedTreeNodeAsync(CollectionTreeItem node)
    {
        if (node.IsObject)
        {
            SetConnectedIdleStatus($"Selected object {node.Name}.");
            return;
        }

        await LoadSelectedCollectionAsync(node.Item);
    }

    private async Task LoadSelectedCollectionAsync(RnaItemRef collection)
    {
        var blender = RequireBlenderApi();
        var objects = await blender.Rna.ListAsync($"{collection.Path}.objects");

        CollectionObjects.Clear();
        foreach (var item in objects.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            CollectionObjects.Add(item);
        }

        SetConnectedIdleStatus($"Loaded collection {collection.Name}.");
    }

    private static CollectionTreeItem? FindTreeItem(IEnumerable<CollectionTreeItem> roots, RnaItemRef target)
    {
        foreach (var item in roots)
        {
            if (BlenderSampleDataHelpers.ReferenceMatches(item.Item, target))
            {
                return item;
            }

            var childMatch = FindTreeItem(item.Children, target);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static async Task<IReadOnlyList<CollectionTreeItem>> BuildCollectionTreeAsync(
        BlenderApi blender,
        RnaItemRef sceneCollection)
    {
        return new[] { await BuildTreeItemAsync(blender, sceneCollection) };
    }

    private static async Task<CollectionTreeItem> BuildTreeItemAsync(
        BlenderApi blender,
        RnaItemRef collection)
    {
        var item = CollectionTreeItem.CreateCollection(collection);

        var children = await blender.Rna.ListAsync($"{collection.Path}.children");
        foreach (var child in children.OrderBy(static child => child.Name, StringComparer.OrdinalIgnoreCase))
        {
            item.Children.Add(await BuildTreeItemAsync(blender, child));
        }

        var objects = await blender.Rna.ListAsync($"{collection.Path}.objects");
        foreach (var obj in objects.OrderBy(static obj => obj.Name, StringComparer.OrdinalIgnoreCase))
        {
            item.Children.Add(CollectionTreeItem.CreateObject(obj));
        }

        return item;
    }
}
