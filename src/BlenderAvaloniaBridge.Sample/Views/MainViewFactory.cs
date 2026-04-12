using BlenderAvaloniaBridge.Sample.ViewModels;

namespace BlenderAvaloniaBridge.Sample.Views;

public static class MainViewFactory
{
    public static MainView CreateMainView()
    {
        return CreateMainView(new MainViewModel());
    }

    public static MainView CreateMainView(MainViewModel viewModel)
    {
        return new MainView
        {
            DataContext = viewModel
        };
    }
}
