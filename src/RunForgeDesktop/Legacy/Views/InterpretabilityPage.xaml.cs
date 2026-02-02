using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Page for viewing the interpretability index and artifacts.
/// </summary>
public partial class InterpretabilityPage : ContentPage
{
    public InterpretabilityPage(InterpretabilityViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
