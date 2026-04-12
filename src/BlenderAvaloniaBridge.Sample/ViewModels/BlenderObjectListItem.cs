using BlenderAvaloniaBridge.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class BlenderObjectListItem : ObservableObject
{
    public BlenderObjectListItem(BlenderRnaRef rnaRef, string label, string objectType, bool isActive)
    {
        RnaRef = rnaRef;
        _label = label;
        ObjectType = objectType;
        IsActive = isActive;
    }

    public BlenderRnaRef RnaRef { get; private set; }

    [ObservableProperty]
    private string _label;

    public string ObjectType { get; }

    public bool IsActive { get; }

    public void UpdateReference(BlenderRnaRef rnaRef)
    {
        RnaRef = rnaRef;
        OnPropertyChanged(nameof(RnaRef));
    }
}
