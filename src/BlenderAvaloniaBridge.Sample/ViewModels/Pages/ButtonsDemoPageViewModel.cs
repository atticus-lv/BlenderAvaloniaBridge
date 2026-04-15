using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public partial class ButtonsDemoPageViewModel : ObservableObject
{
    public IReadOnlyList<string> QualityOptions { get; } =
    [
        "Draft",
        "Preview",
        "Final",
    ];

    [ObservableProperty]
    private bool _isPrimaryOptionEnabled = true;

    [ObservableProperty]
    private bool _isSecondaryOptionEnabled;

    [ObservableProperty]
    private bool _isAutomationEnabled = true;

    [ObservableProperty]
    private string _notesText = "Review the current bridge state before applying changes.";

    [ObservableProperty]
    private string _selectedQuality = "Preview";

    [ObservableProperty]
    private double _intensity = 62;

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

    partial void OnIsAutomationEnabledChanged(bool value)
    {
        ActivityText = $"Automation is now {(value ? "enabled" : "disabled")}.";
    }

    partial void OnSelectedQualityChanged(string value)
    {
        ActivityText = $"Quality preset switched to {value}.";
    }

    partial void OnIntensityChanged(double value)
    {
        ActivityText = $"Intensity adjusted to {Math.Round(value)}%.";
    }
}
