using System.Text.Json;
using BlenderAvaloniaBridge;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

internal static class BlenderSampleViewModelHelpers
{
    internal const string SceneObjectsPath = "bpy.context.scene.objects";
    internal const string MaterialsPath = "bpy.data.materials";
    internal const string CollectionsPath = "bpy.data.collections";

    internal static IReadOnlyList<BlenderObjectListItem> CreateObjectItems(IReadOnlyList<RnaItemRef> items)
    {
        return items
            .Select(
                item => new BlenderObjectListItem(
                    item,
                    item.Label,
                    GetObjectType(item.Metadata),
                    IsActiveObject(item.Metadata)))
            .ToList();
    }

    internal static string GetObjectType(JsonElement? metadata)
    {
        return metadata.HasValue && metadata.Value.TryGetProperty("objectType", out var objectTypeElement)
            ? objectTypeElement.GetString() ?? "?"
            : "?";
    }

    internal static bool IsActiveObject(JsonElement? metadata)
    {
        return metadata.HasValue
               && metadata.Value.TryGetProperty("isActive", out var isActiveElement)
               && isActiveElement.ValueKind is JsonValueKind.True or JsonValueKind.False
               && isActiveElement.GetBoolean();
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
