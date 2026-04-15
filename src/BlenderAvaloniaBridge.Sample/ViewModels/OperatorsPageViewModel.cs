using System.Collections.ObjectModel;
using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class OperatorsPageViewModel : BlenderBridgePageViewModelBase
{
    private bool _isApplyingRemoteState;

    public OperatorsPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect operator calls.",
            "Waiting for Blender operator data.")
    {
    }

    public ObservableCollection<BlenderObjectListItem> Objects { get; } = new();

    [ObservableProperty]
    private BlenderObjectListItem? _selectedObject;

    [ObservableProperty]
    private string _selectedReferenceText = "No object selected";

    [ObservableProperty]
    private bool _canAddCubeOperator;

    [ObservableProperty]
    private bool _canDuplicateOperator;

    [ObservableProperty]
    private bool _canViewSelectedOperator;

    [ObservableProperty]
    private bool _canDeleteOperator;

    [ObservableProperty]
    private string _addCubePollText = "Poll has not run yet.";

    [ObservableProperty]
    private string _duplicatePollText = "Select an object to poll.";

    [ObservableProperty]
    private string _viewSelectedPollText = "Select an object to poll.";

    [ObservableProperty]
    private string _deletePollText = "Select an object to poll.";

    protected override Task OnActivatedAsync()
    {
        if (BlenderDataApi is null)
        {
            SetDisconnectedStatus();
            return Task.CompletedTask;
        }

        return RefreshObjectsAsync();
    }

    protected override void OnBlenderDataApiChanged()
    {
        UpdateCommandStates();
    }

    partial void OnSelectedObjectChanged(BlenderObjectListItem? value)
    {
        UpdateSelectedReferenceText();

        if (_isApplyingRemoteState)
        {
            return;
        }

        _ = SelectionChangedAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task RefreshObjectsAsync()
    {
        return RunPageOperationAsync(RefreshObjectsCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanUseBridge))]
    public Task RefreshPollAsync()
    {
        return RunPageOperationAsync(PollOperatorsCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRunAddCube))]
    public Task AddCubeAsync()
    {
        return RunPageOperationAsync(AddCubeCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRunDuplicate))]
    public Task DuplicateAsync()
    {
        return RunPageOperationAsync(DuplicateCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRunViewSelected))]
    public Task ViewSelectedAsync()
    {
        return RunPageOperationAsync(ViewSelectedCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRunDeleteSelected))]
    public Task DeleteSelectedAsync()
    {
        return RunPageOperationAsync(DeleteSelectedCoreAsync);
    }

    public Task SelectionChangedAsync()
    {
        return RunPageOperationAsync(PollOperatorsCoreAsync);
    }

    private bool CanUseBridge() => BlenderDataApi is not null;

    private bool CanRunAddCube() => BlenderDataApi is not null && CanAddCubeOperator;

    private bool CanRunDuplicate() => BlenderDataApi is not null && SelectedObject is not null && CanDuplicateOperator;

    private bool CanRunViewSelected() => BlenderDataApi is not null && SelectedObject is not null && CanViewSelectedOperator;

    private bool CanRunDeleteSelected() => BlenderDataApi is not null && SelectedObject is not null && CanDeleteOperator;

    private async Task RefreshObjectsCoreAsync()
    {
        var blender = RequireBlenderDataApi();
        var previousSelection = SelectedObject?.RnaRef;
        var objectItems = await BlenderSampleViewModelHelpers.LoadSceneObjectItemsAsync(blender);

        Objects.Clear();
        foreach (var item in objectItems)
        {
            Objects.Add(item);
        }

        BlenderObjectListItem? nextSelection = null;
        if (previousSelection is not null)
        {
            nextSelection = Objects.FirstOrDefault(item => BlenderSampleViewModelHelpers.ReferenceMatches(item.RnaRef, previousSelection));
        }

        nextSelection ??= Objects.FirstOrDefault(item => item.IsActive);
        nextSelection ??= Objects.FirstOrDefault();

        _isApplyingRemoteState = true;
        SelectedObject = nextSelection;
        _isApplyingRemoteState = false;

        await PollOperatorsCoreAsync();
    }

    private async Task PollOperatorsCoreAsync()
    {
        var blender = RequireBlenderDataApi();

        var addCube = await blender.PollOperatorAsync("mesh.primitive_cube_add");
        CanAddCubeOperator = addCube.CanExecute;
        AddCubePollText = BuildPollText(addCube);

        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            CanDuplicateOperator = false;
            CanViewSelectedOperator = false;
            CanDeleteOperator = false;
            DuplicatePollText = "Select an object to poll.";
            ViewSelectedPollText = "Select an object to poll.";
            DeletePollText = "Select an object to poll.";
            UpdateCommandStates();
            return;
        }

        var contextOverride = BlenderSampleViewModelHelpers.BuildSelectionContextOverride(rnaRef.Path);
        var duplicate = await blender.PollOperatorAsync("object.duplicate_move", contextOverride: contextOverride);
        var viewSelected = await blender.PollOperatorAsync("view3d.view_selected", contextOverride: contextOverride);
        var delete = await blender.PollOperatorAsync("object.delete", contextOverride: contextOverride);

        CanDuplicateOperator = duplicate.CanExecute;
        CanViewSelectedOperator = viewSelected.CanExecute;
        CanDeleteOperator = delete.CanExecute;
        DuplicatePollText = BuildPollText(duplicate);
        ViewSelectedPollText = BuildPollText(viewSelected);
        DeletePollText = BuildPollText(delete);
        UpdateCommandStates();
    }

    private async Task AddCubeCoreAsync()
    {
        var result = await RequireBlenderDataApi().CallOperatorAsync("mesh.primitive_cube_add", ("size", 2.0));
        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await RefreshObjectsCoreAsync();
    }

    private async Task DuplicateCoreAsync()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            return;
        }

        var result = await RequireBlenderDataApi().CallOperatorAsync(
            "object.duplicate_move",
            new BlenderOperatorCall
            {
                ContextOverride = BlenderSampleViewModelHelpers.BuildSelectionContextOverride(rnaRef.Path),
            });

        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await RefreshObjectsCoreAsync();
    }

    private async Task ViewSelectedCoreAsync()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            return;
        }

        var result = await RequireBlenderDataApi().CallOperatorAsync(
            "view3d.view_selected",
            new BlenderOperatorCall
            {
                ContextOverride = BlenderSampleViewModelHelpers.BuildSelectionContextOverride(rnaRef.Path),
            });

        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await PollOperatorsCoreAsync();
    }

    private async Task DeleteSelectedCoreAsync()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            return;
        }

        var result = await RequireBlenderDataApi().CallOperatorAsync(
            "object.delete",
            new BlenderOperatorCall
            {
                ContextOverride = BlenderSampleViewModelHelpers.BuildSelectionContextOverride(rnaRef.Path),
            });

        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await RefreshObjectsCoreAsync();
    }

    private void UpdateSelectedReferenceText()
    {
        if (SelectedObject?.RnaRef is not { } rnaRef)
        {
            SelectedReferenceText = "No object selected";
            return;
        }

        SelectedReferenceText = $"{rnaRef.Name} | {rnaRef.Path}";
    }

    private void UpdateCommandStates()
    {
        RefreshObjectsCommand.NotifyCanExecuteChanged();
        RefreshPollCommand.NotifyCanExecuteChanged();
        AddCubeCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        ViewSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    private static string BuildPollText(OperatorPollResult result)
    {
        return result.CanExecute
            ? "Ready"
            : string.IsNullOrWhiteSpace(result.FailureReason)
                ? "Poll failed"
                : $"Blocked: {result.FailureReason}";
    }

    private static string FormatOperatorResult(OperatorCallResult result)
    {
        return result.Result.Count > 0 ? string.Join(", ", result.Result) : "no result";
    }
}
