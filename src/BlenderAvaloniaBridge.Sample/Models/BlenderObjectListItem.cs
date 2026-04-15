using CommunityToolkit.Mvvm.ComponentModel;
using BlenderAvaloniaBridge;

namespace BlenderAvaloniaBridge.Sample.Models;

public partial class BlenderObjectListItem : ObservableObject
{
    public BlenderObjectListItem(RnaItemRef rnaRef, string label, string objectType, bool isActive)
    {
        RnaRef = rnaRef;
        _label = label;
        ObjectType = objectType;
        IsActive = isActive;
    }

    public RnaItemRef RnaRef { get; private set; }

    [ObservableProperty]
    private string _label;

    public string ObjectType { get; }

    public bool IsActive { get; }

    public void UpdateReference(RnaItemRef rnaRef)
    {
        RnaRef = rnaRef;
        OnPropertyChanged(nameof(RnaRef));
    }
}
