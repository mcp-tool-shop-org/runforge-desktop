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

        // Subscribe to theme changes
        _viewModel.ThemeChanged += OnThemeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(string theme)
    {
        // Apply theme through the App instance
        if (Application.Current is App app)
        {
            app.ApplyTheme(theme);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
