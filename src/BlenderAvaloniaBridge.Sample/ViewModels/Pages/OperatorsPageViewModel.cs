using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public partial class OperatorsPageViewModel : BlenderBridgePageViewModelBase
{
    public OperatorsPageViewModel()
        : base(
            "Desktop window mode. Start the bridge from Blender to inspect and invoke operators.",
            "Refresh the current Blender context object to preview operator poll state.")
    {
    }

    [ObservableProperty]
    private RnaItemRef? _currentObject;

    [ObservableProperty]
    private string _selectedReferenceText = "No active context object";

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
    private string _duplicatePollText = "Refresh until bpy.context.object resolves to an object.";

    [ObservableProperty]
    private string _viewSelectedPollText = "Refresh until bpy.context.object resolves to an object.";

    [ObservableProperty]
    private string _deletePollText = "Refresh until bpy.context.object resolves to an object.";

    protected override Task OnActivatedAsync()
    {
        if (BlenderApi is null)
        {
            SetDisconnectedStatus();
            return Task.CompletedTask;
        }

        return RefreshObjectsAsync();
    }

    protected override void OnBlenderApiChanged()
    {
        UpdateCommandStates();
    }

    partial void OnCurrentObjectChanged(RnaItemRef? value)
    {
        UpdateSelectedReferenceText();
        UpdateCommandStates();
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

    private bool CanUseBridge() => BlenderApi is not null;

    private bool CanRunAddCube() => BlenderApi is not null && CanAddCubeOperator;

    private bool CanRunDuplicate() => BlenderApi is not null && CurrentObject is not null && CanDuplicateOperator;

    private bool CanRunViewSelected() => BlenderApi is not null && CurrentObject is not null && CanViewSelectedOperator;

    private bool CanRunDeleteSelected() => BlenderApi is not null && CurrentObject is not null && CanDeleteOperator;

    private async Task RefreshObjectsCoreAsync()
    {
        var blender = RequireBlenderApi();
        RnaItemRef? currentObject;

        try
        {
            currentObject = await blender.Rna.GetAsync<RnaItemRef>(BlenderSampleDataHelpers.CurrentObjectPath);
        }
        catch (InvalidOperationException)
        {
            currentObject = null;
        }

        CurrentObject = currentObject;
        await PollOperatorsCoreAsync();
    }

    private async Task PollOperatorsCoreAsync()
    {
        var blender = RequireBlenderApi();

        var addCube = await blender.Ops.PollAsync("mesh.primitive_cube_add");
        CanAddCubeOperator = addCube.CanExecute;
        AddCubePollText = BuildPollText(addCube);

        if (CurrentObject is not { } rnaRef)
        {
            CanDuplicateOperator = false;
            CanViewSelectedOperator = false;
            CanDeleteOperator = false;
            DuplicatePollText = "Refresh until bpy.context.object resolves to an object.";
            ViewSelectedPollText = "Refresh until bpy.context.object resolves to an object.";
            DeletePollText = "Refresh until bpy.context.object resolves to an object.";
            SetConnectedIdleStatus("No active context object.");
            UpdateCommandStates();
            return;
        }

        var contextOverride = BlenderSampleDataHelpers.BuildSelectionContextOverride(rnaRef.Path);
        var duplicate = await blender.Ops.PollAsync("object.duplicate_move", contextOverride: contextOverride);
        var viewSelected = await blender.Ops.PollAsync("view3d.view_selected", contextOverride: contextOverride);
        var delete = await blender.Ops.PollAsync("object.delete", contextOverride: contextOverride);

        CanDuplicateOperator = duplicate.CanExecute;
        CanViewSelectedOperator = viewSelected.CanExecute;
        CanDeleteOperator = delete.CanExecute;
        DuplicatePollText = BuildPollText(duplicate);
        ViewSelectedPollText = BuildPollText(viewSelected);
        DeletePollText = BuildPollText(delete);
        SetConnectedIdleStatus($"Operator poll refreshed for {rnaRef.Name}.");
        UpdateCommandStates();
    }

    private async Task AddCubeCoreAsync()
    {
        var result = await RequireBlenderApi().Ops.CallAsync("mesh.primitive_cube_add", ("size", 2.0));
        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await RefreshObjectsCoreAsync();
    }

    private async Task DuplicateCoreAsync()
    {
        if (CurrentObject is not { } rnaRef)
        {
            return;
        }

        var result = await RequireBlenderApi().Ops.CallAsync(
            "object.duplicate_move",
            new BlenderOperatorCall
            {
                ContextOverride = BlenderSampleDataHelpers.BuildSelectionContextOverride(rnaRef.Path),
            });

        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await RefreshObjectsCoreAsync();
    }

    private async Task ViewSelectedCoreAsync()
    {
        if (CurrentObject is not { } rnaRef)
        {
            return;
        }

        var result = await RequireBlenderApi().Ops.CallAsync(
            "view3d.view_selected",
            new BlenderOperatorCall
            {
                ContextOverride = BlenderSampleDataHelpers.BuildSelectionContextOverride(rnaRef.Path),
            });

        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await PollOperatorsCoreAsync();
    }

    private async Task DeleteSelectedCoreAsync()
    {
        if (CurrentObject is not { } rnaRef)
        {
            return;
        }

        var result = await RequireBlenderApi().Ops.CallAsync(
            "object.delete",
            new BlenderOperatorCall
            {
                ContextOverride = BlenderSampleDataHelpers.BuildSelectionContextOverride(rnaRef.Path),
            });

        StatusText = $"{result.OperatorName}: {FormatOperatorResult(result)}";
        await RefreshObjectsCoreAsync();
    }

    private void UpdateSelectedReferenceText()
    {
        if (CurrentObject is not { } rnaRef)
        {
            SelectedReferenceText = "No active context object";
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
