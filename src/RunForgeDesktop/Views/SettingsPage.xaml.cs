using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
