using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class SortableListDemoPageViewModel : ObservableObject
{
    public ObservableCollection<SortableListItemViewModel> Items { get; } =
    [
        new("Bridge Launch", "Open the desktop shell and verify the renderer is connected."),
        new("Camera Review", "Inspect the preview framing before pushing the next action."),
        new("Material Pass", "Check materials and update the bridge status notes."),
        new("Export Queue", "Prepare the next task batch for downstream processing."),
        new("Lighting Sweep", "Walk the key lights through a quick balance check for the active scene."),
        new("Rig Notes", "Review control naming and flag anything that still feels inconsistent."),
        new("Physics Cache", "Queue a compact cache refresh before the next simulation preview."),
        new("Texture Audit", "Scan the current asset stack and note any maps that still need cleanup."),
        new("Shot Timing", "Reorder the scene beats so the preview sequence reads more clearly."),
        new("Overlay Check", "Verify the status overlays remain readable against darker renders."),
        new("Library Sync", "Pull the latest linked asset changes into the working scene snapshot."),
        new("Final Review", "Group the last pass tasks in the order the artist will likely use them."),
    ];

    [ObservableProperty]
    private string _statusText = "Drag the handle on the left side of an item to reorder the list.";

    public void MoveItem(SortableListItemViewModel? source, SortableListItemViewModel? target)
    {
        if (source is null || target is null || ReferenceEquals(source, target))
        {
            return;
        }

        var sourceIndex = Items.IndexOf(source);
        var targetIndex = Items.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        Items.Move(sourceIndex, targetIndex);
        StatusText = $"Moved \"{source.Title}\" to position {targetIndex + 1}.";
    }

    public void MoveItemToIndex(SortableListItemViewModel? source, int targetIndex)
    {
        if (source is null)
        {
            return;
        }

        var sourceIndex = Items.IndexOf(source);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= Items.Count || sourceIndex == targetIndex)
        {
            return;
        }

        Items.Move(sourceIndex, targetIndex);
        StatusText = $"Moved \"{source.Title}\" to position {targetIndex + 1}.";
    }
}
