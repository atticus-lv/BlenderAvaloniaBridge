using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class MainViewModel : ObservableObject, IBlenderBridgeStatusSink, IBusinessEndpointSink
{
    private readonly ButtonsDemoPageViewModel _buttonsPage = new();
    private readonly SortableListDemoPageViewModel _sortableListPage = new();
    private readonly BlenderInspectorPageViewModel _blenderPage = new();

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private ObservableObject _currentPage;

    [ObservableProperty]
    private string _currentPageTitle;

    public MainViewModel()
    {
        _currentPage = _buttonsPage;
        _currentPageTitle = "Buttons + Checkbox";
    }

    public bool IsButtonsPageSelected => ReferenceEquals(CurrentPage, _buttonsPage);

    public bool IsSortableListPageSelected => ReferenceEquals(CurrentPage, _sortableListPage);

    public bool IsBlenderPageSelected => ReferenceEquals(CurrentPage, _blenderPage);

    public string SidebarToggleText => IsSidebarOpen ? "<" : ">";

    public void AttachBusinessEndpoint(IBusinessEndpoint? businessEndpoint)
    {
        _blenderPage.AttachBusinessEndpoint(businessEndpoint);
    }

    public void SetBridgeStatus(string status)
    {
        _blenderPage.SetBridgeStatus(status);
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
        OnPropertyChanged(nameof(SidebarToggleText));
    }

    [RelayCommand]
    private void ShowButtonsPage()
    {
        NavigateTo(_buttonsPage, "Buttons + Checkbox");
    }

    [RelayCommand]
    private void ShowSortableListPage()
    {
        NavigateTo(_sortableListPage, "Drag Sort List");
    }

    [RelayCommand]
    private void ShowBlenderPage()
    {
        NavigateTo(_blenderPage, "Blender Bridge");
    }

    partial void OnCurrentPageChanged(ObservableObject value)
    {
        OnPropertyChanged(nameof(IsButtonsPageSelected));
        OnPropertyChanged(nameof(IsSortableListPageSelected));
        OnPropertyChanged(nameof(IsBlenderPageSelected));
    }

    private void NavigateTo(ObservableObject page, string title)
    {
        CurrentPage = page;
        CurrentPageTitle = title;
    }
}
