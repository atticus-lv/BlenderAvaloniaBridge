using System.Collections.ObjectModel;
using System.Text.Json;
using BlenderAvaloniaBridge.Sample.Models;
using BlenderAvaloniaBridge.Sample.Helpers;
using BlenderAvaloniaBridge.Sample.ViewModels;
using BlenderAvaloniaBridge.Sample.ViewModels.Pages;

namespace BlenderAvaloniaBridge.Sample.Design;

public static class SampleDesignData
{
    public static MainViewModel MainView => new DesignMainViewModel();

    public static ButtonsDemoPageViewModel ButtonsPage => new DesignButtonsDemoPageViewModel();

    public static SortableListDemoPageViewModel SortableListPage => new DesignSortableListDemoPageViewModel();

    public static BlenderObjectsPageViewModel BlenderObjectsPage => new DesignBlenderObjectsPageViewModel();

    public static LiveTransformPageViewModel LiveTransformPage => new DesignLiveTransformPageViewModel();

    public static MaterialsPageViewModel MaterialsPage => new DesignMaterialsPageViewModel();

    public static CollectionsPageViewModel CollectionsPage => new DesignCollectionsPageViewModel();

    public static OperatorsPageViewModel OperatorsPage => new DesignOperatorsPageViewModel();

    private static BlenderObjectListItem CreateObjectItem(
        string name,
        string objectType,
        bool isActive,
        long sessionUid,
        string path)
    {
        return new BlenderObjectListItem(
            CreateRnaRef(name, path, "Object", "OBJECT", sessionUid),
            name,
            objectType,
            isActive);
    }

    private static RnaItemRef CreateRnaRef(
        string name,
        string path,
        string rnaType,
        string idType,
        long sessionUid)
    {
        return new RnaItemRef
        {
            Name = name,
            Label = name,
            Path = path,
            RnaType = rnaType,
            IdType = idType,
            SessionUid = sessionUid,
        };
    }

    private static byte[] FloatBytes(params float[] values)
    {
        var bytes = new byte[values.Length * sizeof(float)];
        for (var index = 0; index < values.Length; index++)
        {
            BitConverter.GetBytes(values[index]).CopyTo(bytes, index * sizeof(float));
        }

        return bytes;
    }

    private static BlenderArrayReadResult CreatePreviewPixels(params byte[] rgbaBytes)
    {
        var floatValues = new float[rgbaBytes.Length];
        for (var index = 0; index < rgbaBytes.Length; index++)
        {
            floatValues[index] = rgbaBytes[index] / 255f;
        }

        return new BlenderArrayReadResult
        {
            ElementType = "float32",
            Count = floatValues.Length,
            Shape = [2, 2, 4],
            RawBytes = FloatBytes(floatValues),
        };
    }

    private sealed class DesignMainViewModel : MainViewModel
    {
        public DesignMainViewModel()
        {
            IsSidebarOpen = true;
            CurrentPage = new DesignBlenderObjectsPageViewModel();
            CurrentPageTitle = "Blender Objects";
        }
    }

    private sealed class DesignButtonsDemoPageViewModel : ButtonsDemoPageViewModel
    {
        public DesignButtonsDemoPageViewModel()
        {
            IsPrimaryOptionEnabled = true;
            IsSecondaryOptionEnabled = true;
            IsAutomationEnabled = true;
            SelectedQuality = "Final";
            NotesText = "Preview mode now includes slider, text input, and common control samples.";
            Intensity = 78;
            ActivityText = "Primary action fired at 14:36:12.";
        }
    }

    private sealed class DesignSortableListDemoPageViewModel : SortableListDemoPageViewModel
    {
        public DesignSortableListDemoPageViewModel()
        {
            StatusText = "Drag the right-side queue to prioritize the next four tasks.";
        }
    }

    private sealed class DesignBlenderObjectsPageViewModel : BlenderObjectsPageViewModel
    {
        public DesignBlenderObjectsPageViewModel()
        {
            var active = CreateObjectItem("Camera", "CAMERA", true, 201, "bpy.data.objects[\"Camera\"]");
            Objects.Add(active);
            Objects.Add(CreateObjectItem("Cube", "MESH", false, 202, "bpy.data.objects[\"Cube\"]"));
            Objects.Add(CreateObjectItem("KeyLight", "LIGHT", false, 203, "bpy.data.objects[\"KeyLight\"]"));

            SelectedObject = active;
            SelectedReferenceText = "Object | Camera | session_uid=201 | bpy.data.objects[\"Camera\"]";
            ObjectName = "Camera";
            LocationX = "0";
            LocationY = "-6.2";
            LocationZ = "3.1";
            StatusText = "Loaded properties for Camera.";
            BridgeStatusText = "Bridge connected (design preview)";
        }
    }

    private sealed class DesignLiveTransformPageViewModel : LiveTransformPageViewModel
    {
        public DesignLiveTransformPageViewModel()
        {
            CurrentObject = CreateRnaRef("Cube", "bpy.data.objects[\"Cube\"]", "Object", "OBJECT", 301);
            SelectedReferenceText = "Cube | bpy.data.objects[\"Cube\"]";
            IsLiveWatchEnabled = true;
            LocationX = "1.25";
            LocationY = "-0.4";
            LocationZ = "0";
            RotationX = "0";
            RotationY = "0.785";
            RotationZ = "0";
            ScaleX = "1";
            ScaleY = "1";
            ScaleZ = "1.2";
            StatusText = "Transform synced for Cube.";
            BridgeStatusText = "Live transform dirty event received";
        }
    }

