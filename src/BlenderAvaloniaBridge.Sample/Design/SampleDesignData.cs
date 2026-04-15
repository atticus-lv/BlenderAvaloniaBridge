using System.Collections.ObjectModel;
using System.Text.Json;
using BlenderAvaloniaBridge.Sample.Models;
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
            var active = CreateObjectItem("Cube", "MESH", true, 301, "bpy.data.objects[\"Cube\"]");
            Objects.Add(active);
            Objects.Add(CreateObjectItem("Camera", "CAMERA", false, 302, "bpy.data.objects[\"Camera\"]"));
            Objects.Add(CreateObjectItem("KeyLight", "LIGHT", false, 303, "bpy.data.objects[\"KeyLight\"]"));

            SelectedObject = active;
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
            var preview = CreateRnaRef("PreviewMat", "bpy.data.materials[\"PreviewMat\"]", "Material", "MATERIAL", 401);
            Materials.Add(preview);
            Materials.Add(CreateRnaRef("GlassAccent", "bpy.data.materials[\"GlassAccent\"]", "Material", "MATERIAL", 402));
            Materials.Add(CreateRnaRef("Ground", "bpy.data.materials[\"Ground\"]", "Material", "MATERIAL", 403));

            SelectedMaterial = preview;
            MaterialName = "PreviewMat";
            NewMaterialName = "Accent";
            UseNodes = true;
            StatusText = "Loaded material PreviewMat.";
            BridgeStatusText = "Bridge connected (design preview)";
        }
    }

    private sealed class DesignCollectionsPageViewModel : CollectionsPageViewModel
    {
        public DesignCollectionsPageViewModel()
        {
            var environment = CreateRnaRef("Environment", "bpy.data.collections[\"Environment\"]", "Collection", "COLLECTION", 501);
            Collections.Add(environment);
            Collections.Add(CreateRnaRef("Characters", "bpy.data.collections[\"Characters\"]", "Collection", "COLLECTION", 502));
            Collections.Add(CreateRnaRef("Props", "bpy.data.collections[\"Props\"]", "Collection", "COLLECTION", 503));

            ChildCollections.Add(CreateRnaRef("Lighting", "bpy.data.collections[\"Lighting\"]", "Collection", "COLLECTION", 504));
            ChildCollections.Add(CreateRnaRef("Background", "bpy.data.collections[\"Background\"]", "Collection", "COLLECTION", 505));

            CollectionObjects.Add(CreateRnaRef("GroundPlane", "bpy.data.objects[\"GroundPlane\"]", "Object", "OBJECT", 506));
            CollectionObjects.Add(CreateRnaRef("SkyRig", "bpy.data.objects[\"SkyRig\"]", "Object", "OBJECT", 507));

            SelectedCollection = environment;
            StatusText = "Loaded collection Environment.";
            BridgeStatusText = "Bridge connected (design preview)";
        }
    }

    private sealed class DesignOperatorsPageViewModel : OperatorsPageViewModel
    {
        public DesignOperatorsPageViewModel()
        {
            var active = CreateObjectItem("Cube", "MESH", true, 601, "bpy.data.objects[\"Cube\"]");
            Objects.Add(active);
            Objects.Add(CreateObjectItem("Camera", "CAMERA", false, 602, "bpy.data.objects[\"Camera\"]"));

            SelectedObject = active;
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
