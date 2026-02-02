using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class MultiRunPage : ContentPage
{
    public MultiRunPage(MultiRunViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
