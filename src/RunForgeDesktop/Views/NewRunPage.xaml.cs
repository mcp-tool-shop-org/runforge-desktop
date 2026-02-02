using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class NewRunPage : ContentPage
{
    public NewRunPage(NewRunViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
