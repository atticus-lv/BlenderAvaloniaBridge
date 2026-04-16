using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.ViewModels.Pages;
using Material.Icons;
using System.Threading;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public partial class MainViewModel : ObservableObject, IBlenderBridgeStatusSink, IBlenderApiSink
{
    private readonly ButtonsDemoPageViewModel _buttonsPage = new();
    private readonly SortableListDemoPageViewModel _sortableListPage = new();
    private readonly BlenderObjectsPageViewModel _blenderObjectsPage = new();
    private readonly LiveTransformPageViewModel _liveTransformPage = new();
    private readonly MaterialsPageViewModel _materialsPage = new();
    private readonly CollectionsPageViewModel _collectionsPage = new();
    private readonly OperatorsPageViewModel _operatorsPage = new();
    private readonly IBlenderSamplePageViewModel[] _blenderPages;
    private readonly SemaphoreSlim _navigationTransitionGate = new(1, 1);
    private int _navigationVersion;

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private ObservableObject _currentPage;

    [ObservableProperty]
    private string _currentPageTitle;

    public MainViewModel()
    {
        _blenderPages =
        [
            _blenderObjectsPage,
            _liveTransformPage,
            _materialsPage,
            _collectionsPage,
            _operatorsPage,
        ];

        _currentPage = _buttonsPage;
        _currentPageTitle = "Buttons + Checkbox";
    }

    public bool IsButtonsPageSelected => ReferenceEquals(CurrentPage, _buttonsPage);

    public bool IsSortableListPageSelected => ReferenceEquals(CurrentPage, _sortableListPage);

    public bool IsBlenderObjectsPageSelected => ReferenceEquals(CurrentPage, _blenderObjectsPage);

    public bool IsLiveTransformPageSelected => ReferenceEquals(CurrentPage, _liveTransformPage);

    public bool IsMaterialsPageSelected => ReferenceEquals(CurrentPage, _materialsPage);

    public bool IsCollectionsPageSelected => ReferenceEquals(CurrentPage, _collectionsPage);

    public bool IsOperatorsPageSelected => ReferenceEquals(CurrentPage, _operatorsPage);

    public StreamGeometry SidebarToggleIcon => StreamGeometry.Parse(
        MaterialIconDataProvider.GetData(IsSidebarOpen
            ? MaterialIconKind.AlignHorizontalRight
            : MaterialIconKind.AlignHorizontalLeft));


    public void AttachBlenderApi(BlenderApi? blenderApi)
    {
        foreach (var page in _blenderPages)
        {
            page.AttachBlenderApi(blenderApi);
        }
    }

    public void SetBridgeStatus(string status)
    {
        foreach (var page in _blenderPages)
        {
            page.SetBridgeStatus(status);
        }
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
        OnPropertyChanged(nameof(SidebarToggleIcon));
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
    private void ShowBlenderObjectsPage()
    {
        NavigateTo(_blenderObjectsPage, "Blender Objects");
    }

    [RelayCommand]
    private void ShowLiveTransformPage()
    {
        NavigateTo(_liveTransformPage, "Live Transform");
    }

    [RelayCommand]
    private void ShowMaterialsPage()
    {
        NavigateTo(_materialsPage, "Materials");
    }

    [RelayCommand]
    private void ShowCollectionsPage()
    {
        NavigateTo(_collectionsPage, "Collections");
    }

    [RelayCommand]
    private void ShowOperatorsPage()
    {
        NavigateTo(_operatorsPage, "Operator Playground");
    }

    partial void OnCurrentPageChanged(ObservableObject value)
    {
        OnPropertyChanged(nameof(IsButtonsPageSelected));
        OnPropertyChanged(nameof(IsSortableListPageSelected));
        OnPropertyChanged(nameof(IsBlenderObjectsPageSelected));
        OnPropertyChanged(nameof(IsLiveTransformPageSelected));
        OnPropertyChanged(nameof(IsMaterialsPageSelected));
        OnPropertyChanged(nameof(IsCollectionsPageSelected));
        OnPropertyChanged(nameof(IsOperatorsPageSelected));
    }

    private void NavigateTo(ObservableObject page, string title)
    {
        if (ReferenceEquals(CurrentPage, page))
        {
            CurrentPageTitle = title;
            return;
        }

        var previousPage = CurrentPage;
        CurrentPage = page;
        CurrentPageTitle = title;
        var navigationVersion = Interlocked.Increment(ref _navigationVersion);
        _ = TransitionPagesAsync(previousPage, page, navigationVersion);
    }

    private async Task TransitionPagesAsync(ObservableObject? previousPage, ObservableObject nextPage, int navigationVersion)
    {
        await _navigationTransitionGate.WaitAsync();
        try
        {
            if (previousPage is IBlenderSamplePageViewModel previousBlenderPage)
            {
                await previousBlenderPage.DeactivateAsync();
            }

            if (navigationVersion != Volatile.Read(ref _navigationVersion) || !ReferenceEquals(CurrentPage, nextPage))
            {
                return;
            }

            if (nextPage is IBlenderSamplePageViewModel nextBlenderPage)
            {
                await nextBlenderPage.ActivateAsync();
            }
        }
        catch
        {
            // Each page stores its own failure state, so navigation should remain resilient.
        }
        finally
        {
            _navigationTransitionGate.Release();
        }
    }
}
