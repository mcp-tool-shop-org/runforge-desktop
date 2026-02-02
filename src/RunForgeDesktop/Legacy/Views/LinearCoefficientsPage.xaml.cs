using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Page displaying detailed linear coefficients information.
/// </summary>
public partial class LinearCoefficientsPage : ContentPage
{
    public LinearCoefficientsPage(LinearCoefficientsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
