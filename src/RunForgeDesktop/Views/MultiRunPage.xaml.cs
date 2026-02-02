using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class MultiRunPage : ContentPage
{
    public MultiRunPage(MultiRunViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
