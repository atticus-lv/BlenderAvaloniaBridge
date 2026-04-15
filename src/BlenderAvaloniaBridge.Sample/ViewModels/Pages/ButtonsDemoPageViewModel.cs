using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public partial class ButtonsDemoPageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isPrimaryOptionEnabled = true;

    [ObservableProperty]
    private bool _isSecondaryOptionEnabled;

    [ObservableProperty]
    private string _activityText = "Click a button or toggle a checkbox to see the state update.";

    [RelayCommand]
    private void TriggerPrimaryAction()
    {
        ActivityText = $"Primary action fired at {DateTime.Now:HH:mm:ss}.";
    }

    [RelayCommand]
    private void TriggerSecondaryAction()
    {
        ActivityText = $"Secondary action fired with primary={IsPrimaryOptionEnabled} secondary={IsSecondaryOptionEnabled}.";
    }

    partial void OnIsPrimaryOptionEnabledChanged(bool value)
    {
        ActivityText = $"Primary checkbox changed to {(value ? "checked" : "unchecked")}.";
    }

    partial void OnIsSecondaryOptionEnabledChanged(bool value)
    {
        ActivityText = $"Secondary checkbox changed to {(value ? "checked" : "unchecked")}.";
    }
}
