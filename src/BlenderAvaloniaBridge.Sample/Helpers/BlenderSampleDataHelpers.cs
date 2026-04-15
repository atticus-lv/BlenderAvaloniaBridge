using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.Models;

namespace BlenderAvaloniaBridge.Sample.Helpers;

internal static class BlenderSampleDataHelpers
{
    internal const string SceneObjectsPath = "bpy.context.scene.objects";
    internal const string ActiveObjectPath = "bpy.context.active_object";
    internal const string MaterialsPath = "bpy.data.materials";
    internal const string SceneCollectionPath = "bpy.context.scene.collection";

    internal static async Task<IReadOnlyList<BlenderObjectListItem>> LoadSceneObjectItemsAsync(
        IBlenderDataApi blenderDataApi,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blenderDataApi);

        var items = await blenderDataApi.ListAsync(SceneObjectsPath, cancellationToken);
        RnaItemRef? activeObject = null;

        try
        {
            activeObject = await blenderDataApi.GetAsync<RnaItemRef>(ActiveObjectPath, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            activeObject = null;
        }

        var loadTasks = items.Select(async item =>
        {
            var objectType = await blenderDataApi.GetAsync<string>($"{item.Path}.type", cancellationToken);
            var isActiveObject = activeObject is not null && ReferenceMatches(item, activeObject);
            return new BlenderObjectListItem(item, item.Label, objectType, isActiveObject);
        });

        return await Task.WhenAll(loadTasks);
    }

    internal static bool ReferenceMatches(RnaItemRef left, RnaItemRef right)
    {
        if (left.SessionUid.HasValue && right.SessionUid.HasValue && left.SessionUid.Value != 0 && right.SessionUid.Value != 0)
        {
            return left.SessionUid.Value == right.SessionUid.Value;
        }

        return string.Equals(left.Path, right.Path, StringComparison.Ordinal)
               || (string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                   && string.Equals(left.RnaType, right.RnaType, StringComparison.Ordinal));
    }

    internal static BlenderContextOverride BuildSelectionContextOverride(string path)
    {
        return new BlenderContextOverride
        {
            ActiveObject = path,
            SelectedObjects = new[] { path },
        };
    }
}
