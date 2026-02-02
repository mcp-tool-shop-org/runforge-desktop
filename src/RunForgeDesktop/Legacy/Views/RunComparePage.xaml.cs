using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class RunComparePage : ContentPage
{
    public RunComparePage(RunCompareViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
