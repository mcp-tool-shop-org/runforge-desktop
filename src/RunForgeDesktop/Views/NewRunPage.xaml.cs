using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class NewRunPage : ContentPage
{
    public NewRunPage(NewRunViewModel viewModel)
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
