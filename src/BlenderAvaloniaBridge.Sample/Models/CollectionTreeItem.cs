using System.Collections.ObjectModel;
using Avalonia.Media;
using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace BlenderAvaloniaBridge.Sample.Models;

public partial class CollectionTreeItem : ObservableObject
{
    public CollectionTreeItem(RnaItemRef item, bool isCollection)
    {
        Item = item;
        IsCollection = isCollection;
        Icon = StreamGeometry.Parse(
            MaterialIconDataProvider.GetData(isCollection ? MaterialIconKind.FolderOutline : MaterialIconKind.CubeOutline));
    }

    public RnaItemRef Item { get; }

    public bool IsCollection { get; }

    public bool IsObject => !IsCollection;

    public string Name => Item.Name;

    public Geometry Icon { get; }

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<CollectionTreeItem> Children { get; } = new();

    public static CollectionTreeItem CreateCollection(RnaItemRef collection)
    {
        return new CollectionTreeItem(collection, isCollection: true);
    }

    public static CollectionTreeItem CreateObject(RnaItemRef obj)
    {
        return new CollectionTreeItem(obj, isCollection: false);
    }
}
