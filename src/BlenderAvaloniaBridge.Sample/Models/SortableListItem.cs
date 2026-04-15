namespace BlenderAvaloniaBridge.Sample.Models;

public sealed class SortableListItem
{
    public SortableListItem(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }
}