    private sealed class DesignMaterialsPageViewModel : MaterialsPageViewModel
    {
        public DesignMaterialsPageViewModel()
        {
            var preview = new MaterialLibraryItem(
                CreateRnaRef("PreviewMat", "bpy.data.materials[\"PreviewMat\"]", "Material", "MATERIAL", 401),
                "PreviewMat",
                MaterialPreviewImageFactory.Create(
                    [2, 2],
                    CreatePreviewPixels(240, 170, 72, 255, 215, 96, 80, 255, 92, 139, 220, 255, 36, 52, 86, 255)));
            var glass = new MaterialLibraryItem(
                CreateRnaRef("GlassAccent", "bpy.data.materials[\"GlassAccent\"]", "Material", "MATERIAL", 402),
                "GlassAccent",
                MaterialPreviewImageFactory.Create(
                    [2, 2],
                    CreatePreviewPixels(124, 208, 255, 255, 210, 246, 255, 255, 46, 89, 134, 255, 118, 166, 228, 255)));
            var ground = new MaterialLibraryItem(
                CreateRnaRef("Ground", "bpy.data.materials[\"Ground\"]", "Material", "MATERIAL", 403),
                "Ground",
                null);

            Materials.Add(preview);
            Materials.Add(glass);
            Materials.Add(ground);
            SelectedMaterial = preview;
            MaterialName = "PreviewMat";
            SelectedMaterialPath = "bpy.data.materials[\"PreviewMat\"]";
            SelectedPreviewImage = MaterialPreviewImageFactory.Create(
                [2, 2],
                CreatePreviewPixels(240, 170, 72, 255, 215, 96, 80, 255, 92, 139, 220, 255, 36, 52, 86, 255));
            NewMaterialName = "Accent";
            StatusText = "Loaded material PreviewMat.";
            BridgeStatusText = "Bridge connected (design preview)";
        }
    }

    private sealed class DesignCollectionsPageViewModel : CollectionsPageViewModel
    {
        public DesignCollectionsPageViewModel()
        {
            var environment = CreateRnaRef("Environment", "bpy.context.scene.collection", "Collection", "COLLECTION", 501);
            var environmentNode = CollectionTreeItem.CreateCollection(environment);
            environmentNode.IsExpanded = true;
            environmentNode.Children.Add(CollectionTreeItem.CreateCollection(CreateRnaRef("Lighting", "bpy.data.collections[\"Lighting\"]", "Collection", "COLLECTION", 504)));
            environmentNode.Children.Add(CollectionTreeItem.CreateCollection(CreateRnaRef("Background", "bpy.data.collections[\"Background\"]", "Collection", "COLLECTION", 505)));
            environmentNode.Children.Add(CollectionTreeItem.CreateObject(CreateRnaRef("GroundPlane", "bpy.data.objects[\"GroundPlane\"]", "Object", "OBJECT", 506)));
            environmentNode.Children.Add(CollectionTreeItem.CreateObject(CreateRnaRef("SkyRig", "bpy.data.objects[\"SkyRig\"]", "Object", "OBJECT", 507)));

            var charactersNode = CollectionTreeItem.CreateCollection(CreateRnaRef("Characters", "bpy.data.collections[\"Characters\"]", "Collection", "COLLECTION", 502));
            var heroNode = CollectionTreeItem.CreateCollection(CreateRnaRef("Hero", "bpy.data.collections[\"Hero\"]", "Collection", "COLLECTION", 508));
            heroNode.Children.Add(CollectionTreeItem.CreateCollection(CreateRnaRef("HeroProps", "bpy.data.collections[\"HeroProps\"]", "Collection", "COLLECTION", 509)));
            heroNode.Children.Add(CollectionTreeItem.CreateObject(CreateRnaRef("HeroBody", "bpy.data.objects[\"HeroBody\"]", "Object", "OBJECT", 510)));
            charactersNode.Children.Add(heroNode);

            CollectionTreeRoots.Add(environmentNode);
            CollectionTreeRoots.Add(charactersNode);
            CollectionTreeRoots.Add(CollectionTreeItem.CreateCollection(CreateRnaRef("Props", "bpy.data.collections[\"Props\"]", "Collection", "COLLECTION", 503)));

            CollectionObjects.Add(CreateRnaRef("GroundPlane", "bpy.data.objects[\"GroundPlane\"]", "Object", "OBJECT", 506));
            CollectionObjects.Add(CreateRnaRef("SkyRig", "bpy.data.objects[\"SkyRig\"]", "Object", "OBJECT", 507));

            SelectedCollectionNode = environmentNode;
            StatusText = "Loaded collection Environment.";
            BridgeStatusText = "Bridge connected (design preview)";
        }
    }

    private sealed class DesignOperatorsPageViewModel : OperatorsPageViewModel
    {
        public DesignOperatorsPageViewModel()
        {
            CurrentObject = CreateRnaRef("Cube", "bpy.data.objects[\"Cube\"]", "Object", "OBJECT", 601);
            SelectedReferenceText = "Cube | bpy.data.objects[\"Cube\"]";
            CanAddCubeOperator = true;
            CanDuplicateOperator = true;
            CanViewSelectedOperator = true;
            CanDeleteOperator = true;
            AddCubePollText = "Ready";
            DuplicatePollText = "Ready";
            ViewSelectedPollText = "Ready";
            DeletePollText = "Blocked: linked library object";
            StatusText = "object.duplicate_move: FINISHED";
            BridgeStatusText = "Bridge connected (design preview)";
        }
    }
}
