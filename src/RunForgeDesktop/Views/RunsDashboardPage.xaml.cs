using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class RunsDashboardPage : ContentPage
{
    private readonly RunsDashboardViewModel _viewModel;

    public RunsDashboardPage(RunsDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.StartPolling();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopPolling();
    }
}
