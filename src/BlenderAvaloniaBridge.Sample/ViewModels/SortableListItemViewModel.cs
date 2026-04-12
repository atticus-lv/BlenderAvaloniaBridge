namespace BlenderAvaloniaBridge.Sample.ViewModels;

public sealed class SortableListItemViewModel
{
    public SortableListItemViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }
}
