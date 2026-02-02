using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class LiveRunPage : ContentPage
{
    private readonly LiveRunViewModel _viewModel;

    public LiveRunPage(LiveRunViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Wire up chart invalidation
        _viewModel.ChartInvalidated += OnChartInvalidated;
    }

    private void OnChartInvalidated()
    {
        // Invalidate the GraphicsView to trigger redraw
        LossChart.Invalidate();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ChartInvalidated -= OnChartInvalidated;
        _viewModel.Dispose();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("settings");
    }
}
