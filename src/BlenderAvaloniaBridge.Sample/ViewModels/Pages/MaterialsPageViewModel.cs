using System.Collections.ObjectModel;
using Avalonia.Media;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.Helpers;
using BlenderAvaloniaBridge.Sample.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public partial class MaterialsPageViewModel : BlenderBridgePageViewModelBase
{
    private bool _isApplyingRemoteState;
    private IAsyncDisposable? _watchSubscription;

    public MaterialsPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect materials.",
            "Waiting for Blender material data.")
    {
    }

    public ObservableCollection<MaterialLibraryItem> Materials { get; } = new();

    [ObservableProperty]
    private MaterialLibraryItem? _selectedMaterial;

    [ObservableProperty]
    private string _materialName = string.Empty;

    [ObservableProperty]
    private IImage? _selectedPreviewImage;

    [ObservableProperty]
    private string _selectedMaterialPath = string.Empty;

    [ObservableProperty]
    private string _newMaterialName = "Material";

    public bool HasSelectedMaterial => SelectedMaterial is not null;

    public bool HasSelectedPreview => SelectedPreviewImage is not null;

    public bool ShowSelectedPreviewPlaceholder => SelectedPreviewImage is null;

    protected override async Task OnActivatedAsync()
    {
        if (BlenderApi is null)
        {
            SetDisconnectedStatus();
            return;
        }

        await RefreshMaterialsAsync();
        await EnsureWatchAsync();
    }

    protected override async Task OnDeactivatedAsync()
    {
        await StopWatchAsync();
    }

    protected override void OnBlenderApiChanged()
    {
        RefreshMaterialsCommand.NotifyCanExecuteChanged();
        CreateMaterialCommand.NotifyCanExecuteChanged();
        CommitMaterialNameCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedMaterial));
    }

    partial void OnSelectedMaterialChanged(MaterialLibraryItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedMaterial));
        CommitMaterialNameCommand.NotifyCanExecuteChanged();

        if (!_isApplyingRemoteState)
        {
            _ = SelectionChangedAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task RefreshMaterialsAsync()
    {
        return RunPageOperationAsync(RefreshMaterialsCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task CreateMaterialAsync()
    {
        return RunPageOperationAsync(CreateMaterialCoreAsync);
    }

    public Task SelectionChangedAsync()
    {
        return RunPageOperationAsync(SelectionChangedCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanCommitMaterialName))]
    public Task CommitMaterialNameAsync()
    {
        return RunPageOperationAsync(CommitMaterialNameCoreAsync);
    }

    private bool CanUseBridge() => BlenderApi is not null;

    private bool CanCommitMaterialName() => SelectedMaterial is not null && BlenderApi is not null;

    private async Task RefreshMaterialsCoreAsync()
    {
        var blender = RequireBlenderApi();
        var previousSelection = SelectedMaterial?.MaterialRef;
        var materialRefs = await blender.Rna.ListAsync(BlenderSampleDataHelpers.MaterialsPath);
        var materialCards = await Task.WhenAll(materialRefs.Select(item => CreateMaterialLibraryItemAsync(blender, item)));
        var nextSelection = previousSelection is null
            ? materialCards.FirstOrDefault()
            : materialCards.FirstOrDefault(item => BlenderSampleDataHelpers.ReferenceMatches(item.MaterialRef, previousSelection))
              ?? materialCards.FirstOrDefault(item => string.Equals(item.DisplayName, previousSelection.Name, StringComparison.Ordinal))
              ?? materialCards.FirstOrDefault();

        await RunOnUiThreadAsync(() =>
        {
            foreach (var existing in Materials)
            {
                existing.Dispose();
            }

            Materials.Clear();
            foreach (var item in materialCards)
            {
                Materials.Add(item);
            }

            _isApplyingRemoteState = true;
            SelectedMaterial = nextSelection;
            _isApplyingRemoteState = false;
            return Task.CompletedTask;
        });

        if (nextSelection is not null)
        {
            await LoadSelectedMaterialAsync(nextSelection);
        }
        else
        {
            await RunOnUiThreadAsync(() =>
            {
                ClearSelectedMaterial();
                SetConnectedIdleStatus("No materials found.");
                return Task.CompletedTask;
            });
        }
    }

    private async Task SelectionChangedCoreAsync()
    {
        if (SelectedMaterial is null)
        {
            ClearSelectedMaterial();
            return;
        }

        await LoadSelectedMaterialAsync(SelectedMaterial);
    }

    private async Task CreateMaterialCoreAsync()
    {
        var requestedName = string.IsNullOrWhiteSpace(NewMaterialName) ? "Material" : NewMaterialName.Trim();
        var created = await RequireBlenderApi().Rna.CallAsync<RnaItemRef>(
            BlenderSampleDataHelpers.MaterialsPath,
            "new",
            ("name", requestedName));

        NewMaterialName = requestedName;
        await RefreshMaterialsCoreAsync();

        var matched = Materials.FirstOrDefault(item => BlenderSampleDataHelpers.ReferenceMatches(item.MaterialRef, created))
                      ?? Materials.FirstOrDefault(item => string.Equals(item.DisplayName, created.Name, StringComparison.Ordinal));
        if (matched is not null)
        {
            _isApplyingRemoteState = true;
            SelectedMaterial = matched;
            _isApplyingRemoteState = false;
            await LoadSelectedMaterialAsync(matched);
        }
    }

    private async Task CommitMaterialNameCoreAsync()
    {
        if (_isApplyingRemoteState || SelectedMaterial is null)
        {
            return;
        }

        await RequireBlenderApi().Rna.SetAsync($"{SelectedMaterial.MaterialRef.Path}.name", MaterialName);
        await RefreshMaterialsCoreAsync();
    }

    private async Task LoadSelectedMaterialAsync(MaterialLibraryItem material)
    {
        var blender = RequireBlenderApi();
        var materialName = await blender.Rna.GetAsync<string>($"{material.MaterialRef.Path}.name");
        var selectedPreview = await LoadPreviewImageAsync(blender, material.MaterialRef.Path, useLargePreview: true)
                              ?? await LoadPreviewImageAsync(blender, material.MaterialRef.Path, useLargePreview: false, ensureGeneratedIfMissing: false);

        await RunOnUiThreadAsync(() =>
        {
            _isApplyingRemoteState = true;
            MaterialName = materialName;
            SelectedMaterialPath = material.MaterialRef.Path;
            SelectedPreviewImage = selectedPreview;
            _isApplyingRemoteState = false;

            SetConnectedIdleStatus($"Loaded material {materialName}.");
            return Task.CompletedTask;
        });
    }

    private async Task EnsureWatchAsync()
    {
        if (_watchSubscription is not null || BlenderApi is null || !IsActive)
        {
            return;
        }

        _watchSubscription = await BlenderApi.Observe.WatchAsync(
            "materials-page",
            WatchSource.Depsgraph,
            BlenderSampleDataHelpers.MaterialsPath,
            async _ => await RefreshMaterialsAsync());
    }

    private async Task StopWatchAsync()
    {
        if (_watchSubscription is null)
        {
            return;
        }

        await _watchSubscription.DisposeAsync();
        _watchSubscription = null;
    }

    private void ClearSelectedMaterial()
    {
        _isApplyingRemoteState = true;
        MaterialName = string.Empty;
        SelectedMaterialPath = string.Empty;
        SelectedPreviewImage = null;
        _isApplyingRemoteState = false;
    }

    partial void OnSelectedPreviewImageChanged(IImage? oldValue, IImage? newValue)
    {
        if (!ReferenceEquals(oldValue, newValue) && oldValue is IDisposable disposable)
        {
            disposable.Dispose();
        }

        OnPropertyChanged(nameof(HasSelectedPreview));
        OnPropertyChanged(nameof(ShowSelectedPreviewPlaceholder));
    }

    private async Task<MaterialLibraryItem> CreateMaterialLibraryItemAsync(BlenderApi blender, RnaItemRef materialRef)
    {
        var displayName = await blender.Rna.GetAsync<string>($"{materialRef.Path}.name");
        var thumbnail = await LoadPreviewImageAsync(blender, materialRef.Path, useLargePreview: false);
        return new MaterialLibraryItem(materialRef, displayName, thumbnail);
    }

    private static async Task<IImage?> LoadPreviewImageAsync(
        BlenderApi blender,
        string materialPath,
        bool useLargePreview,
        bool ensureGeneratedIfMissing = true)
    {
        var preview = await TryLoadPreviewImageAsync(blender, materialPath, useLargePreview);
        if (preview is not null || !ensureGeneratedIfMissing)
        {
            return preview;
        }

        await EnsurePreviewAsync(blender, materialPath);
        return await TryLoadPreviewImageAsync(blender, materialPath, useLargePreview);
    }

    private static async Task EnsurePreviewAsync(BlenderApi blender, string materialPath)
    {
        try
        {
            _ = await blender.Rna.CallAsync<RnaItemRef>(materialPath, "preview_ensure");
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task<IImage?> TryLoadPreviewImageAsync(BlenderApi blender, string materialPath, bool useLargePreview)
    {
        var sizeSegment = useLargePreview ? "image_size" : "icon_size";
        var pixelsSegment = useLargePreview ? "image_pixels" : "icon_pixels";

        try
        {
            var size = await blender.Rna.GetAsync<int[]>($"{materialPath}.preview.{sizeSegment}");
            var pixels = await blender.Rna.ReadArrayAsync($"{materialPath}.preview.{pixelsSegment}");
            return MaterialPreviewImageFactory.Create(size, pixels.RawBytes);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
